using System.Diagnostics;
using CredentialProvider.Devcontainer.Handlers;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Focused tests for Program.cs covering main code paths.
/// </summary>
[Collection("PluginConfig")]  // Ensure tests don't run in parallel (due to static state)
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
    public async Task TryGetAccessTokenAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        Assert.Null(token);
    }

    #endregion

    #region TryGetTokenFromAuthHelperAsync

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var token = await Program.TryGetTokenFromAuthHelperAsync(cts.Token);
        Assert.Null(token);
    }

    #endregion

    #region TryGetTokenFromAzureIdentityAsync

    [Fact]
    public async Task TryGetTokenFromAzureIdentityAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var token = await Program.TryGetTokenFromAzureIdentityAsync(cts.Token);
        Assert.Null(token);
    }

    #endregion

    #region Configuration

    [Fact]
    public async Task Main_Config_ReturnsZero()
    {
        Assert.Equal(0, await Program.Main(["--config"]));
    }

    [Fact]
    public void PluginConfig_DisabledEnvVar_SetsDisabled()
    {
        var originalValue = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED");
        try
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", "true");
            PluginConfig.Reload();

            Assert.True(PluginConfig.Instance.Disabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", originalValue);
            PluginConfig.Reload();
        }
    }

    [Fact]
    public void PluginConfig_UseAzureIdentityEnvVar_DisablesWhenFalse()
    {
        var originalValue = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY");
        try
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
            PluginConfig.Reload();

            Assert.False(PluginConfig.Instance.UseAzureIdentity);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", originalValue);
            PluginConfig.Reload();
        }
    }

    [Fact]
    public void PluginConfig_UseAzureIdentityEnvVar_DisablesWhenZero()
    {
        var originalValue = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY");
        try
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "0");
            PluginConfig.Reload();

            Assert.False(PluginConfig.Instance.UseAzureIdentity);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", originalValue);
            PluginConfig.Reload();
        }
    }

    #endregion
}
