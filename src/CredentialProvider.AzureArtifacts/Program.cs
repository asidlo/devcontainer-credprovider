// NuGet Credential Provider Plugin for Azure Artifacts
// Uses NuGet.Protocol library for proper plugin protocol handling
// See: https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin

using System.Diagnostics;
using System.Reflection;
using Azure.Core;
using Azure.Identity;
using NuGet.Protocol.Plugins;

namespace AzureArtifacts.CredentialProvider;

public static class Program
{
    private static readonly string[] AuthHelperPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ado-auth-helper"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "azure-auth-helper"),
        "/usr/local/bin/ado-auth-helper",
        "/usr/local/bin/azure-auth-helper"
    ];

    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    /// <summary>
    /// Gets the version from the assembly's InformationalVersion attribute (set by MinVer).
    /// Format: "1.2.3" for tagged releases, "1.2.4-alpha.0.5+abc1234" for untagged commits.
    /// </summary>
    private static string GetVersion()
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
            Console.WriteLine($"CredentialProvider.AzureArtifacts {GetVersion()}");
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine($"AzureArtifacts.CredentialProvider v{GetVersion()}");
            Console.WriteLine("NuGet Credential Provider for Azure Artifacts");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  CredentialProvider.AzureArtifacts -Plugin     Run as NuGet plugin");
            Console.WriteLine("  CredentialProvider.AzureArtifacts --test      Test credential acquisition");
            Console.WriteLine("  CredentialProvider.AzureArtifacts --version   Show version info");
            Console.WriteLine();
            return 0;
        }

        if (args.Contains("--test"))
        {
            return await TestCredentialsAsync();
        }

        Console.Error.WriteLine("Use -Plugin to run as NuGet credential provider, or --test to test credentials");
        return 1;
    }

    private static async Task<int> TestCredentialsAsync()
    {
        Console.WriteLine($"CredentialProvider.AzureArtifacts v{GetVersion()}");
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
        Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Plugin starting...");

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

            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Initializing plugin connection...");

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

            Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Plugin connected, protocol version: {plugin.Connection.ProtocolVersion}");

            // Listen for connection faults (NuGet client disconnect)
            var connectionClosed = new TaskCompletionSource<bool>();
            plugin.Connection.Faulted += (sender, args) =>
            {
                Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Connection faulted: {args.Exception?.Message}");
                connectionClosed.TrySetResult(true);
            };

            plugin.Closed += (sender, args) =>
            {
                Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Plugin closed by client");
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

            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Plugin shutting down...");
            
            // Dispose is handled by 'using' statement above

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Plugin error: {ex}");
            return 1;
        }
    }

    internal static async Task<string?> TryGetAccessTokenAsync(string packageSourceUri, CancellationToken cancellationToken = default)
    {
        // 1. Check environment variable first
        var envToken = Environment.GetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN");
        if (!string.IsNullOrEmpty(envToken))
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Using token from VSS_NUGET_ACCESSTOKEN");
            return envToken;
        }

        // 2. Try auth helpers (devcontainer scenario)
        var helperToken = await TryGetTokenFromAuthHelperAsync(cancellationToken);
        if (!string.IsNullOrEmpty(helperToken))
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Acquired token via auth helper");
            return helperToken;
        }

        // 3. Try Azure.Identity (DefaultAzureCredential)
        var identityToken = await TryGetTokenFromAzureIdentityAsync(cancellationToken);
        if (!string.IsNullOrEmpty(identityToken))
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Acquired token via Azure.Identity");
            return identityToken;
        }

        return null;
    }

    private static async Task<string?> TryGetTokenFromAuthHelperAsync(CancellationToken cancellationToken = default)
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
                    Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Trying auth helper: {helperPath} (attempt {attempt}/{maxRetries})");

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
                            Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Auth helper returned no token, retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Timeout - kill the process and retry
                        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Auth helper timed out: {helperPath}");
                        try { process.Kill(entireProcessTree: true); } catch { }

                        if (attempt < maxRetries)
                        {
                            Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Auth helper {helperPath} failed: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Retrying in {retryDelayMs}ms...");
                        await Task.Delay(retryDelayMs, cancellationToken);
                    }
                }
            }
        }

        return null;
    }

    private static async Task<string?> TryGetTokenFromAzureIdentityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Trying Azure.Identity DefaultAzureCredential...");

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = true, // Skip in dev scenarios for speed
                ExcludeWorkloadIdentityCredential = true,
            });

            var tokenRequest = new TokenRequestContext([AzureDevOpsScope]);

            // Use linked token with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            
            var token = await credential.GetTokenAsync(tokenRequest, timeoutCts.Token);

            return token.Token;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Azure.Identity timed out");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Azure.Identity failed: {ex.Message}");
            return null;
        }
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

        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] GetOperationClaims: '{packageSourceUri}'");

        // When package source is null/empty, NuGet is asking for source-agnostic operations
        // Return Authentication to indicate we handle auth
        // When package source is specified, only claim it if it's Azure DevOps
        var claims = new List<OperationClaim>();

        if (string.IsNullOrEmpty(packageSourceUri))
        {
            // Source-agnostic query - we support Authentication generally
            claims.Add(OperationClaim.Authentication);
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Claiming Authentication (source-agnostic)");
        }
        else if (Program.IsAzureDevOpsUri(packageSourceUri))
        {
            // Source-specific query - we handle Azure DevOps feeds
            claims.Add(OperationClaim.Authentication);
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Claiming Authentication for Azure DevOps");
        }
        else
        {
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Not claiming (not Azure DevOps)");
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

        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] GetAuthenticationCredentials: {uri}, IsRetry={payload?.IsRetry}");

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
            Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Failed to acquire credentials");

            var errorResponse = new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: "Failed to acquire Azure DevOps access token. Please run 'az login' or ensure auth helpers are available.",
                authenticationTypes: null,
                responseCode: MessageResponseCode.Error);

            await responseHandler.SendResponseAsync(request, errorResponse, cancellationToken);
            return;
        }

        Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Successfully acquired credentials");

        var successResponse = new GetAuthenticationCredentialsResponse(
            username: "AzureArtifacts",
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
        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] SetLogLevel: {payload?.LogLevel}");

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
        Console.Error.WriteLine($"[CredentialProvider.AzureArtifacts] Initialize: ClientVersion={payload?.ClientVersion}, Culture={payload?.Culture}");

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
        Console.Error.WriteLine("[CredentialProvider.AzureArtifacts] Close request received");

        // Acknowledge the close request
        // The plugin will exit after responding
        return Task.CompletedTask;
    }
}
