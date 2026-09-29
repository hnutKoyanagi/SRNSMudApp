namespace SRNSMudApp.Tests.Components.Account;

using System.Text;

using SRNSMudApp.Components.Account.Pages.Manage;

/// <summary>
///     PasskeysViewModel の単体テスト。
///     ASP.NET Core のランタイムやブラウザ/E2Eなしで、パスキー管理画面の検証・整形・エンコードロジックを高速にテストする。
/// </summary>
public class PasskeysViewModelTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public void CanAddPasskey_ReturnsExpectedResultBasedOnMaxPasskeyCount(int count, bool expected)
    {
        var result = PasskeysViewModel.CanAddPasskey(count);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, PasskeysViewModel.DefaultPasskeyName)]
    [InlineData("", PasskeysViewModel.DefaultPasskeyName)]
    [InlineData("   ", PasskeysViewModel.DefaultPasskeyName)]
    [InlineData("My Security Key", "My Security Key")]
    public void FormatPasskeyName_ReturnsExpectedName(string? input, string expected)
    {
        var result = PasskeysViewModel.FormatPasskeyName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EncodeAndDecodeCredentialId_RoundTripsSuccessfully()
    {
        byte[] rawBytes = Encoding.UTF8.GetBytes("test-credential-id-12345");
        var encoded = PasskeysViewModel.EncodeCredentialId(rawBytes);

        var success = PasskeysViewModel.TryDecodeCredentialId(encoded, out byte[] decoded);

        Assert.True(success);
        Assert.Equal(rawBytes, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Invalid Base64 @@@!")]
    public void TryDecodeCredentialId_WithInvalidInput_ReturnsFalse(string? invalidInput)
    {
        var success = PasskeysViewModel.TryDecodeCredentialId(invalidInput, out byte[] decoded);

        Assert.False(success);
        Assert.Empty(decoded);
    }

    [Fact]
    public void FormatAttestationError_FormatsMessageWithFailureReason()
    {
        const string reason = "Device rejected ceremony";
        var message = PasskeysViewModel.FormatAttestationError(reason);

        Assert.Equal($"Error: Could not add the passkey: {reason}", message);
    }

    [Fact]
    public void FormatUnknownActionError_FormatsMessageWithActionName()
    {
        const string action = "invalid_action";
        var message = PasskeysViewModel.FormatUnknownActionError(action);

        Assert.Equal($"Error: Unknown action '{action}'.", message);
    }
}