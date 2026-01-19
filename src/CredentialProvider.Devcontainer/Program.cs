// NuGet Credential Provider Plugin for Devcontainers
// Uses NuGet.Protocol library for proper plugin protocol handling
// See: https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin
//
// This plugin only uses auth helpers (ado-auth-helper, azure-auth-helper).
// If auth helpers are not available or fail, it returns NotApplicable to allow
// fallback to other credential providers like Microsoft's artifacts-credprovider.
//
// Configuration:
//   Environment variables:
//     DEVCONTAINER_CREDPROVIDER_DISABLED=true     - Disable plugin (for testing fallback)
//     DEVCONTAINER_CREDPROVIDER_VERBOSITY=debug   - Enable verbose logging (to stderr)
//   Config file (~/.config/devcontainer-credprovider/config.json):
//     { "disabled": true, "verbosity": "debug" }

using System.Diagnostics;
using System.Reflection;
using NuGet.Protocol.Plugins;
using CredentialProvider.Devcontainer.Handlers;

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

    /// <summary>
    /// Log a message to stderr. Only logs if verbose mode is enabled, unless alwaysLog is true.
    /// </summary>
    internal static void Log(string message, bool alwaysLog = false)
    {
        if (alwaysLog || PluginConfig.Instance.IsVerbose)
        {
            Console.Error.WriteLine($"[CredentialProvider.Devcontainer] {message}");
        }
    }

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
        var config = PluginConfig.Instance;

        Log($"Process started with args: [{string.Join(", ", args)}]");
        Log($"Process ID: {Environment.ProcessId}");
        Log($"Version: {GetVersion()}");
        Log($"Config: Disabled={config.Disabled}, Verbosity={config.Verbosity}");
        Log($"Config file path: {PluginConfig.ConfigFilePath}");
        
        // Handle -Plugin argument (required for NuGet to recognize this as a credential provider)
        if (args.Contains("-Plugin", StringComparer.OrdinalIgnoreCase))
        {
            // Check if plugin is disabled via config
            if (config.Disabled)
            {
                Log("Plugin is DISABLED via configuration, returning immediately to allow fallback", true);
                // Return success but don't start - NuGet will fallback to other providers
                // We need to still respond to handshake but return NotApplicable for all credential requests
                return await RunAsDisabledPluginAsync();
            }
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
            Console.WriteLine("  CredentialProvider.Devcontainer -Plugin     Run as NuGet plugin");
            Console.WriteLine("  CredentialProvider.Devcontainer --test      Test credential acquisition");
            Console.WriteLine("  CredentialProvider.Devcontainer --version   Show version info");
            Console.WriteLine("  CredentialProvider.Devcontainer --config    Show current configuration");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  Environment variables:");
            Console.WriteLine("    DEVCONTAINER_CREDPROVIDER_DISABLED=true|false         Disable plugin (for testing fallback)");
            Console.WriteLine("    DEVCONTAINER_CREDPROVIDER_VERBOSITY=debug/verbose|*   Set logging verbosity");
            Console.WriteLine();
            Console.WriteLine($"  Config file: {PluginConfig.ConfigFilePath}");
            Console.WriteLine("     Example: { \"disabled\": true, \"verbosity\": \"debug\" }");
            Console.WriteLine();
            return 0;
        }

        if (args.Contains("--config"))
        {
            Console.WriteLine($"CredentialProvider.Devcontainer v{GetVersion()} - Configuration");
            Console.WriteLine();
            Console.WriteLine($"Config file path: {PluginConfig.ConfigFilePath}");
            Console.WriteLine($"Config file exists: {File.Exists(PluginConfig.ConfigFilePath)}");
            Console.WriteLine();
            Console.WriteLine("Current settings:");
            Console.WriteLine($"  Disabled: {config.Disabled}");
            Console.WriteLine($"  Verbosity: {config.Verbosity}");
            Console.WriteLine();
            Console.WriteLine("Environment variables:");
            Console.WriteLine($"  DEVCONTAINER_CREDPROVIDER_DISABLED: {Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED") ?? "(not set)"}");
            Console.WriteLine($"  DEVCONTAINER_CREDPROVIDER_VERBOSITY: {Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY") ?? "(not set)"}");
            return 0;
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
        Log("Plugin starting...");

        try
        {
            // Create request handlers
            var requestHandlers = new RequestHandlers();

            // Add our credential handlers
            requestHandlers.TryAdd(MessageMethod.GetAuthenticationCredentials, new GetAuthenticationCredentialsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.GetOperationClaims, new GetOperationClaimsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.SetLogLevel, new SetLogLevelRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Initialize, new InitializeRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Close, new PluginCloseRequestHandler());

            // Use default connection options
            var options = ConnectionOptions.CreateDefault();

            Log("Initializing plugin connection...");

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

            Log($"Plugin connected, protocol version: {plugin.Connection.ProtocolVersion}");

            // Listen for connection faults (NuGet client disconnect)
            var connectionClosed = new TaskCompletionSource<bool>();
            plugin.Connection.Faulted += (sender, args) =>
            {
                connectionClosed.TrySetResult(true);
            };

            plugin.Closed += (sender, args) =>
            {
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

            return 0;
        }
        catch (Exception ex)
        {
            // Always log errors
            Log($"Plugin error: {ex}", alwaysLog: true);
            return 1;
        }
    }

    /// <summary>
    /// Runs the plugin in disabled mode - responds to protocol but returns NotApplicable for all credential requests.
    /// This allows testing fallback to other credential providers.
    /// </summary>
    private static async Task<int> RunAsDisabledPluginAsync()
    {
        // Always log disabled mode since user explicitly requested it
        Log("Running in DISABLED mode - all requests will return NotApplicable", alwaysLog: true);

        try
        {
            // Create request handlers that always return NotApplicable
            var requestHandlers = new RequestHandlers();

            // Use disabled handlers that return NotApplicable
            requestHandlers.TryAdd(MessageMethod.GetAuthenticationCredentials, new DisabledGetAuthenticationCredentialsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.GetOperationClaims, new DisabledGetOperationClaimsRequestHandler());
            requestHandlers.TryAdd(MessageMethod.SetLogLevel, new SetLogLevelRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Initialize, new InitializeRequestHandler());
            requestHandlers.TryAdd(MessageMethod.Close, new PluginCloseRequestHandler());

            var options = ConnectionOptions.CreateDefault();

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var plugin = await PluginFactory.CreateFromCurrentProcessAsync(
                requestHandlers,
                options,
                cts.Token);

            Log($"Disabled plugin connected, protocol version: {plugin.Connection.ProtocolVersion}");

            var connectionClosed = new TaskCompletionSource<bool>();
            plugin.Connection.Faulted += (sender, args) =>
            {
                connectionClosed.TrySetResult(true);
            };

            plugin.Closed += (sender, args) =>
            {
                connectionClosed.TrySetResult(true);
            };

            try
            {
                await Task.WhenAny(
                    connectionClosed.Task,
                    Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)
                );
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log($"Disabled plugin error: {ex}", alwaysLog: true);
            return 1;
        }
    }

    internal static async Task<string?> TryGetAccessTokenAsync(string packageSourceUri, CancellationToken cancellationToken = default)
    {
        // Try auth helpers (devcontainer scenario)
        // If auth helpers are not available or fail, return null to allow fallback
        // to other credential providers like Microsoft's artifacts-credprovider
        var helperToken = await TryGetTokenFromAuthHelperAsync(cancellationToken);
        if (!string.IsNullOrEmpty(helperToken))
        {
            Log("Acquired token via auth helper");
            return helperToken;
        }

        Log("Auth helper not available, returning NotApplicable to allow fallback to other providers");
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
                    Log($"Trying auth helper: {helperPath} (attempt {attempt}/{maxRetries})");

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
                            Log($"Auth helper returned no token, retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Timeout - kill the process and retry
                        Log($"Auth helper timed out: {helperPath}");
                        try { process.Kill(entireProcessTree: true); } catch { }

                        if (attempt < maxRetries)
                        {
                            Log($"Retrying in {retryDelayMs}ms...");
                            await Task.Delay(retryDelayMs, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Auth helper {helperPath} failed: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        Log($"Retrying in {retryDelayMs}ms...");
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
}
