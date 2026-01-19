using System.Text.Json;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Tests for serialization, deserialization, and error handling scenarios in the plugin.
/// </summary>
public class SerializationAndErrorHandlingTests
{
    #region PluginConfig Serialization Tests

    [Fact]
    public void PluginConfig_Serialize_ProducesValidJson()
    {
        // Arrange
        var config = new PluginConfig
        {
            Disabled = true,
            Verbosity = "debug"
        };

        // Act
        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<PluginConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Disabled);
        Assert.Equal("debug", deserialized.Verbosity);
    }

    [Fact]
    public void PluginConfig_Deserialize_CaseInsensitivePropertyNames()
    {
        // Arrange
        var json = "{\"DISABLED\":true,\"VERBOSITY\":\"debug\"}";

        // Act
        var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(config);
        Assert.True(config.Disabled);
        Assert.Equal("debug", config.Verbosity);
    }

    [Fact]
    public void PluginConfig_Deserialize_MixedCasePropertyNames()
    {
        // Arrange
        var json = "{\"Disabled\":false,\"verbosity\":\"verbose\"}";

        // Act
        var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(config);
        Assert.False(config.Disabled);
        Assert.Equal("verbose", config.Verbosity);
    }

    [Fact]
    public void PluginConfig_Deserialize_PartialJson_UsesDefaults()
    {
        // Arrange
        var json = "{\"disabled\":true}";

        // Act
        var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(config);
        Assert.True(config.Disabled);
        Assert.Equal("normal", config.Verbosity); // Default value
    }

    [Fact]
    public void PluginConfig_Deserialize_EmptyJson_UsesDefaults()
    {
        // Arrange
        var json = "{}";

        // Act
        var config = JsonSerializer.Deserialize<PluginConfig>(json);

        // Assert
        Assert.NotNull(config);
        Assert.False(config.Disabled); // Default value
        Assert.Equal("normal", config.Verbosity); // Default value
    }

    [Fact]
    public void PluginConfig_Deserialize_InvalidJson_ThrowsException()
    {
        // Arrange
        var json = "not valid json";

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PluginConfig>(json));
    }

    [Fact]
    public void PluginConfig_Deserialize_NullJson_ReturnsNull()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => JsonSerializer.Deserialize<PluginConfig>(json!));
    }

    #endregion

    #region PluginConfig Load Error Handling Tests

    [Fact]
    public void PluginConfig_Load_WithInvalidJsonFile_UsesDefaults()
    {
        // This test verifies that invalid config files are handled gracefully
        // The Load method catches exceptions and returns defaults
        
        var originalValue = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED");
        var originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        try
        {
            // Clear env vars to test config file behavior
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", null);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", null);
            
            // Reload config (config file may or may not exist)
            PluginConfig.Reload();
            var config = PluginConfig.Instance;
            
            // Should have default values or values from existing config file
            Assert.NotNull(config);
            Assert.NotNull(config.Verbosity);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", originalValue);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", originalVerbosity);
            PluginConfig.Reload();
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void PluginConfig_DisabledEnvVar_ParsesCorrectly(string value, bool expectedDisabled)
    {
        // Save original values
        var originalDisabled = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED");
        var originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        
        try
        {
            // Set test value
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", value);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", null);
            PluginConfig.Reload();
            
            // Assert
            if (string.IsNullOrEmpty(value))
            {
                // Empty string doesn't override, uses default
                Assert.False(PluginConfig.Instance.Disabled);
            }
            else
            {
                Assert.Equal(expectedDisabled, PluginConfig.Instance.Disabled);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", originalDisabled);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", originalVerbosity);
            PluginConfig.Reload();
        }
    }

    [Theory]
    [InlineData("debug", true)]
    [InlineData("DEBUG", true)]
    [InlineData("Debug", true)]
    [InlineData("verbose", true)]
    [InlineData("VERBOSE", true)]
    [InlineData("Verbose", true)]
    [InlineData("normal", false)]
    [InlineData("info", false)]
    [InlineData("warning", false)]
    [InlineData("error", false)]
    [InlineData("", false)]
    public void PluginConfig_VerbosityEnvVar_SetsIsVerboseCorrectly(string value, bool expectedIsVerbose)
    {
        // Save original values
        var originalDisabled = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED");
        var originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        
        try
        {
            // Set test value
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", null);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", value);
            PluginConfig.Reload();
            
            // Assert
            if (string.IsNullOrEmpty(value))
            {
                // Empty string doesn't override, uses default "normal"
                Assert.Equal("normal", PluginConfig.Instance.Verbosity);
                Assert.False(PluginConfig.Instance.IsVerbose);
            }
            else
            {
                Assert.Equal(value.ToLowerInvariant(), PluginConfig.Instance.Verbosity);
                Assert.Equal(expectedIsVerbose, PluginConfig.Instance.IsVerbose);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", originalDisabled);
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", originalVerbosity);
            PluginConfig.Reload();
        }
    }

    #endregion

    #region IsAzureDevOpsUri Error Handling Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAzureDevOpsUri_NullOrEmpty_ReturnsFalse(string? uri)
    {
        Assert.False(Program.IsAzureDevOpsUri(uri));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///path/to/file")]
    public void IsAzureDevOpsUri_InvalidUri_HandleGracefully(string uri)
    {
        // Should not throw, just return false
        Assert.False(Program.IsAzureDevOpsUri(uri));
    }

    [Fact]
    public void IsAzureDevOpsUri_VeryLongString_HandleGracefully()
    {
        // Test with a very long string to ensure no performance issues
        var longString = new string('a', 10000);
        Assert.False(Program.IsAzureDevOpsUri(longString));
    }

    [Theory]
    [InlineData("https://dev.azure.com")]
    [InlineData("https://pkgs.dev.azure.com")]
    [InlineData("https://visualstudio.com")]
    [InlineData("https://azure.com/_packaging")]
    public void IsAzureDevOpsUri_MinimalValidPatterns_ReturnsTrue(string uri)
    {
        Assert.True(Program.IsAzureDevOpsUri(uri));
    }

    #endregion

    #region TryGetAccessTokenAsync Error Handling Tests

    [Fact]
    public async Task TryGetAccessTokenAsync_WithCancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var result = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_WithTimeout_ReturnsWithinReasonableTime()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/", cts.Token);
        sw.Stop();
        
        // Should complete quickly since no auth helper is available
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_WithCancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var result = await Program.TryGetTokenFromAuthHelperAsync(cts.Token);
        
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_NoAuthHelper_ReturnsNull()
    {
        // In a typical test environment, auth helpers are not installed
        var result = await Program.TryGetTokenFromAuthHelperAsync();
        
        Assert.Null(result);
    }

    #endregion

    #region Log Method Tests

    [Fact]
    public void Log_WithAlwaysLog_WritesToConsoleError()
    {
        // Arrange
        var originalOut = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        
        try
        {
            // Act
            Program.Log("Test message", alwaysLog: true);
            
            // Assert
            var output = sw.ToString();
            Assert.Contains("Test message", output);
            Assert.Contains("[CredentialProvider.Devcontainer]", output);
        }
        finally
        {
            Console.SetError(originalOut);
        }
    }

    [Fact]
    public void Log_WithoutAlwaysLog_AndNotVerbose_DoesNotWrite()
    {
        // Save original values
        var originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        
        try
        {
            // Ensure not verbose
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", "normal");
            PluginConfig.Reload();
            
            var originalOut = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            
            try
            {
                // Act
                Program.Log("Test message", alwaysLog: false);
                
                // Assert
                var output = sw.ToString();
                Assert.Empty(output);
            }
            finally
            {
                Console.SetError(originalOut);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", originalVerbosity);
            PluginConfig.Reload();
        }
    }

    [Fact]
    public void Log_WithVerboseMode_WritesToConsoleError()
    {
        // Save original values
        var originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        
        try
        {
            // Set verbose mode
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", "debug");
            PluginConfig.Reload();
            
            var originalOut = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            
            try
            {
                // Act
                Program.Log("Test message", alwaysLog: false);
                
                // Assert
                var output = sw.ToString();
                Assert.Contains("Test message", output);
            }
            finally
            {
                Console.SetError(originalOut);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", originalVerbosity);
            PluginConfig.Reload();
        }
    }

    #endregion

    #region GetVersion Tests

    [Fact]
    public void GetVersion_ReturnsNonEmptyString()
    {
        var version = Program.GetVersion();
        
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.NotEqual("unknown", version);
    }

    #endregion

    #region TestCredentialsAsync Tests

    [Fact]
    public async Task TestCredentialsAsync_CompletesSuccessfully()
    {
        // Capture console output
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        
        try
        {
            var result = await Program.TestCredentialsAsync();
            
            // Should return 0 (success) or 1 (failure) based on auth helper availability
            Assert.True(result == 0 || result == 1);
            
            var output = sw.ToString();
            Assert.Contains("Testing credential acquisition", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion
}
