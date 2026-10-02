#region

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Cryptography;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

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
///     InvitationManager コンポーネントの表示・判定・乱数生成・招待操作ロジックを担当する ViewModel。
/// </summary>
public sealed class InvitationManagerViewModel
{
    private const string CodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly IAdminDataProvider _adminData;
    private readonly TimeProvider _timeProvider;

    public InvitationManagerViewModel(IAdminDataProvider adminData, TimeProvider? timeProvider = null)
    {
        _adminData = adminData ?? throw new ArgumentNullException(nameof(adminData));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string NewEmail { get; set; } = string.Empty;
    public string? CurrentUserId { get; private set; }
    public IReadOnlyList<Invitation> Invitations { get; private set; } = [];
    public bool IsProcessing { get; private set; }

    public void Initialize(ClaimsPrincipal? user)
    {
        CurrentUserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    ///     招待一覧を読み込みます。
    /// </summary>
    public async Task LoadInvitationsAsync()
    {
        IsProcessing = true;
        try
        {
            Invitations = await _adminData.GetInvitationsAsync();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     新規招待を生成・登録します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "招待作成結果をResult型で返却するため")]
    public async Task<Result<Invitation>> CreateInvitationAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEmail))
        {
            return new Failure("Email is required.");
        }

        IsProcessing = true;
        try
        {
            var code = GenerateRandomCode(16);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var invitation = new Invitation
            {
                Email = NewEmail,
                InvitationCode = code,
                ExpirationDate = now.AddDays(7),
                IsUsed = false,
                InvitedByAdminId = CurrentUserId ?? string.Empty,
                OwnerId = CurrentUserId ?? "system"
            };

            await _adminData.CreateInvitationAsync(invitation);
            NewEmail = string.Empty;
            await LoadInvitationsAsync();
            return new Success<Invitation>(invitation);
        }
        catch (Exception ex)
        {
            return new Failure($"招待の作成に失敗しました: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     指定した招待を削除します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "招待削除結果をResult型で返却するため")]
    public async Task<Result<bool>> DeleteInvitationAsync(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        IsProcessing = true;
        try
        {
            await _adminData.DeleteInvitationAsync(invitation);
            await LoadInvitationsAsync();
            return new Success<bool>(true);
        }
        catch (Exception ex)
        {
            return new Failure($"招待の削除に失敗しました: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     招待コードからログイン・登録用リンクを生成します。
    /// </summary>
    public static string BuildInviteLink(string code) => $"/Account/Login?inviteCode={code}";

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