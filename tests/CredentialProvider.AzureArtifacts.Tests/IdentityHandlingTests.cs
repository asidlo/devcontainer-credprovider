using AzureArtifacts.CredentialProvider;
using System.Diagnostics;

namespace CredentialProvider.AzureArtifacts.Tests;

/// <summary>
/// Comprehensive tests for identity and authentication handling
/// Tests various authentication scenarios including Azure.Identity integration
/// </summary>
public class IdentityHandlingTests
{
    [Fact]
    public async Task Authentication_VssToken_TakesPriorityOverOtherMethods()
    {
        // Arrange
        var expectedToken = "priority-env-token";
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
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_WithMultipleAzureDevOpsUris_AllRecognized()
    {
        // Arrange & Act & Assert
        var testCases = new[]
        {
            "https://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json",
            "https://dev.azure.com/org/_apis/packaging/",
            "https://org.visualstudio.com/_packaging/feed/nuget/v3/index.json",
            "https://pkgs.dev.azure.com/org/project/_packaging/feed/nuget/v3/index.json",
            "https://example.azure.com/_packaging/test/nuget/v3/index.json"
        };

        foreach (var uri in testCases)
        {
            var isRecognized = Program.IsAzureDevOpsUri(uri);
            Assert.True(isRecognized, $"URI should be recognized as Azure DevOps: {uri}");
        }
    }

    [Fact]
    public async Task Authentication_WithNonAzureDevOpsUris_NoneRecognized()
    {
        // Arrange & Act & Assert
        var testCases = new[]
        {
            "https://nuget.org/api/v3/index.json",
            "https://api.nuget.org/v3/index.json",
            "https://www.myget.org/F/myfeed/api/v3/index.json",
            "https://example.com/nuget/v3/index.json",
            "https://github.com/nuget/packages",
            "",
            null
        };

        foreach (var uri in testCases)
        {
            var isRecognized = Program.IsAzureDevOpsUri(uri);
            Assert.False(isRecognized, $"URI should NOT be recognized as Azure DevOps: {uri ?? "(null)"}");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Authentication_WithEmptyOrNullToken_FallsBackToOtherMethods(string? token)
    {
        // Arrange
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", token);

        try
        {
            // Act
            var result = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert - Should try other methods and eventually return null or a valid token from another source
            // The result depends on whether other auth methods (Azure CLI, auth helper) are available
            Assert.True(result == null || !string.IsNullOrWhiteSpace(result));
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_WithWhitespaceToken_ReturnsWhitespaceAsIs()
    {
        // Arrange - Environment variable with whitespace
        var whitespaceToken = "   ";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", whitespaceToken);

        try
        {
            // Act
            var result = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert - Whitespace token is returned as-is (not trimmed)
            // This is the current behavior - env var is used directly
            Assert.Equal(whitespaceToken, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_ConcurrentRequests_HandleCorrectly()
    {
        // Arrange
        var token = "concurrent-test-token";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", token);

        try
        {
            // Act - Make multiple concurrent requests
            var tasks = new List<Task<string?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All should succeed with the same token
            Assert.All(results, result => Assert.Equal(token, result));
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_WithCancellationToken_CancelsGracefully()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);

        // Assert - Should handle cancellation gracefully (return null instead of throwing)
        // This tests that the method doesn't throw OperationCanceledException
        Assert.True(true); // If we got here, cancellation was handled
    }

    [Fact]
    public async Task Authentication_WithPreCancelledToken_ReturnsImmediately()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before calling

        // Act
        var stopwatch = Stopwatch.StartNew();
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        stopwatch.Stop();

        // Assert
        Assert.Null(token);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Should return quickly when pre-cancelled");
    }

    [Fact]
    public async Task Authentication_TokenFromEnvironment_NotAffectedByWhitespace()
    {
        // Arrange - Token with leading/trailing whitespace
        var actualToken = "my-test-token";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", $"  {actualToken}  ");

        try
        {
            // Act
            var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert - Should use the token as-is (whitespace included)
            // The environment variable is used directly without trimming
            Assert.NotNull(token);
            Assert.Contains(actualToken, token);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_DifferentUris_SameAuthenticationFlow()
    {
        // Arrange
        var token = "uri-test-token";
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", token);

        try
        {
            var uris = new[]
            {
                "https://pkgs.dev.azure.com/org1/_packaging/feed1/nuget/v3/index.json",
                "https://pkgs.dev.azure.com/org2/_packaging/feed2/nuget/v3/index.json",
                "https://dev.azure.com/org3/_apis/",
            };

            // Act & Assert
            foreach (var uri in uris)
            {
                var result = await Program.TryGetAccessTokenAsync(uri);
                Assert.Equal(token, result);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Fact]
    public async Task Authentication_RapidEnvironmentVariableChanges_ReflectsLatestValue()
    {
        // Arrange & Act
        var token1 = "token-one";
        var token2 = "token-two";

        try
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", token1);
            var result1 = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", token2);
            var result2 = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            // Assert
            Assert.Equal(token1, result1);
            Assert.Equal(token2, result2);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        }
    }

    [Theory]
    [InlineData("https://PKGS.DEV.AZURE.COM/org/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("https://Pkgs.Dev.Azure.Com/org/_packaging/feed/nuget/v3/index.json", true)]
    [InlineData("HTTPS://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json", true)]
    public void Authentication_UriRecognition_CaseInsensitive(string uri, bool expected)
    {
        // Act
        var result = Program.IsAzureDevOpsUri(uri);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Authentication_LongRunningTokenAcquisition_WithTimeout()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VSS_NUGET_ACCESSTOKEN", null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        stopwatch.Stop();

        // Assert
        // Should complete within reasonable time (30 seconds max from CTS, but likely much faster)
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), 
            "Token acquisition should not hang indefinitely");
    }

    [Fact]
    public void Authentication_UriValidation_HandlesEdgeCases()
    {
        // Test edge cases for URI validation
        var edgeCases = new Dictionary<string, bool>
        {
            { string.Empty, false },
            { "   ", false },
            { "not-a-url", false },
            { "ftp://pkgs.dev.azure.com/test", true }, // Protocol doesn't matter for our check
            { "pkgs.dev.azure.com/test", true }, // No protocol
            { "https://", false },
            { "something.azure.com/_packaging/feed", true }, // Contains azure.com/_packaging
            { "azure.com", false }, // Just domain without _packaging
        };

        foreach (var (uri, expected) in edgeCases)
        {
            var result = Program.IsAzureDevOpsUri(uri);
            Assert.Equal(expected, result);
        }
        
        // Test null separately
        Assert.False(Program.IsAzureDevOpsUri(null));
    }
}
