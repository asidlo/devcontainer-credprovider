using System.Text.Json;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Comprehensive tests for PluginConfig including config file loading and error handling.
/// </summary>
[Collection("PluginConfig")]  // Ensure tests don't run in parallel (due to static state)
public class PluginConfigTests : IDisposable
{
    private readonly string _originalDisabled;
    private readonly string _originalVerbosity;
    private readonly string? _originalConfigFileContent;
    private readonly bool _configFileExisted;

    public PluginConfigTests()
    {
        // Save original environment variables
        _originalDisabled = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED") ?? "";
        _originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY") ?? "";
        
        // Save original config file if it exists
        _configFileExisted = File.Exists(PluginConfig.ConfigFilePath);
        if (_configFileExisted)
        {
            _originalConfigFileContent = File.ReadAllText(PluginConfig.ConfigFilePath);
        }
        
        // Clear environment variables for tests
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", null);
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", null);
    }

    public void Dispose()
    {
        // Restore environment variables
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", 
            string.IsNullOrEmpty(_originalDisabled) ? null : _originalDisabled);
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", 
            string.IsNullOrEmpty(_originalVerbosity) ? null : _originalVerbosity);
        
        // Restore config file
        if (_configFileExisted && _originalConfigFileContent != null)
        {
            File.WriteAllText(PluginConfig.ConfigFilePath, _originalConfigFileContent);
        }
        else if (File.Exists(PluginConfig.ConfigFilePath))
        {
            File.Delete(PluginConfig.ConfigFilePath);
        }
        
        // Reload to restore original state
        PluginConfig.Reload();
    }

    #region Config File Tests

    [Fact]
    public void Load_WithValidConfigFile_LoadsSettings()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        var configContent = @"{""disabled"": true, ""verbosity"": ""debug""}";
        File.WriteAllText(PluginConfig.ConfigFilePath, configContent);

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert
        Assert.True(config.Disabled);
        Assert.Equal("debug", config.Verbosity);
        Assert.True(config.IsVerbose);
    }

    [Fact]
    public void Load_WithInvalidJsonInConfigFile_UsesDefaults()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        File.WriteAllText(PluginConfig.ConfigFilePath, "invalid json {{{");

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert - should use defaults, not throw
        Assert.False(config.Disabled);
        Assert.Equal("normal", config.Verbosity);
    }

    [Fact]
    public void Load_WithEmptyConfigFile_UsesDefaults()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        File.WriteAllText(PluginConfig.ConfigFilePath, "");

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert - should use defaults (empty file isn't valid JSON)
        Assert.False(config.Disabled);
        Assert.Equal("normal", config.Verbosity);
    }

    [Fact]
    public void Load_WithNullValuesInConfigFile_UsesDefaults()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        File.WriteAllText(PluginConfig.ConfigFilePath, @"{""disabled"": null, ""verbosity"": null}");

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert - null values should result in defaults
        Assert.False(config.Disabled);
        Assert.Equal("normal", config.Verbosity);
    }

    [Fact]
    public void Load_WithPartialConfigFile_UsesDefaultsForMissing()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        File.WriteAllText(PluginConfig.ConfigFilePath, @"{""disabled"": true}");

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert
        Assert.True(config.Disabled);
        Assert.Equal("normal", config.Verbosity); // Default since not specified
    }

    [Fact]
    public void Load_WithNoConfigFile_UsesDefaults()
    {
        // Arrange - ensure config file doesn't exist
        if (File.Exists(PluginConfig.ConfigFilePath))
        {
            File.Delete(PluginConfig.ConfigFilePath);
        }

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert
        Assert.False(config.Disabled);
        Assert.Equal("normal", config.Verbosity);
    }

    #endregion

    #region Environment Variable Override Tests

    [Fact]
    public void Load_EnvironmentVariablesOverrideConfigFile()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);
        
        // Config file sets disabled to false
        File.WriteAllText(PluginConfig.ConfigFilePath, @"{""disabled"": false, ""verbosity"": ""normal""}");
        
        // Environment variables override to true/debug
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", "true");
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", "debug");

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert - env vars should override
        Assert.True(config.Disabled);
        Assert.Equal("debug", config.Verbosity);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    [InlineData("yes", false)] // "yes" is not a recognized true value
    [InlineData("no", false)]
    [InlineData("anything", false)]
    public void Load_DisabledEnvVarParsing(string value, bool expected)
    {
        // Arrange
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED", value);

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert
        Assert.Equal(expected, config.Disabled);
    }

    [Theory]
    [InlineData("debug", "debug", true)]
    [InlineData("DEBUG", "debug", true)]
    [InlineData("Debug", "debug", true)]
    [InlineData("verbose", "verbose", true)]
    [InlineData("VERBOSE", "verbose", true)]
    [InlineData("Verbose", "verbose", true)]
    [InlineData("normal", "normal", false)]
    [InlineData("NORMAL", "normal", false)]
    [InlineData("info", "info", false)]
    [InlineData("warning", "warning", false)]
    [InlineData("error", "error", false)]
    public void Load_VerbosityEnvVarParsing(string value, string expectedVerbosity, bool expectedIsVerbose)
    {
        // Arrange
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", value);

        // Act
        PluginConfig.Reload();
        var config = PluginConfig.Instance;

        // Assert
        Assert.Equal(expectedVerbosity, config.Verbosity);
        Assert.Equal(expectedIsVerbose, config.IsVerbose);
    }

    #endregion

    #region Singleton Tests

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = PluginConfig.Instance;
        var instance2 = PluginConfig.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Reload_CreatesNewInstance()
    {
        // Act
        var instance1 = PluginConfig.Instance;
        PluginConfig.Reload();
        var instance2 = PluginConfig.Instance;

        // Assert - After reload, should be a different instance
        Assert.NotSame(instance1, instance2);
    }

    #endregion

    #region ConfigFilePath Tests

    [Fact]
    public void ConfigFilePath_ReturnsExpectedPath()
    {
        // Act
        var path = PluginConfig.ConfigFilePath;

        // Assert
        Assert.NotEmpty(path);
        Assert.Contains(".config", path);
        Assert.Contains("devcontainer-credprovider", path);
        Assert.EndsWith("config.json", path);
    }

    [Fact]
    public void ConfigFilePath_IsUnderUserProfile()
    {
        // Act
        var path = PluginConfig.ConfigFilePath;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Assert
        Assert.StartsWith(userProfile, path);
    }

    #endregion

    #region Property Default Tests

    [Fact]
    public void PluginConfig_NewInstance_HasCorrectDefaults()
    {
        // Act
        var config = new PluginConfig();

        // Assert
        Assert.False(config.Disabled);
        Assert.Equal("normal", config.Verbosity);
        Assert.False(config.IsVerbose);
    }

    [Fact]
    public void IsVerbose_TrueOnlyForDebugOrVerbose()
    {
        // Arrange & Act & Assert
        var config = new PluginConfig();

        config.Verbosity = "debug";
        Assert.True(config.IsVerbose);

        config.Verbosity = "verbose";
        Assert.True(config.IsVerbose);

        config.Verbosity = "normal";
        Assert.False(config.IsVerbose);

        config.Verbosity = "info";
        Assert.False(config.IsVerbose);

        config.Verbosity = "warning";
        Assert.False(config.IsVerbose);

        config.Verbosity = "error";
        Assert.False(config.IsVerbose);
    }

    #endregion
}
