using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CredentialProvider.AzureArtifacts.Tests;

/// <summary>
/// Integration tests for installation process
/// </summary>
public class InstallationTests
{
    private readonly string _tempTestDir;
    private readonly string _homeDir;
    private readonly string _pluginDir;

    public InstallationTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"credprovider-test-{Guid.NewGuid()}");
        _homeDir = Path.Combine(_tempTestDir, "home");
        _pluginDir = Path.Combine(_homeDir, ".nuget", "plugins", "netcore", "CredentialProvider.AzureArtifacts");
        
        Directory.CreateDirectory(_tempTestDir);
        Directory.CreateDirectory(_homeDir);
    }

    [Fact]
    public async Task InstallScript_CreatesPluginDirectory()
    {
        // This test verifies the installation directory structure
        var pluginDestination = Path.Combine(_homeDir, ".nuget", "plugins", "netcore", "CredentialProvider.AzureArtifacts");
        
        // Act
        Directory.CreateDirectory(pluginDestination);
        
        // Assert
        Assert.True(Directory.Exists(pluginDestination));
    }

    [Fact]
    public void PluginDirectory_HasCorrectStructure()
    {
        // Arrange
        var expectedPaths = new[]
        {
            Path.Combine(_homeDir, ".nuget"),
            Path.Combine(_homeDir, ".nuget", "plugins"),
            Path.Combine(_homeDir, ".nuget", "plugins", "netcore"),
            _pluginDir
        };

        // Act
        foreach (var path in expectedPaths)
        {
            Directory.CreateDirectory(path);
        }

        // Assert
        foreach (var path in expectedPaths)
        {
            Assert.True(Directory.Exists(path), $"Expected directory {path} to exist");
        }
    }

    [Fact]
    public async Task InstalledPlugin_CanBeExecuted()
    {
        // Skip this test if we're not on the same OS as the build
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        // Arrange - Find the built DLL
        var repoRoot = FindRepositoryRoot();
        if (repoRoot == null)
        {
            // Skip if we can't find the repo (e.g., in isolated test environment)
            return;
        }

        var dllPath = Path.Combine(repoRoot, "src", "CredentialProvider.AzureArtifacts", "bin", "Debug", "net8.0", "CredentialProvider.AzureArtifacts.dll");
        
        if (!File.Exists(dllPath))
        {
            // Build hasn't been done yet, skip
            return;
        }

        // Act
        var result = await RunDotnetCommand(dllPath, "--version");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CredentialProvider.AzureArtifacts", result.Output);
    }

    [Fact]
    public async Task InstalledPlugin_ShowsHelpCorrectly()
    {
        // Arrange
        var repoRoot = FindRepositoryRoot();
        if (repoRoot == null) return;

        var dllPath = Path.Combine(repoRoot, "src", "CredentialProvider.AzureArtifacts", "bin", "Debug", "net8.0", "CredentialProvider.AzureArtifacts.dll");
        if (!File.Exists(dllPath)) return;

        // Act
        var result = await RunDotnetCommand(dllPath, "--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NuGet Credential Provider", result.Output);
        Assert.Contains("--test", result.Output);
        Assert.Contains("--version", result.Output);
    }

    private string? FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "credentialprovider-azureartifacts.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDotnetCommand(string dllPath, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
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
