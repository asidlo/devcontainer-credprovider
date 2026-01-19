// NuGet Credential Provider Plugin for Devcontainers
// Configuration handling for the credential provider

using System.Text.Json;

namespace CredentialProvider.Devcontainer;

/// <summary>
/// Configuration for the credential provider, loaded from environment variables and config file.
/// </summary>
public class PluginConfig
{
    private static PluginConfig? _instance;
    private static readonly object _lock = new();

    public bool Disabled { get; set; }
    public string Verbosity { get; set; } = "normal";

    /// <summary>
    /// Config file path: ~/.config/devcontainer-credprovider/config.json
    /// </summary>
    public static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "devcontainer-credprovider", "config.json");

    /// <summary>
    /// Gets the singleton configuration instance, loading from env vars and config file.
    /// </summary>
    public static PluginConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Reloads the configuration (useful for testing).
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _instance = Load();
        }
    }

    internal static PluginConfig Load()
    {
        var config = new PluginConfig();

        // Load from config file first (if exists)
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var fileConfig = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (fileConfig != null)
                {
                    config = fileConfig;
                }
            }
        }
        catch
        {
            // Ignore config file errors, use defaults
        }

        // Environment variables override config file
        var disabledEnv = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED");
        if (!string.IsNullOrEmpty(disabledEnv))
        {
            config.Disabled = disabledEnv.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                              disabledEnv.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        var verbosityEnv = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY");
        if (!string.IsNullOrEmpty(verbosityEnv))
        {
            config.Verbosity = verbosityEnv.ToLowerInvariant();
        }

        return config;
    }

    public bool IsVerbose => Verbosity.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
                             Verbosity.Equals("verbose", StringComparison.OrdinalIgnoreCase);
}
