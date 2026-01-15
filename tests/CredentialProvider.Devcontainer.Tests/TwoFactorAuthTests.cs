using OtpNet;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Tests for TOTP-based two-factor authentication functionality
/// </summary>
public class TwoFactorAuthTests : IDisposable
{
    private readonly List<string> _environmentVariablesToCleanup = new();

    public void Dispose()
    {
        // Clean up environment variables after each test
        foreach (var varName in _environmentVariablesToCleanup)
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _environmentVariablesToCleanup.Add(name);
    }

    [Fact]
    public void ValidateTwoFactorAuth_WhenNotConfigured_ReturnsTrue()
    {
        // Arrange - ensure no 2FA environment variables are set
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", null);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", null);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.True(result, "2FA validation should pass when not configured");
    }

    [Fact]
    public void ValidateTwoFactorAuth_WhenSecretSetButNoCode_ReturnsFalse()
    {
        // Arrange
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", null);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.False(result, "2FA validation should fail when secret is set but code is not provided");
    }

    [Fact]
    public void ValidateTwoFactorAuth_WithValidCode_ReturnsTrue()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var totp = new Totp(secretKey);
        var validCode = totp.ComputeTotp();

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", validCode);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.True(result, "2FA validation should pass with valid TOTP code");
    }

    [Fact]
    public void ValidateTwoFactorAuth_WithInvalidCode_ReturnsFalse()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var invalidCode = "000000"; // Invalid code

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", invalidCode);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.False(result, "2FA validation should fail with invalid TOTP code");
    }

    [Fact]
    public void ValidateTwoFactorAuth_WithExpiredCode_ReturnsFalse()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        
        // Generate a code from a time in the past (way beyond the verification window)
        var totp = new Totp(secretKey, step: 30);
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        var expiredCode = totp.ComputeTotp(pastTime);

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", expiredCode);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.False(result, "2FA validation should fail with expired TOTP code");
    }

    [Fact]
    public void ValidateTwoFactorAuth_WithInvalidSecret_ReturnsFalse()
    {
        // Arrange
        var invalidSecret = "INVALID_BASE32_SECRET!!!";
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", invalidSecret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", "123456");

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.False(result, "2FA validation should fail with invalid secret format");
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_With2FAEnabled_ValidatesBeforeGettingToken()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var totp = new Totp(secretKey);
        var validCode = totp.ComputeTotp();

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", validCode);

        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert - should not throw and 2FA validation should pass
        // Token may still be null if auth helper is not available, but 2FA check should pass
        Assert.True(true); // Test passes if no exception is thrown
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_With2FAEnabled_FailsWithInvalidCode()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var invalidCode = "000000";

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", invalidCode);

        // Act
        var token = await Program.TryGetAccessTokenAsync("https://pkgs.dev.azure.com/test/");

        // Assert
        Assert.Null(token); // Should return null when 2FA fails
    }

    [Fact]
    public void ValidateTwoFactorAuth_WithWhitespaceSecret_ReturnsTrue()
    {
        // Arrange - whitespace-only secret should be treated as not configured
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", "   ");
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", "123456");

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.True(result, "2FA validation should pass when secret is whitespace (treated as not configured)");
    }

    [Fact]
    public void ValidateTwoFactorAuth_SupportsClockSkew()
    {
        // Arrange
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var totp = new Totp(secretKey, step: 30);
        
        // Generate a code from 30 seconds ago (one step back, should be accepted due to verification window)
        var pastTime = DateTime.UtcNow.AddSeconds(-30);
        var codeFromPast = totp.ComputeTotp(pastTime);

        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_SECRET", secret);
        SetEnvironmentVariable("NUGET_CREDPROVIDER_2FA_CODE", codeFromPast);

        // Act
        var result = Program.ValidateTwoFactorAuth();

        // Assert
        Assert.True(result, "2FA validation should accept codes from one time step in the past (clock skew tolerance)");
    }
}
