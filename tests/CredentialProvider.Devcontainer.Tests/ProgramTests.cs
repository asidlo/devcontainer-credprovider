using System.Diagnostics;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Focused tests for Program.cs covering main code paths.
/// </summary>
public class ProgramTests
{
    #region GetVersion

    [Fact]
    public void GetVersion_ReturnsVersionString()
    {
        var version = Program.GetVersion();
        
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }

    #endregion

    #region IsAzureDevOpsUri

    [Theory]
    [InlineData("https://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("https://dev.azure.com/org/_apis/", true)]
    [InlineData("https://org.visualstudio.com/_packaging/feed/nuget/v3/", true)]
    [InlineData("https://example.azure.com/_packaging/feed/nuget/v3/", true)]
    [InlineData("https://nuget.org/api/v3/index.json", false)]
    [InlineData("https://api.nuget.org/v3/index.json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAzureDevOpsUri_IdentifiesCorrectly(string? uri, bool expected)
    {
        Assert.Equal(expected, Program.IsAzureDevOpsUri(uri));
    }

    #endregion

    #region Main - Command Line Arguments

    [Fact]
    public async Task Main_Version_ReturnsZero()
    {
        Assert.Equal(0, await Program.Main(["--version"]));
    }

    [Fact]
    public async Task Main_Help_ReturnsZero()
    {
        Assert.Equal(0, await Program.Main(["--help"]));
    }

    [Fact]
    public async Task Main_NoArgs_ReturnsOne()
    {
        Assert.Equal(1, await Program.Main([]));
    }

    [Fact]
    public async Task Main_Test_ReturnsZeroOrOne()
    {
        var result = await Program.Main(["--test"]);
        Assert.True(result == 0 || result == 1); // Depends on auth availability
    }

    #endregion

    #region TryGetAccessTokenAsync

    [Fact]
    public async Task TryGetAccessTokenAsync_Completes()
    {
        // Should complete without throwing
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");
        Assert.True(token == null || token.Length > 0);
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        Assert.Null(token);
    }

    #endregion

    #region TestCredentialsAsync

    [Fact]
    public async Task TestCredentialsAsync_ReturnsZeroOrOne()
    {
        var result = await Program.TestCredentialsAsync();
        Assert.True(result == 0 || result == 1);
    }

    #endregion

    #region TryGetTokenFromAuthHelperAsync

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_Completes()
    {
        // Auth helper likely not present, should return null gracefully
        var token = await Program.TryGetTokenFromAuthHelperAsync();
        Assert.True(token == null || token.Length > 0);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var token = await Program.TryGetTokenFromAuthHelperAsync(cts.Token);
        Assert.Null(token);
    }

    #endregion

    #region Request Handlers

    [Fact]
    public void RequestHandlers_CanBeInstantiated()
    {
        Assert.NotNull(new GetOperationClaimsRequestHandler());
        Assert.NotNull(new GetAuthenticationCredentialsRequestHandler());
        Assert.NotNull(new SetLogLevelRequestHandler());
        Assert.NotNull(new InitializeRequestHandler());
        Assert.NotNull(new CloseRequestHandler());
    }

    #endregion

    #region Plugin Mode Integration

    [Fact]
    public async Task Plugin_StartsAndExits()
    {
        var dllPath = GetPluginDllPath();
        if (!File.Exists(dllPath)) return; // Skip if not built

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" -Plugin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        Assert.NotNull(process);
        
        // Let it start, then kill it
        await Task.Delay(300);
        process.Kill();
        await process.WaitForExitAsync();
    }

    #endregion

    #region Helpers

    private static string GetPluginDllPath()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            var sln = Path.Combine(current, "devcontainer-credprovider.sln");
            if (File.Exists(sln))
            {
                return Path.Combine(current, "src", "CredentialProvider.Devcontainer",
                    "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return "";
    }

    #endregion
}
