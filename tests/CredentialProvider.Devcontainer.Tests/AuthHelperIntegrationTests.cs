using System.Diagnostics;
using CredentialProvider.Devcontainer.Handlers;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Tests for auth helper token acquisition, Azure Identity fallback, CLI arg paths,
/// and handler success paths. Uses temporary mock auth helper scripts to exercise
/// real code paths in TryGetTokenFromAuthHelperAsync.
/// </summary>
[Collection("PluginConfig")]
public class AuthHelperIntegrationTests : IDisposable
{
    private readonly string _originalDisabled;
    private readonly string _originalVerbosity;
    private readonly string _originalUseAzureIdentity;
    private readonly string _fakeHelperPath;
    private readonly bool _fakeHelperExistedBefore;

    public AuthHelperIntegrationTests()
    {
        _originalDisabled = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED") ?? "";
        _originalVerbosity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY") ?? "";
        _originalUseAzureIdentity = Environment.GetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY") ?? "";

        // Use the first auth helper path the plugin searches for
        _fakeHelperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ado-auth-helper");
        _fakeHelperExistedBefore = File.Exists(_fakeHelperPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_DISABLED",
            string.IsNullOrEmpty(_originalDisabled) ? null : _originalDisabled);
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY",
            string.IsNullOrEmpty(_originalVerbosity) ? null : _originalVerbosity);
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY",
            string.IsNullOrEmpty(_originalUseAzureIdentity) ? null : _originalUseAzureIdentity);
        PluginConfig.Reload();

        // Clean up the fake helper only if we created it
        if (!_fakeHelperExistedBefore && File.Exists(_fakeHelperPath))
        {
            try { File.Delete(_fakeHelperPath); } catch { }
        }
    }

    #region Auth Helper - Success Path

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_WithValidHelper_ReturnsToken()
    {
        // Skip if a real auth helper already exists (don't mess with it)
        if (_fakeHelperExistedBefore) return;

        // Arrange - Create a fake auth helper that echoes a token
        WriteFakeHelper("#!/bin/bash\necho test-token-abc123\n");

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync();

        // Assert
        Assert.Equal("test-token-abc123", token);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_HelperReturnsError_ReturnsNull()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Helper outputs "Error: ..." which should be treated as failure
        WriteFakeHelper("#!/bin/bash\necho 'Error: authentication failed'\n");

        // Disable Azure Identity to avoid long waits
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
        PluginConfig.Reload();

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync();

        // Assert - "Error" prefix output is rejected
        Assert.Null(token);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_HelperReturnsEmpty_ReturnsNull()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Helper outputs nothing (empty token)
        WriteFakeHelper("#!/bin/bash\necho ''\n");

        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
        PluginConfig.Reload();

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync();

        // Assert - Empty output is rejected
        Assert.Null(token);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_HelperExitsNonZero_RetriesAndReturnsNull()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Helper exits with error code
        WriteFakeHelper("#!/bin/bash\nexit 1\n");

        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
        PluginConfig.Reload();

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync();

        // Assert
        Assert.Null(token);
    }

    #endregion

    #region TryGetAccessTokenAsync - Azure Identity Paths

    [Fact]
    public async Task TryGetAccessTokenAsync_AuthHelperSucceeds_ReturnsHelperToken()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Auth helper succeeds
        WriteFakeHelper("#!/bin/bash\necho my-helper-token\n");

        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert - Should return the helper token
        Assert.Equal("my-helper-token", token);
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_WhenAzureIdentityDisabled_SkipsIdentityAndLogsMessage()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - No valid auth helper + Azure Identity disabled
        WriteFakeHelper("#!/bin/bash\nexit 1\n");
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_VERBOSITY", "debug");
        PluginConfig.Reload();

        var originalError = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);

        try
        {
            var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

            Assert.Null(token);
            Assert.Contains("Azure Identity is disabled", sw.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    #endregion

    #region Main CLI Paths

    [Fact]
    public async Task Main_WithShortVersionFlag_ReturnsVersionInfo()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.Main(["-v"]);
            Assert.Equal(0, result);
            Assert.Contains("CredentialProvider.Devcontainer", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Main_WithShortHelpFlag_ReturnsUsageInfo()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.Main(["-h"]);
            Assert.Equal(0, result);
            Assert.Contains("NuGet Credential Provider", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Main_WithHelpFlag_ShowsAzureIdentityConfig()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.Main(["--help"]);
            Assert.Equal(0, result);
            var output = sw.ToString();
            Assert.Contains("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", output);
            Assert.Contains("useAzureIdentity", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Main_WithConfigFlag_ShowsAllSettings()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.Main(["--config"]);
            Assert.Equal(0, result);
            var output = sw.ToString();
            Assert.Contains("UseAzureIdentity", output);
            Assert.Contains("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region TestCredentialsAsync Output

    [Fact]
    public async Task TestCredentialsAsync_WritesVersionAndStatus()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.TestCredentialsAsync();
            var output = sw.ToString();

            Assert.Contains("CredentialProvider.Devcontainer", output);
            Assert.Contains("Testing credential acquisition", output);

            if (result == 0)
                Assert.Contains("Successfully acquired token", output);
            else
                Assert.Contains("Failed to acquire token", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task TestCredentialsAsync_WithAuthHelper_ReturnsSuccess()
    {
        if (_fakeHelperExistedBefore) return;

        WriteFakeHelper("#!/bin/bash\necho test-cred-token\n");

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var result = await Program.TestCredentialsAsync();
            Assert.Equal(0, result);
            Assert.Contains("Successfully acquired token", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region Handler Success Path

    [Fact]
    public async Task GetAuthenticationCredentialsHandler_WhenTokenAvailable_ReturnsSuccessWithCredentials()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Create auth helper that returns a token
        WriteFakeHelper("#!/bin/bash\necho handler-test-token\n");

        var handler = new GetAuthenticationCredentialsRequestHandler();
        var mockConnection = new Mock<IConnection>();
        var mockResponseHandler = new Mock<IResponseHandler>();

        var payload = new JObject
        {
            ["Uri"] = "https://pkgs.dev.azure.com/org/_packaging/feed/nuget/v3/index.json",
            ["IsRetry"] = false,
            ["IsNonInteractive"] = true,
            ["CanShowDialog"] = false
        };
        var message = new Message(
            requestId: Guid.NewGuid().ToString(),
            type: MessageType.Request,
            method: MessageMethod.GetAuthenticationCredentials,
            payload: payload);

        GetAuthenticationCredentialsResponse? capturedResponse = null;
        mockResponseHandler
            .Setup(r => r.SendResponseAsync(It.IsAny<Message>(), It.IsAny<GetAuthenticationCredentialsResponse>(), It.IsAny<CancellationToken>()))
            .Callback<Message, GetAuthenticationCredentialsResponse, CancellationToken>((m, resp, ct) => capturedResponse = resp)
            .Returns(Task.CompletedTask);

        // Act
        await handler.HandleResponseAsync(mockConnection.Object, message, mockResponseHandler.Object, CancellationToken.None);

        // Assert - Should return success with token as password
        Assert.NotNull(capturedResponse);
        Assert.Equal(MessageResponseCode.Success, capturedResponse.ResponseCode);
        Assert.Equal("DevcontainerCredProvider", capturedResponse.Username);
        Assert.Equal("handler-test-token", capturedResponse.Password);
    }

    #endregion

    #region PluginConfig

    [Fact]
    public async Task PluginConfig_ConcurrentAccess_ReturnsSameInstance()
    {
        PluginConfig.Reload();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => PluginConfig.Instance))
            .ToArray();

        var instances = await Task.WhenAll(tasks);
        Assert.All(instances, instance => Assert.Same(instances[0], instance));
    }

    [Fact]
    public void PluginConfig_Load_WithUseAzureIdentityInConfigFile_LoadsSetting()
    {
        var configDir = Path.GetDirectoryName(PluginConfig.ConfigFilePath)!;
        Directory.CreateDirectory(configDir);

        string? originalContent = null;
        if (File.Exists(PluginConfig.ConfigFilePath))
            originalContent = File.ReadAllText(PluginConfig.ConfigFilePath);

        try
        {
            File.WriteAllText(PluginConfig.ConfigFilePath,
                @"{""disabled"": false, ""verbosity"": ""normal"", ""useAzureIdentity"": false}");
            Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", null);
            PluginConfig.Reload();

            Assert.False(PluginConfig.Instance.UseAzureIdentity);
        }
        finally
        {
            if (originalContent != null)
                File.WriteAllText(PluginConfig.ConfigFilePath, originalContent);
            else if (File.Exists(PluginConfig.ConfigFilePath))
                File.Delete(PluginConfig.ConfigFilePath);
            PluginConfig.Reload();
        }
    }

    #endregion

    #region Plugin Process Integration

    [Fact]
    public async Task Main_WithPluginFlag_WhenDisabled_EntersDisabledMode()
    {
        var dllPath = GetPluginDllPath();
        if (!File.Exists(dllPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" -Plugin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["DEVCONTAINER_CREDPROVIDER_DISABLED"] = "true";

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        await Task.Delay(500);
        process.Kill();
        await process.WaitForExitAsync();

        var stderr = await process.StandardError.ReadToEndAsync();
        Assert.Contains("DISABLED", stderr);
    }

    [Fact]
    public async Task Main_WithPluginFlag_WhenEnabled_StartsPluginMode()
    {
        var dllPath = GetPluginDllPath();
        if (!File.Exists(dllPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" -Plugin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        await Task.Delay(500);
        process.Kill();
        await process.WaitForExitAsync();

        // Verify the process reached the plugin startup path (not the "no args" error)
        var stderr = await process.StandardError.ReadToEndAsync();
        Assert.DoesNotContain("Use -Plugin to run as NuGet credential provider", stderr);
    }

    #endregion

    #region RunAsPlugin Error Handling

    [Fact]
    public async Task RunAsPluginAsync_WithoutNuGetClient_ReturnsError()
    {
        // When RunAsPluginAsync is called without a NuGet client connection on stdin/stdout,
        // it should catch the error and return 1
        var result = await Program.RunAsPluginAsync();

        // Without a proper NuGet client, plugin creation will fail
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RunAsDisabledPluginAsync_WithoutNuGetClient_ReturnsError()
    {
        // When RunAsDisabledPluginAsync is called without a NuGet client connection,
        // it should catch the error and return 1
        var result = await Program.RunAsDisabledPluginAsync();

        Assert.Equal(1, result);
    }

    #endregion

    #region Auth Helper Timeout/Process Error Paths

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_HelperTimesOut_ReturnsNull()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Helper that hangs for 30 seconds (longer than the 10s timeout)
        WriteFakeHelper("#!/bin/bash\nsleep 30\n");

        Environment.SetEnvironmentVariable("DEVCONTAINER_CREDPROVIDER_USE_AZURE_IDENTITY", "false");
        PluginConfig.Reload();

        // Use a timeout CTS so the test doesn't hang
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync(cts.Token);

        // Assert - Should return null after timeout + retries or cancellation
        Assert.Null(token);
    }

    [Fact]
    public async Task TryGetTokenFromAuthHelperAsync_CancelledDuringExecution_ReturnsNull()
    {
        if (_fakeHelperExistedBefore) return;

        // Arrange - Helper that takes some time
        WriteFakeHelper("#!/bin/bash\nsleep 5\necho token\n");

        // Cancel quickly
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        var token = await Program.TryGetTokenFromAuthHelperAsync(cts.Token);

        // Assert
        Assert.Null(token);
    }

    #endregion

    #region Helper Methods

    private void WriteFakeHelper(string content)
    {
        File.WriteAllText(_fakeHelperPath, content);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(_fakeHelperPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string GetPluginDllPath()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "devcontainer-credprovider.sln")))
            {
                var debugPath = Path.Combine(current, "src", "CredentialProvider.Devcontainer",
                    "bin", "Debug", "net8.0", "CredentialProvider.Devcontainer.dll");
                if (File.Exists(debugPath)) return debugPath;

                return Path.Combine(current, "src", "CredentialProvider.Devcontainer",
                    "bin", "Release", "net8.0", "CredentialProvider.Devcontainer.dll");
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return "";
    }

    #endregion
}
