using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Resources;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.UI;

/// <summary>
///     ItemCard におけるユーザー操作（リプライ送信・グループプライバシー判定・アイテム非公開化・タグ付けリクエスト承認・キャンセルなど）を集約する ViewModel。
///     bUnit を使わずに xUnit で直接単体テスト可能。
/// </summary>
public class ItemCardActionViewModel
{
    private readonly IItemReplyService _itemReplyService;
    private readonly IItemCardDataProvider _itemCardDataProvider;
    private readonly ITaggingContractService _taggingContractService;
    private readonly ISnackbar _snackbar;

    public ItemCardActionViewModel(
        IItemReplyService itemReplyService,
        IItemCardDataProvider itemCardDataProvider,
        ITaggingContractService taggingContractService,
        ISnackbar snackbar)
    {
        _itemReplyService = itemReplyService;
        _itemCardDataProvider = itemCardDataProvider;
        _taggingContractService = taggingContractService;
        _snackbar = snackbar;
    }

    /// <summary>
    ///     親アイテムの公開/非公開設定を継承し、リプライを送信する。
    /// </summary>
    public async Task<Data.Item?> SubmitReplyAsync(
        Data.Item parentItem,
        string replyContent,
        string currentUserId,
        bool isPrivateRequested,
        IEnumerable<string>? targetUserIds = null)
    {
        if (parentItem == null || string.IsNullOrWhiteSpace(replyContent) || string.IsNullOrEmpty(currentUserId))
        {
            return null;
        }

        // 親アイテムが非公開なら親のターゲットグループを引き継ぐ
        bool isPrivate = isPrivateRequested;
        int? targetGroupId = isPrivate ? parentItem.TargetUserGroupId : null;

        return await _itemReplyService.AddItemReplyAsync(
            parentItem.Id,
            replyContent,
            currentUserId,
            targetUserIds,
            isPrivate,
            targetGroupId);
    }

    /// <summary>
    ///     管理者権限でアイテムを物理削除する。
    /// </summary>
    public async Task<bool> DeleteItemByAdminAsync(int itemId, string adminUserId, bool isAdmin)
    {
        if (!isAdmin || itemId <= 0 || string.IsNullOrEmpty(adminUserId))
        {
            return false;
        }

        return await _itemCardDataProvider.DeleteItemByAdminAsync(itemId, adminUserId);
    }

    /// <summary>
    ///     管理者権限でアイテムの強制非公開状態（および解除）を設定する。
    /// </summary>
    public async Task<bool> SetAdminHiddenAsync(int itemId, bool isHidden, string? reason, string adminUserId, bool isAdmin)
    {
        if (!isAdmin || itemId <= 0 || string.IsNullOrEmpty(adminUserId))
        {
            return false;
        }

        return await _itemCardDataProvider.SetAdminHiddenAsync(itemId, isHidden, reason, adminUserId);
    }

    /// <summary>
    ///     アイテムの本文を更新する。空文字・長度チェックなどの検証を含む。
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> UpdateItemContentAsync(int itemId, string newContent)
    {
        if (itemId <= 0)
        {
            return (false, "対象のアイテムが無効です。");
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            return (false, "内容は空にできません。");
        }

        if (newContent.Length > 1000)
        {
            return (false, "内容は1000文字以内で入力してください。");
        }

        bool updated = await _itemCardDataProvider.UpdateItemContentAsync(itemId, newContent);
        if (!updated)
        {
            return (false, "対象のアイテムが見つかりませんでした。");
        }

        return (true, null);
    }

    /// <summary>
    ///     タグ付けリクエスト（契約）をキャンセルする。
    /// </summary>
    public async Task<bool> CancelTaggingRequestAsync(TaggingRequestEntity? request, string currentUserId)
    {
        if (request is null || string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        Result<string> result = await _taggingContractService.CancelContractAsync(request.Id, currentUserId);
        switch (result)
        {
            case Success<string>:
                request.Cancel();
                _snackbar.Add(ErrorMessages.ContractCancelSuccess, Severity.Success);
                return true;
            case Failure f:
                _snackbar.Add($"エラー: {f.ErrorMessage}", Severity.Error);
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    ///     タグ付けリクエスト（契約）を承認・実行する。
    /// </summary>
    public async Task<bool> ApproveTaggingRequestAsync(TaggingRequestEntity? request, string currentUserId)
    {
        if (request is null || string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        Result<string> result = await _taggingContractService.AcceptContractAsync(request.Id, currentUserId);
        switch (result)
        {
            case Success<string>:
                request.Execute();
                _snackbar.Add(ErrorMessages.ContractApproveSuccess, Severity.Success);
                return true;
            case Failure f:
                _snackbar.Add($"エラー: {f.ErrorMessage}", Severity.Error);
                return false;
            default:
                return false;
        }
    }
}