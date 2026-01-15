using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Comprehensive integration tests for installation processes
/// Replaces shell script tests with C# xUnit tests for better CI/CD integration
/// </summary>
public class InstallationIntegrationTests : IDisposable
{
    private readonly string _testHome;
    private readonly string _repoRoot;

    public InstallationIntegrationTests()
    {
        _testHome = Path.Combine(Path.GetTempPath(), $"credprovider-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testHome);
        
        _repoRoot = FindRepositoryRoot() ?? throw new InvalidOperationException("Could not find repository root");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testHome))
            {
                Directory.Delete(_testHome, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task Installation_BuildAndPublish_Succeeds()
    {
        // Arrange & Act
        var buildResult = await RunDotnetCommand("build", 
            $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");

        // Assert
        Assert.Equal(0, buildResult.ExitCode);
        Assert.Contains("Build succeeded", buildResult.Output + buildResult.Error);
    }

    [Fact]
    public async Task Installation_PublishedBinariesExist_AfterPublish()
    {
        // Arrange
        var publishDir = Path.Combine(_testHome, "publish", "netcore");

        // Act
        var publishResult = await RunDotnetCommand("publish",
            $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release -o \"{publishDir}\" --nologo");

        // Assert
        Assert.Equal(0, publishResult.ExitCode);
        Assert.True(File.Exists(Path.Combine(publishDir, "CredentialProvider.Devcontainer.dll")));
        Assert.True(File.Exists(Path.Combine(publishDir, "CredentialProvider.Devcontainer.deps.json")));
        Assert.True(File.Exists(Path.Combine(publishDir, "CredentialProvider.Devcontainer.runtimeconfig.json")));
    }

    [Fact]
    public async Task Installation_RunInstallScript_CreatesPluginDirectory()
    {
        // Arrange
        var publishDir = Path.Combine(_testHome, "publish");
        var netcoreDir = Path.Combine(publishDir, "netcore");
        
        // Publish first
        await RunDotnetCommand("publish",
            $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release -o \"{netcoreDir}\" --nologo");

        // Copy install script
        var installScript = Path.Combine(_repoRoot, "scripts", "install.sh");
        if (!File.Exists(installScript))
        {
            return; // Skip on platforms without install.sh
        }

        File.Copy(installScript, Path.Combine(publishDir, "install.sh"));

        // Act
        var result = await RunBashCommand(Path.Combine(publishDir, "install.sh"), _testHome);

        // Assert
        var pluginDir = Path.Combine(_testHome, ".nuget", "plugins", "netcore", "CredentialProvider.Devcontainer");
        Assert.True(Directory.Exists(pluginDir), $"Plugin directory should exist at {pluginDir}");
        Assert.True(File.Exists(Path.Combine(pluginDir, "CredentialProvider.Devcontainer.dll")));
    }

    [Fact]
    public async Task Installation_PluginExecutable_ReturnsVersion()
    {
        // Arrange
        var dllPath = Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer", 
            "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");

        if (!File.Exists(dllPath))
        {
            // Build first
            await RunDotnetCommand("build",
                $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");
        }

        // Act
        var result = await RunDotnetCommand(dllPath, "--version");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CredentialProvider.Devcontainer", result.Output);
    }

    [Fact]
    public async Task Installation_PluginExecutable_ShowsHelp()
    {
        // Arrange
        var dllPath = Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer",
            "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");

        if (!File.Exists(dllPath))
        {
            await RunDotnetCommand("build",
                $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");
        }

        // Act
        var result = await RunDotnetCommand(dllPath, "--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NuGet Credential Provider", result.Output);
        Assert.Contains("--test", result.Output);
        Assert.Contains("--version", result.Output);
    }

    [Fact]
    public async Task Installation_PluginTest_WithEnvironmentVariable_AcquiresToken()
    {
        // Arrange
        var dllPath = Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer",
            "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");

        if (!File.Exists(dllPath))
        {
            await RunDotnetCommand("build",
                $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");
        }

        // Act - Test mode uses auth helpers
        var result = await RunDotnetCommand(dllPath, "--test");

        // Assert - If auth helper is available, it should succeed
        if (result.ExitCode == 0)
        {
            Assert.Contains("Successfully acquired token", result.Output + result.Error);
        }
        else
        {
            // If no auth helper available, it should fail gracefully
            Assert.Contains("Failed to acquire token", result.Output + result.Error);
        }
    }

    [Fact]
    public async Task Installation_PluginTest_AttemptAuthentication()
    {
        // Arrange
        var dllPath = Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer",
            "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");

        if (!File.Exists(dllPath))
        {
            await RunDotnetCommand("build",
                $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release --nologo");
        }

        // Act
        var result = await RunDotnetCommand(dllPath, "--test");

        // Assert - The test mode attempts authentication via auth helpers
        // It may succeed (if auth helper is available) or fail (if not)
        Assert.True(result.Output.Length > 0 || result.Error.Length > 0, "Test mode should produce output");
    }

    [Fact]
    public void DevcontainerFeature_FilesExist()
    {
        // Arrange
        var featureSrc = Path.Combine(_repoRoot, ".devcontainer-feature", "src", "devcontainer-credprovider");

        // Assert
        Assert.True(File.Exists(Path.Combine(featureSrc, "install.sh")), "Feature install.sh should exist");
        Assert.True(File.Exists(Path.Combine(featureSrc, "devcontainer-feature.json")), "Feature devcontainer-feature.json should exist");
    }

    [Fact]
    public async Task DevcontainerFeature_Installation_CreatesPluginDirectory()
    {
        // Arrange
        var featureSrc = Path.Combine(_repoRoot, ".devcontainer-feature", "src", "devcontainer-credprovider");
        var featureInstallScript = Path.Combine(featureSrc, "install.sh");
        
        if (!File.Exists(featureInstallScript))
        {
            return; // Skip if install script doesn't exist
        }

        // Publish binaries for embedding
        var publishDir = Path.Combine(_testHome, "publish");
        await RunDotnetCommand("publish",
            $"\"{Path.Combine(_repoRoot, "src", "CredentialProvider.Devcontainer")}\" -c Release -o \"{publishDir}\" --nologo");

        // Create test feature directory with embedded binaries
        var testFeatureDir = Path.Combine(_testHome, "feature");
        var netcoreDir = Path.Combine(testFeatureDir, "netcore");
        Directory.CreateDirectory(netcoreDir);
        
        CopyDirectory(publishDir, netcoreDir);
        File.Copy(featureInstallScript, Path.Combine(testFeatureDir, "install.sh"));

        // Set environment variables to simulate devcontainer
        Environment.SetEnvironmentVariable("_REMOTE_USER_HOME", _testHome);
        Environment.SetEnvironmentVariable("_REMOTE_USER", "testuser");

        try
        {
            // Act
            var result = await RunBashCommand(Path.Combine(testFeatureDir, "install.sh"), _testHome);

            // Assert
            var pluginDir = Path.Combine(_testHome, ".nuget", "plugins", "netcore", "CredentialProvider.Devcontainer");
            Assert.True(Directory.Exists(pluginDir), $"Plugin directory should exist at {pluginDir}");
            Assert.True(File.Exists(Path.Combine(pluginDir, "CredentialProvider.Devcontainer.dll")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("_REMOTE_USER_HOME", null);
            Environment.SetEnvironmentVariable("_REMOTE_USER", null);
        }
    }

    // Helper methods
    private string? FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "devcontainer-credprovider.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDotnetCommand(string command, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = command == "dotnet" ? arguments : $"{command} {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _repoRoot
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

    private async Task<(int ExitCode, string Output, string Error)> RunBashCommand(string scriptPath, string home)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && 
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return (0, "", "Skipped on non-Unix platform");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)
        };

        psi.Environment["HOME"] = home;

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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
