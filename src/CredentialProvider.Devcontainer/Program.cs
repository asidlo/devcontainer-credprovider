// NuGet Credential Provider Plugin for Devcontainers
// Uses NuGet.Protocol library for proper plugin protocol handling
// See: https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin
//
// This plugin only uses auth helpers (ado-auth-helper, azure-auth-helper).
// If auth helpers are not available or fail, it returns NotApplicable to allow
// fallback to other credential providers like Microsoft's artifacts-credprovider.

using System.Diagnostics;
using System.Reflection;
using NuGet.Protocol.Plugins;
using OtpNet;
using QRCoder;

namespace CredentialProvider.Devcontainer;

public static class Program
{
    private static readonly string[] AuthHelperPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ado-auth-helper"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "azure-auth-helper"),
        "/usr/local/bin/ado-auth-helper",
        "/usr/local/bin/azure-auth-helper"
    ];

    private static readonly string TwoFactorSecretPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget-credprovider-2fa-secret"
    );

    /// <summary>
    /// Gets the version from the assembly's InformationalVersion attribute (set by MinVer).
    /// Format: "1.2.3" for tagged releases, "1.2.4-alpha.0.5+abc1234" for untagged commits.
    /// </summary>
    internal static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return infoVersion?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }

    public static async Task<int> Main(string[] args)
    {
        // Handle -Plugin argument (required for NuGet to recognize this as a credential provider)
        if (args.Contains("-Plugin", StringComparer.OrdinalIgnoreCase))
        {
            return await RunAsPluginAsync();
        }

        // Handle standalone mode for testing
        if (args.Contains("--version") || args.Contains("-v"))
        {
            Console.WriteLine($"CredentialProvider.Devcontainer {GetVersion()}");
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine($"Devcontainer.CredentialProvider v{GetVersion()}");
            Console.WriteLine("NuGet Credential Provider for Devcontainers");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  CredentialProvider.Devcontainer -Plugin       Run as NuGet plugin");
            Console.WriteLine("  CredentialProvider.Devcontainer --test        Test credential acquisition");
            Console.WriteLine("  CredentialProvider.Devcontainer --setup-2fa   Setup/view 2FA configuration");
            Console.WriteLine("  CredentialProvider.Devcontainer --version     Show version info");
            Console.WriteLine();
            return 0;
        }

        if (args.Contains("--setup-2fa"))
        {
            return SetupTwoFactorAuth();
        }

        if (args.Contains("--test"))
        {
            return await TestCredentialsAsync();
        }

        Console.Error.WriteLine("Use -Plugin to run as NuGet credential provider, or --test to test credentials");
        return 1;
    }

    internal static async Task<int> TestCredentialsAsync()
    {
        Console.WriteLine($"CredentialProvider.Devcontainer v{GetVersion()}");
        Console.WriteLine("Testing credential acquisition...");

        var token = await TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        if (!string.IsNullOrEmpty(token))
        {
            Console.WriteLine($"✓ Successfully acquired token (length: {token.Length})");
            return 0;
        }

        Console.WriteLine("✗ Failed to acquire token");
        return 1;
    }

    private static async Task<int> RunAsPluginAsync()
    {
        Console.Error.WriteLine("[CredentialProvider.Devcontainer] Plugin starting...");

        try
        {
            // Create request handlers
            var requestHandlers = new RequestHandlers();

            // Add our credential handlers
            requestHandlers.TryAdd(MessageMethod.GetAuthenticationCredentials, new GetAuthenticationCredentialsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.GetOperationClaims, new GetOperationClaimsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.SetLogLevel, new SetLogLevelRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Initialize, new InitializeRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Close, new CloseRequestHandler());

            // Use default connection options
            var options = ConnectionOptions.CreateDefault();

            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Initializing plugin connection...");

            // Create plugin using the official NuGet.Protocol factory
            // This handles all the complex bidirectional handshake
            using var cts = new CancellationTokenSource();

            // Handle process exit cleanly
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var plugin = await PluginFactory.CreateFromCurrentProcessAsync(
                requestHandlers,
                options,
                cts.Token);

            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Plugin connected, protocol version: {plugin.Connection.ProtocolVersion}");

            // Listen for connection faults (NuGet client disconnect)
            var connectionClosed = new TaskCompletionSource<bool>();
            plugin.Connection.Faulted += (sender, args) =>
            {
                Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Connection faulted: {args.Exception?.Message}");
                connectionClosed.TrySetResult(true);
            };

            plugin.Closed += (sender, args) =>
            {
                Console.Error.WriteLine("[CredentialProvider.Devcontainer] Plugin closed by client");
                connectionClosed.TrySetResult(true);
            };

            // Keep alive until connection closes or cancelled
            try
            {
                await Task.WhenAny(
                    connectionClosed.Task,
                    Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)
                );
            }
            catch (OperationCanceledException)
            {
                // Expected when Ctrl+C pressed
            }

            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Plugin shutting down...");
            
            // Dispose is handled by 'using' statement above

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Plugin error: {ex}");
            return 1;
        }
    }

    internal static async Task<string?> TryGetAccessTokenAsync(string packageSourceUri, CancellationToken cancellationToken = default)
    {
        // Validate 2FA if configured
        if (!ValidateTwoFactorAuth())
        {
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] 2FA validation failed, denying access");
            return null;
        }

        // Try auth helpers (devcontainer scenario)
        // If auth helpers are not available or fail, return null to allow fallback
        // to other credential providers like Microsoft's artifacts-credprovider
        var helperToken = await TryGetTokenFromAuthHelperAsync(cancellationToken);
        if (!string.IsNullOrEmpty(helperToken))
        {
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Acquired token via auth helper");
            return helperToken;
        }

        Console.Error.WriteLine("[CredentialProvider.Devcontainer] Auth helper not available, returning NotApplicable to allow fallback to other providers");
        return null;
    }

    internal static async Task<string?> TryGetTokenFromAuthHelperAsync(CancellationToken cancellationToken = default)
    {
        // Retry settings - auth helpers may take time to initialize after container start
        const int maxRetries = 3;
        const int retryDelayMs = 2000;

        foreach (var helperPath in AuthHelperPaths)
        {
            if (!File.Exists(helperPath))
            {
                continue;
            }

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Trying auth helper: {helperPath} (attempt {attempt}/{maxRetries})");

                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = helperPath,
                            Arguments = "get-access-token",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();

                    // Use a timeout with cancellation support
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                    try
                    {
                        var token = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                        await process.WaitForExitAsync(timeoutCts.Token);

                        if (process.ExitCode == 0)
                        {
                            token = token.Trim();
                            if (!string.IsNullOrEmpty(token) && !token.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                            {
                                return token;
                            }
                        }

                        // Token was empty or invalid - retry if we have attempts left
                        if (attempt < maxRetries)
                        {
                            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Auth helper returned no token, retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Timeout - kill the process and retry
                        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Auth helper timed out: {helperPath}");
                        try { process.Kill(entireProcessTree: true); } catch { }

                        if (attempt < maxRetries)
                        {
                            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Auth helper {helperPath} failed: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Retrying in {retryDelayMs}ms...");
                        await Task.Delay(retryDelayMs, cancellationToken);
                    }
                }
            }
        }

        return null;
    }

    internal static bool IsAzureDevOpsUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return false;
        }

        return uri.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("azure.com/_packaging", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets or generates the 2FA TOTP secret.
    /// Checks environment variable first, then persistent file, then generates new.
    /// </summary>
    internal static string GetOrCreateTwoFactorSecret()
    {
        // First check environment variable (for Codespaces secrets)
        var envSecret = Environment.GetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET");
        if (!string.IsNullOrWhiteSpace(envSecret))
        {
            return envSecret.Trim();
        }

        // Check persistent file
        if (File.Exists(TwoFactorSecretPath))
        {
            try
            {
                var fileSecret = File.ReadAllText(TwoFactorSecretPath).Trim();
                if (!string.IsNullOrWhiteSpace(fileSecret))
                {
                    return fileSecret;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Warning: Could not read 2FA secret file: {ex.Message}");
            }
        }

        // Generate new secret
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);
        
        // Try to save it
        try
        {
            File.WriteAllText(TwoFactorSecretPath, secret);
            // Set file permissions to be readable only by owner (Unix-like systems)
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(TwoFactorSecretPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Generated new 2FA secret and saved to {TwoFactorSecretPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Warning: Could not save 2FA secret: {ex.Message}");
        }

        return secret;
    }

    /// <summary>
    /// Setup or display 2FA configuration with QR code.
    /// </summary>
    internal static int SetupTwoFactorAuth()
    {
        Console.WriteLine($"CredentialProvider.Devcontainer v{GetVersion()}");
        Console.WriteLine("Two-Factor Authentication Setup");
        Console.WriteLine("================================");
        Console.WriteLine();

        var secret = GetOrCreateTwoFactorSecret();
        
        Console.WriteLine("⚠️  WARNING: Keep your secret private! Do not share or commit it to source control.");
        Console.WriteLine();
        Console.WriteLine($"Secret: {secret}");
        Console.WriteLine($"Stored in: {TwoFactorSecretPath}");
        Console.WriteLine();

        // Generate TOTP URI
        var totpUri = $"otpauth://totp/NuGetCredProvider?secret={secret}&issuer=DevcontainerCredProvider";
        
        Console.WriteLine("Scan this QR code with your authenticator app:");
        Console.WriteLine();

        // Generate and display QR code
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new AsciiQRCode(qrCodeData);
            var qrCodeAsAscii = qrCode.GetGraphic(1);
            Console.WriteLine(qrCodeAsAscii);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not generate QR code: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine($"Manual setup URL: {totpUri}");
        }

        Console.WriteLine();
        Console.WriteLine("Or enter this information manually in your authenticator app:");
        Console.WriteLine($"  Account: NuGetCredProvider");
        Console.WriteLine($"  Secret: {secret}");
        Console.WriteLine($"  Type: Time-based (TOTP)");
        Console.WriteLine();

        // Show current code for verification
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            var currentCode = totp.ComputeTotp();
            Console.WriteLine($"Current code (for verification): {currentCode}");
            Console.WriteLine("This code changes every 30 seconds.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not generate current code: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("To use 2FA:");
        Console.WriteLine("  1. Scan the QR code or enter the secret in your authenticator app");
        Console.WriteLine("  2. Set NUGET_CREDPROVIDER_2FA_ENABLED=true in your environment");
        Console.WriteLine("  3. Provide the current code via NUGET_CREDPROVIDER_2FA_CODE when prompted");
        Console.WriteLine();

        return 0;
    }

    /// <summary>
    /// Validates TOTP (Time-based One-Time Password) for 2FA if enabled.
    /// Checks NUGET_CREDPROVIDER_2FA_ENABLED to determine if 2FA should be used.
    /// If enabled, gets secret and validates the provided code.
    /// </summary>
    internal static bool ValidateTwoFactorAuth()
    {
        var enabled = Environment.GetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_ENABLED");
        
        if (string.IsNullOrWhiteSpace(enabled) || !bool.TryParse(enabled, out var isEnabled) || !isEnabled)
        {
            // 2FA not enabled, skip validation
            return true;
        }

        Console.Error.WriteLine("[CredentialProvider.Devcontainer] 2FA enabled, validating...");

        var totpCode = Environment.GetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE");
        
        if (string.IsNullOrWhiteSpace(totpCode))
        {
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] 2FA enabled but no code provided in NUGET_CREDPROVIDER_2FA_CODE");
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Run: dotnet <path-to-dll> --setup-2fa");
            return false;
        }

        try
        {
            // Get or create the secret
            var totpSecret = GetOrCreateTwoFactorSecret();
            
            // Decode the base32 secret and create TOTP generator
            var secretBytes = Base32Encoding.ToBytes(totpSecret);
            var totp = new Totp(secretBytes);
            
            // Verify the code with a time window (allow 1 step before and after for clock skew)
            var isValid = totp.VerifyTotp(totpCode, out _, new VerificationWindow(previous: 1, future: 1));
            
            if (isValid)
            {
                Console.Error.WriteLine("[CredentialProvider.Devcontainer] 2FA validation successful");
            }
            else
            {
                Console.Error.WriteLine("[CredentialProvider.Devcontainer] 2FA validation failed - invalid code");
                // Don't log the expected code in production for security reasons
                // Users can run --setup-2fa to see the current code
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] 2FA validation error: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Handles GetOperationClaims requests - tells NuGet what operations this plugin supports
/// </summary>
internal sealed class GetOperationClaimsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetOperationClaimsRequest>(request);
        var packageSourceUri = payload?.PackageSourceRepository;

        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] GetOperationClaims: '{packageSourceUri}'");

        // When package source is null/empty, NuGet is asking for source-agnostic operations
        // Return Authentication to indicate we handle auth
        // When package source is specified, only claim it if it's Azure DevOps
        var claims = new List<OperationClaim>();

        if (string.IsNullOrEmpty(packageSourceUri))
        {
            // Source-agnostic query - we support Authentication generally
            claims.Add(OperationClaim.Authentication);
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Claiming Authentication (source-agnostic)");
        }
        else if (Program.IsAzureDevOpsUri(packageSourceUri))
        {
            // Source-specific query - we handle Azure DevOps feeds
            claims.Add(OperationClaim.Authentication);
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Claiming Authentication for Azure DevOps");
        }
        else
        {
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Not claiming (not Azure DevOps)");
        }

        var response = new GetOperationClaimsResponse(claims);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

/// <summary>
/// Handles GetAuthenticationCredentials requests - provides credentials for Azure Artifacts feeds
/// </summary>
internal sealed class GetAuthenticationCredentialsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public async Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetAuthenticationCredentialsRequest>(request);
        var uri = payload?.Uri?.AbsoluteUri;

        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] GetAuthenticationCredentials: {uri}, IsRetry={payload?.IsRetry}");

        if (string.IsNullOrEmpty(uri) || !Program.IsAzureDevOpsUri(uri))
        {
            // Return error for non-Azure DevOps URIs - NuGet will try other providers
            var notApplicableResponse = new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: "Not an Azure DevOps feed",
                authenticationTypes: null,
                responseCode: MessageResponseCode.NotFound);

            await responseHandler.SendResponseAsync(request, notApplicableResponse, cancellationToken);
            return;
        }

        // Pass cancellation token to token acquisition
        var token = await Program.TryGetAccessTokenAsync(uri, cancellationToken);

        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("[CredentialProvider.Devcontainer] Auth helper not available, returning NotApplicable for fallback to other providers");

            // Return NotApplicable to allow NuGet to try other credential providers
            // (like Microsoft's artifacts-credprovider which supports device code flow)
            var notApplicableResponse = new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: "Auth helper not available. Falling back to other credential providers.",
                authenticationTypes: null,
                responseCode: MessageResponseCode.NotFound);

            await responseHandler.SendResponseAsync(request, notApplicableResponse, cancellationToken);
            return;
        }

        Console.Error.WriteLine("[CredentialProvider.Devcontainer] Successfully acquired credentials");

        var successResponse = new GetAuthenticationCredentialsResponse(
            username: "DevcontainerCredProvider",
            password: token,
            message: null,
            authenticationTypes: new List<string> { "Basic" },
            responseCode: MessageResponseCode.Success);

        await responseHandler.SendResponseAsync(request, successResponse, cancellationToken);
    }
}

/// <summary>
/// Handles SetLogLevel requests
/// </summary>
internal sealed class SetLogLevelRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<SetLogLevelRequest>(request);
        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] SetLogLevel: {payload?.LogLevel}");

        var response = new SetLogLevelResponse(MessageResponseCode.Success);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

/// <summary>
/// Handles Initialize requests - called by NuGet to initialize the plugin
/// </summary>
internal sealed class InitializeRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<InitializeRequest>(request);
        Console.Error.WriteLine($"[CredentialProvider.Devcontainer] Initialize: ClientVersion={payload?.ClientVersion}, Culture={payload?.Culture}");

        // Update connection timeout if provided
        if (payload?.RequestTimeout != null)
        {
            connection.Options.SetRequestTimeout(payload.RequestTimeout);
        }

        var response = new InitializeResponse(MessageResponseCode.Success);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

/// <summary>
/// Handles Close requests - called by NuGet when shutting down the plugin
/// </summary>
internal sealed class CloseRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("[CredentialProvider.Devcontainer] Close request received");

        // Acknowledge the close request
        // The plugin will exit after responding
        return Task.CompletedTask;
    }
}
