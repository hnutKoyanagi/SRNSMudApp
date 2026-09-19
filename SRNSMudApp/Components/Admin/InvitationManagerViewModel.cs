// Components/Admin/InvitationManagerViewModel.cs
#region

using System.Security.Cryptography;

using MudBlazor;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Components.Admin;

/// <summary>
///     招待状の表示状態。
/// </summary>
public enum InvitationStatus
{
    Pending,
    Used,
    Expired
}

/// <summary>
///     InvitationManager コンポーネントの表示・判定・乱数生成ロジックを担当する ViewModel。
/// </summary>
public static class InvitationManagerViewModel
{
    private const string CodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    ///     指定した文字数の暗号論的ランダムな招待コードを生成する。
    /// </summary>
    /// <param name="length">生成する文字列の長さ。</param>
    /// <returns>ランダムな文字列。</returns>
    public static string GenerateRandomCode(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        return RandomNumberGenerator.GetString(CodeCharacters, length);
    }

    /// <summary>
    ///     招待状の現在状態（使用済み、期限切れ、保留中）を判定する。
    /// </summary>
    /// <param name="invitation">対象の招待エンティティ。</param>
    /// <param name="utcNow">判定基準とする現在 UTC 日時。</param>
    /// <returns>招待状態 enum。</returns>
    public static InvitationStatus GetInvitationStatus(Invitation invitation, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.IsUsed)
        {
            return InvitationStatus.Used;
        }

        if (invitation.ExpirationDate < utcNow)
        {
            return InvitationStatus.Expired;
        }

        return InvitationStatus.Pending;
    }

    /// <summary>
    ///     招待状態に応じた表示テキストを取得する。
    /// </summary>
    public static string GetStatusText(InvitationStatus status) => status switch
    {
        InvitationStatus.Used => "Used",
        InvitationStatus.Expired => "Expired",
        InvitationStatus.Pending => "Pending",
        _ => "Unknown"
    };

    /// <summary>
    ///     招待状態に応じた MudBlazor Color を取得する。
    /// </summary>
    public static Color GetStatusColor(InvitationStatus status) => status switch
    {
        InvitationStatus.Used => Color.Success,
        InvitationStatus.Expired => Color.Error,
        InvitationStatus.Pending => Color.Warning,
        _ => Color.Default
    };
}