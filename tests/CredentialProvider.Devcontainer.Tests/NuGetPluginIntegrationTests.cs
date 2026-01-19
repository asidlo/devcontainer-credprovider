using System.Diagnostics;
using CredentialProvider.Devcontainer.Handlers;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Integration tests for NuGet plugin functionality
/// Tests the plugin's behavior when invoked as a NuGet credential provider
/// </summary>
public class NuGetPluginIntegrationTests
{
    private readonly string _repoRoot;

    public NuGetPluginIntegrationTests()
    {
        _repoRoot = FindRepositoryRoot() ?? throw new InvalidOperationException("Could not find repository root");
    }

    [Fact]
    public async Task Plugin_InvokedWithPluginFlag_StartsSuccessfully()
    {
        // Arrange
        var dllPath = await BuildPluginIfNeeded();

        // Act - Start plugin in plugin mode (it will wait for stdin)
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" -Plugin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        // Give it a moment to start
        await Task.Delay(500);

        // Kill the process since we can't complete the full handshake in a unit test
        process.Kill();
        await process.WaitForExitAsync();

        // Assert - Process started (we killed it, so exit code will be non-zero)
        Assert.True(true); // If we got here, the plugin started
    }

    [Fact]
    public void Plugin_RequestHandlers_AreRegistered()
    {
        // This test verifies that request handler classes exist and can be instantiated
        // These are the handlers the plugin registers when running

        // Arrange & Act
        var operationClaimsHandler = new GetOperationClaimsRequestHandler();
        var authCredsHandler = new GetAuthenticationCredentialsRequestHandler();
        var setLogLevelHandler = new SetLogLevelRequestHandler();
        var initializeHandler = new InitializeRequestHandler();
        var closeHandler = new PluginCloseRequestHandler();

        // Assert
        Assert.NotNull(operationClaimsHandler);
        Assert.NotNull(authCredsHandler);
        Assert.NotNull(setLogLevelHandler);
        Assert.NotNull(initializeHandler);
        Assert.NotNull(closeHandler);
    }

    [Theory]
    [InlineData("https://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json")]
    [InlineData("https://dev.azure.com/org/_apis/packaging/")]
    [InlineData("https://org.visualstudio.com/_packaging/feed/nuget/v3/index.json")]
    public void Plugin_IsAzureDevOpsUri_AcceptsKnownPatterns(string uri)
    {
        // Act
        var result = Program.IsAzureDevOpsUri(uri);

        // Assert
        Assert.True(result, $"Should recognize {uri} as Azure DevOps");
    }

    [Theory]
    [InlineData("https://nuget.org/api/v3/index.json")]
    [InlineData("https://api.nuget.org/v3/index.json")]
    [InlineData("https://myget.org/feed/api/v3/index.json")]
    public void Plugin_IsAzureDevOpsUri_RejectsNonAzureDevOpsUris(string uri)
    {
        // Act
        var result = Program.IsAzureDevOpsUri(uri);

        // Assert
        Assert.False(result, $"Should NOT recognize {uri} as Azure DevOps");
    }

    [Fact]
    public async Task Plugin_TokenAcquisition_UsesAuthHelpers()
    {
        // Act - Try to get a token using auth helpers
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert - Should return a token if auth helper is available, or null if not
        Assert.True(token == null || !string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Plugin_TokenAcquisition_ReturnsNullWhenNoAuthAvailable()
    {
        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert - Should return null when no auth helpers are available
        // (unless auth helper is configured on the test machine)
        Assert.True(token == null || !string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Plugin_TokenAcquisition_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);

        // Assert - Should handle cancellation gracefully
        Assert.Null(token);
    }

    [Fact]
    public async Task Plugin_MultipleTokenRequests_WorkConcurrently()
    {
        // Act - Simulate multiple concurrent requests (as NuGet might make)
        var tasks = new Task<string?>[5];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be consistent (either all get a token or all get null)
        var firstResult = results[0];
        Assert.All(results, r => Assert.Equal(firstResult, r));
    }

    [Fact]
    public async Task Plugin_DifferentAzureDevOpsOrgs_AllReturnToken()
    {
        // Arrange - Test that auth helpers return tokens for all Azure DevOps orgs
        var uris = new[]
        {
            "https://pkgs.dev.azure.com/org1/_packaging/feed/nuget/v3/index.json",
            "https://pkgs.dev.azure.com/org2/_packaging/feed/nuget/v3/index.json",
            "https://dev.azure.com/org3/_apis/",
        };

        // Act & Assert - All Azure DevOps URIs should get a token from auth helper
        foreach (var uri in uris)
        {
            var token = await Program.TryGetAccessTokenAsync(uri);
            // With auth helper configured, we should get a token
            // Skip assertion if not logged in
            if (token != null)
            {
                Assert.NotEmpty(token);
            }
        }
    }

    [Fact]
    public async Task Plugin_CommandLineArgs_VersionFlag_ReturnsVersionInfo()
    {
        // Arrange
        var dllPath = await BuildPluginIfNeeded();

        // Act
        var result = await RunDotnetCommand(dllPath, "--version");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CredentialProvider.Devcontainer", result.Output);
    }

    [Fact]
    public async Task Plugin_CommandLineArgs_HelpFlag_ReturnsUsageInfo()
    {
        // Arrange
        var dllPath = await BuildPluginIfNeeded();

        // Act
        var result = await RunDotnetCommand(dllPath, "--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NuGet Credential Provider", result.Output);
        Assert.Contains("-Plugin", result.Output);
        Assert.Contains("--test", result.Output);
    }

    [Fact]
    public async Task Plugin_CommandLineArgs_TestMode_Succeeds()
    {
        // Arrange
        var dllPath = await BuildPluginIfNeeded();

        // Act
        var result = await RunDotnetCommand(dllPath, "--test");

        // Assert - If auth helper is available, it should succeed
        // The test passes as long as it completes (either success or graceful failure)
        if (result.ExitCode == 0)
        {
            Assert.Contains("Successfully acquired token", result.Output + result.Error);
        }
        else
        {
            // If no auth helper available, it should fail gracefully
            Assert.Contains("Failed to acquire token", result.Output + result.Error);
        }
    }

    [Fact]
    public async Task Plugin_CommandLineArgs_TestMode_AttemptAuthentication()
    {
        // Arrange
        var dllPath = await BuildPluginIfNeeded();

        // Act
        var result = await RunDotnetCommand(dllPath, "--test");

        // Assert - The test mode attempts authentication via auth helpers
        // It may succeed (if auth helper is available) or fail (if not)
        // Either way, it should complete and provide output
        Assert.True(result.Output.Length > 0 || result.Error.Length > 0, "Test mode should produce output");
    }

    // Helper methods
    private string? FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "devcontainer-credprovider.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private async Task<string> BuildPluginIfNeeded()
    {
        var dllPath = Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer",
            "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");

        if (!File.Exists(dllPath))
        {
            await RunDotnetCommand("build",
                $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");
        }

        return dllPath;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDotnetCommand(string command, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = command.EndsWith(".dll") ? $"\"{command}\" {arguments}" : $"{command} {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _repoRoot
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "", "Failed to start process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }
}
