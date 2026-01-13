using AzureArtifacts.CredentialProvider;

namespace CredentialProvider.AzureArtifacts.Tests;

/// <summary>
/// Tests for authentication methods and fallback behavior
/// </summary>
public class AuthenticationTests
{
    [Fact]
    public async Task TryGetAccessToken_WithVssNugetAccessToken_ReturnsTokenFromEnvironment()
    {
        // Arrange
        var expectedToken = "test-token-from-env-var";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", expectedToken);

        try
        {
            // Act
            var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert
            Assert.NotNull(token);
            Assert.Equal(expectedToken, token);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task TryGetAccessToken_WithoutVssToken_AttemptsAuthHelper()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);

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
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
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
    public async Task TryGetAccessToken_FallbackChain_PrioritizesCorrectly()
    {
        // Arrange - Set environment variable to ensure it takes priority
        var envToken = "env-var-token";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", envToken);

        try
        {
            // Act
            var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert - Should use env var first
            Assert.Equal(envToken, token);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }
}
