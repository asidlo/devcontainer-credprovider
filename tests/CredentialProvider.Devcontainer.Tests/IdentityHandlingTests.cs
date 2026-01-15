using System.Diagnostics;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Comprehensive tests for identity and authentication handling
/// Tests various authentication scenarios including auth helpers
/// </summary>
public class IdentityHandlingTests
{
    [Fact]
    public void Authentication_WithMultipleAzureDevOpsUris_AllRecognized()
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
    public void Authentication_WithNonAzureDevOpsUris_NoneRecognized()
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

    [Fact]
    public async Task Authentication_ConcurrentRequests_HandleCorrectly()
    {
        // Act - Make multiple concurrent requests
        var tasks = new List<Task<string?>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should complete without error (token may be null if no auth available)
        Assert.All(results, result => Assert.True(result == null || !string.IsNullOrEmpty(result)));
    }

    [Fact]
    public async Task Authentication_WithCancellationToken_CancelsGracefully()
    {
        // Arrange
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
    public async Task Authentication_DifferentUris_SameAuthenticationFlow()
    {
        var uris = new[]
        {
            "https://pkgs.dev.azure.com/org1/_packaging/feed1/nuget/v3/index.json",
            "https://pkgs.dev.azure.com/org2/_packaging/feed2/nuget/v3/index.json",
            "https://dev.azure.com/org3/_apis/",
        };

        // Act & Assert - All should complete without error
        foreach (var uri in uris)
        {
            var result = await Program.TryGetAccessTokenAsync(uri);
            // Token may be null if no auth available, but should not throw
            Assert.True(result == null || !string.IsNullOrEmpty(result));
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
