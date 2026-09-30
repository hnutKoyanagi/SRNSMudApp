using System.Diagnostics.CodeAnalysis;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     選択可能なリクエスト先ユーザーの表示用レコード。
/// </summary>
public sealed record RequestTagPermissionSelectableUser(string UserId, string DisplayName, int TotalAmount);

/// <summary>
///     タグ操作権限（RightAsset）リクエストダイアログの ViewModel。
///     候補ユーザーの抽出、対価アセットの管理、リクエスト送信処理を担当する。
/// </summary>
public class RequestTagPermissionViewModel
{
    private readonly IRightAssetDataProvider _rightAssetDataProvider;
    private readonly ISnackbar _snackbar;

    public RequestTagPermissionViewModel(
        IRightAssetDataProvider rightAssetDataProvider,
        ISnackbar snackbar)
    {
        _rightAssetDataProvider = rightAssetDataProvider;
        _snackbar = snackbar;
    }

    public IReadOnlyList<RequestTagPermissionSelectableUser> SelectableUsers { get; private set; } = [];
    public IReadOnlyList<UserAvailableRightAssetDto> MyAvailableAssets { get; private set; } = [];

    public string? SelectedTargetUserId { get; set; }
    public int RequestedAmount { get; set; } = 1;
    public int? SelectedOfferedAssetId { get; set; }
    public int OfferedAmount { get; set; } = 1;
    public string? Message { get; set; }
    public bool IsSubmitting { get; private set; }

    /// <summary>
    ///     ダイアログ初期化時のデータ読み込みと候補ユーザー一覧の構築を行う。
    /// </summary>
    public async Task InitializeAsync(
        Data.Tag requestedTag,
        string? presetTargetUserId,
        IReadOnlyList<RightAssetHolderSummary> availableHolders,
        string? currentUserId,
        CancellationToken ct = default)
    {
        var usersMap = new Dictionary<string, RequestTagPermissionSelectableUser>();

        // タグオーナー（自分以外）を追加
        if (!string.IsNullOrWhiteSpace(requestedTag.OwnerId) && requestedTag.OwnerId != currentUserId)
        {
            var ownerName = requestedTag.Owner?.UserName ?? requestedTag.OwnerId;
            usersMap[requestedTag.OwnerId] = new RequestTagPermissionSelectableUser(
                requestedTag.OwnerId,
                $"{ownerName} (タグ作成者)",
                0);
        }

        // 保有者一覧（自分以外）を追加
        foreach (var holder in availableHolders.Where(h => h.UserId != currentUserId && h.TotalAmount > 0))
        {
            var name = holder.UserName ?? holder.UserId;
            var isOwner = holder.UserId == requestedTag.OwnerId;
            var roleSuffix = isOwner ? " [作成者]" : "";
            usersMap[holder.UserId] = new RequestTagPermissionSelectableUser(
                holder.UserId,
                $"{name}{roleSuffix} (有効保有量: {holder.TotalAmount})",
                holder.TotalAmount);
        }

        SelectableUsers = usersMap.Values.ToList();

        // 初期選択ユーザーの決定
        if (!string.IsNullOrWhiteSpace(presetTargetUserId) && usersMap.ContainsKey(presetTargetUserId))
        {
            SelectedTargetUserId = presetTargetUserId;
        }
        else if (SelectableUsers.Count > 0)
        {
            SelectedTargetUserId = SelectableUsers[0].UserId;
        }

        // ログインユーザーの保有RightAsset一覧（対価候補）を取得
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var assets = await _rightAssetDataProvider.GetAvailableRightAssetsForUserAsync(currentUserId, ct);
            MyAvailableAssets = assets ?? [];
        }
        else
        {
            MyAvailableAssets = [];
        }
    }

    /// <summary>
    ///     選択中の対価アセットの最大保有量を返す。
    /// </summary>
    public int GetSelectedAssetMaxAmount()
    {
        if (!SelectedOfferedAssetId.HasValue) return 1;
        var asset = MyAvailableAssets.FirstOrDefault(a => a.Id == SelectedOfferedAssetId.Value);
        return asset?.Amount ?? 1;
    }

    /// <summary>
    ///     選択されたターゲットユーザーの保有量ヘルパーテキストを生成する。
    /// </summary>
    public string GetTargetUserAmountHelperText()
    {
        if (string.IsNullOrWhiteSpace(SelectedTargetUserId)) return string.Empty;
        var targetUser = SelectableUsers.FirstOrDefault(u => u.UserId == SelectedTargetUserId);
        return targetUser is { TotalAmount: > 0 }
            ? $"相手の有効保有残高: {targetUser.TotalAmount}"
            : "相手の保有残高: 未確認または0";
    }

    /// <summary>
    ///     操作権限リクエストを送信する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Display error message in snackbar on failure")]
    public async Task<bool> SubmitAsync(int requestedTagId, string? currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedTargetUserId))
        {
            _snackbar.Add("リクエスト先のユーザーを選択してください。", Severity.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            _snackbar.Add("ログインが必要です。", Severity.Warning);
            return false;
        }

        if (RequestedAmount <= 0)
        {
            _snackbar.Add("要求数量は1以上を入力してください。", Severity.Warning);
            return false;
        }

        IsSubmitting = true;
        try
        {
            var request = new TagPermissionRequestDto(
                RequestedTagId: requestedTagId,
                TargetUserId: SelectedTargetUserId,
                RequestedAmount: RequestedAmount,
                OfferedRightAssetId: SelectedOfferedAssetId,
                OfferedAmount: SelectedOfferedAssetId.HasValue ? OfferedAmount : 0,
                Message: Message);

            var result = await _rightAssetDataProvider.SubmitPermissionRequestAsync(currentUserId, request, ct);
            switch (result)
            {
                case Success<bool>:
                    _snackbar.Add("操作権限のリクエストを送信しました。", Severity.Success);
                    return true;
                case Failure fail:
                    _snackbar.Add(fail.ErrorMessage, Severity.Error);
                    return false;
                default:
                    _snackbar.Add("予期しないエラーが発生しました。", Severity.Error);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"リクエスト送信中にエラーが発生しました: {ex.Message}", Severity.Error);
            return false;
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}