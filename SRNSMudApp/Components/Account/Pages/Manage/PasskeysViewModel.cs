namespace SRNSMudApp.Components.Account.Pages.Manage;

using System.Buffers.Text;

/// <summary>
///     Passkeys 管理画面（/Account/Manage/Passkeys）の表示・制約・ID変換ロジックを責務とする ViewModel。
///     UI レンダリングや ASP.NET Core ランタイムに依存せず、単体テスト可能。
/// </summary>
public static class PasskeysViewModel
{
    public const int MaxPasskeyCount = 100;

    public const string DefaultPasskeyName = "Unnamed passkey";
    public const string NoPasskeysMessage = "No passkeys are registered.";
    public const string MaxPasskeyCountMessage = "Error: You have reached the maximum number of allowed passkeys.";
    public const string MissingBrowserPasskeyMessage = "Error: The browser did not provide a passkey.";
    public const string InvalidCredentialIdFormatMessage = "Error: The specified passkey ID had an invalid format.";
    public const string PasskeyDeleteFailedMessage = "Error: The passkey could not be deleted.";
    public const string PasskeyDeleteSuccessMessage = "Passkey deleted successfully.";
    public const string PasskeyAddFailedMessage = "Error: The passkey could not be added to your account.";

    /// <summary>
    ///     パスキーが上限に達しておらず新規追加可能かを判定する。
    /// </summary>
    public static bool CanAddPasskey(int currentCount) => currentCount < MaxPasskeyCount;

    /// <summary>
    ///     パスキーの表示名を整形する。nullまたは空文字の場合はデフォルト表示名を返す。
    /// </summary>
    public static string FormatPasskeyName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? DefaultPasskeyName : name;

    /// <summary>
    ///     Credential ID のバイト配列を Base64Url 文字列にエンコードする。
    /// </summary>
    public static string EncodeCredentialId(byte[] credentialId) =>
        Base64Url.EncodeToString(credentialId);

    /// <summary>
    ///     Base64Url 形式の Credential ID 文字列をデコードする。
    /// </summary>
    public static bool TryDecodeCredentialId(string? encodedCredentialId, out byte[] credentialId)
    {
        credentialId = [];
        if (string.IsNullOrEmpty(encodedCredentialId))
        {
            return false;
        }

        try
        {
            credentialId = Base64Url.DecodeFromChars(encodedCredentialId);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Attestation 失敗時のエラーメッセージを生成する。
    /// </summary>
    public static string FormatAttestationError(string failureMessage) =>
        $"Error: Could not add the passkey: {failureMessage}";

    /// <summary>
    ///     未知のアクションが指定された場合のエラーメッセージを生成する。
    /// </summary>
    public static string FormatUnknownActionError(string? action) =>
        $"Error: Unknown action '{action}'.";
}