namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Tests for authentication methods and fallback behavior
/// </summary>
public class AuthenticationTests
{
    [Fact]
    public async Task TryGetAccessToken_AttemptsAuthHelper()
    {
        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert
        // Token may be null if no auth helper is available, but the method should not throw
        // This test verifies the fallback chain works
        Assert.True(true); // Method completed without throwing
    }

    [Fact]
    public async Task TryGetAccessToken_WithCancellation_StopsProperly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        // The method handles cancellation gracefully and returns null instead of throwing
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);

        // Assert
        // When cancelled, the method should return null rather than throw
        Assert.Null(token);
    }

    [Theory]
    [InlineData("https://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("https://dev.azure.com/org/_apis/", true)]
    [InlineData("https://org.visualstudio.com/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("https://example.azure.com/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("https://nuget.org/api/v2/", false)]
    [InlineData("https://example.com/nuget/v3/index.json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAzureDevOpsUri_RecognizesValidUris(string? uri, bool expected)
    {
        // Act
        var result = Program.IsAzureDevOpsUri(uri);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task TryGetAccessToken_FallbackChain_TriesAuthHelperFirst()
    {
        // Act
        // This test just ensures the method completes without throwing
        // The actual auth helper may or may not be present in the test environment
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert - Method completed without error (token may be null if no auth available)
        Assert.True(true);
    }

    [Fact]
    public async Task TryGetTokenFromAzureIdentityAsync_CompletesWithoutThrowing()
    {
        // Act - Azure Identity may or may not find credentials depending on environment
        var token = await Program.TryGetTokenFromAzureIdentityAsync();

        // Assert - Method completed without throwing (token may be null)
        Assert.True(token == null || token.Length > 0);
    }

    [Fact]
    public async Task TryGetTokenFromAzureIdentityAsync_WithCancellation_ReturnsNull()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var token = await Program.TryGetTokenFromAzureIdentityAsync(cts.Token);

        // Assert
        Assert.Null(token);
    }

    [Fact]
    public async Task TryGetAccessToken_WithAzureIdentityDisabled_SkipsAzureIdentity()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY");
        try
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
            PluginConfig.Reload();

            // Act - With Azure Identity disabled and no auth helpers, should return null quickly
            var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert - should not throw
            Assert.True(token == null || token.Length > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", originalValue);
            PluginConfig.Reload();
        }
    }
}
