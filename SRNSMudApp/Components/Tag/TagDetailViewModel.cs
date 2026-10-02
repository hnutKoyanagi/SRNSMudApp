using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     TagDetail コンポーネントの状態管理、データ取得、およびタグに対する各種操作を集約する ViewModel。
///     Blazor の UI レンダリング (bUnit) から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class TagDetailViewModel
{
    private readonly ITagDetailDataProvider _tagDetailData;
    private readonly ITagCommandService _tagCommandService;
    private readonly ITagContentProposalService _tagContentProposalService;
    private readonly ITagNameProposalService _tagNameProposalService;
    private readonly ITagLockService _tagLockService;

    public TagDetailViewModel(
        ITagDetailDataProvider tagDetailData,
        ITagCommandService tagCommandService,
        ITagContentProposalService tagContentProposalService,
        ITagNameProposalService tagNameProposalService,
        ITagLockService tagLockService)
    {
        _tagDetailData = tagDetailData;
        _tagCommandService = tagCommandService;
        _tagContentProposalService = tagContentProposalService;
        _tagNameProposalService = tagNameProposalService;
        _tagLockService = tagLockService;
    }

    public Data.Tag? Tag { get; private set; }
    public bool IsFollowing { get; private set; }
    public IReadOnlyList<Data.Item> RelatedItems { get; private set; } = [];
    public IReadOnlyList<Data.Tag> RelatedTags { get; private set; } = [];
    public IReadOnlyList<TagWeightLedger> WeightLedgers { get; private set; } = [];
    public IReadOnlyList<PublicTradeOffer> PublicOffers { get; private set; } = [];
    public IReadOnlyList<TaggingRequestEntity> PendingRequests { get; private set; } = [];
    public IReadOnlyList<TagContentProposal> PendingProposals { get; private set; } = [];
    public IReadOnlyList<TagNameProposal> PendingNameProposals { get; private set; } = [];
    public RightAssetOverviewData? RightAssetOverview { get; private set; }

    public bool AreAncestorsLocked { get; private set; }
    public bool IsTagOrSiblingLocked { get; private set; }
    public bool IsTogglingAncestorsLock { get; private set; }
    public bool IsTogglingTagLock { get; private set; }

    public string? CurrentUserId { get; private set; }
    public bool IsAdmin { get; private set; }

    public bool CanDelete => Tag != null && TagTableViewModel.CanDeleteTag(Tag, CurrentUserId, IsTagOrSiblingLocked, IsAdmin);
    public bool CanEdit => Tag != null && (IsAdmin || (CurrentUserId == Tag.OwnerId && !IsTagOrSiblingLocked));
    public bool IsOwnerOrAdmin => Tag != null && (CurrentUserId == Tag.OwnerId || IsAdmin);
    public bool CanShowLockSwitches => IsAdmin && Tag != null && Tag.Name != Data.Tag.RootTagName;
    public int TotalPendingProposalsCount => PendingProposals.Count + PendingNameProposals.Count;

    /// <summary>
    ///     認証情報からユーザーIDおよび管理者権限を設定する。
    /// </summary>
    public void SetUserContext(ClaimsPrincipal? user)
    {
        if (user != null)
        {
            CurrentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            IsAdmin = user.IsInRole("Admin");
        }
        else
        {
            CurrentUserId = null;
            IsAdmin = false;
        }
    }

    /// <summary>
    ///     ユーザーコンテキストを直接設定する（テストおよび明示的設定用）。
    /// </summary>
    public void SetUserContext(string? userId, bool isAdmin)
    {
        CurrentUserId = userId;
        IsAdmin = isAdmin;
    }

    /// <summary>
    ///     タグ詳細データ、提案、ロック状態を一括して非同期読み込みする。
    /// </summary>
    public async Task LoadDataAsync(int tagId, CancellationToken cancellationToken = default)
    {
        TagDetailPageData data = await _tagDetailData.GetTagDetailAsync(tagId, CurrentUserId);
        Tag = data.Tag;
        IsFollowing = data.IsFollowing;
        RelatedItems = data.RelatedItems?.ToList() ?? [];
        RelatedTags = data.RelatedTags?.ToList() ?? [];
        WeightLedgers = data.WeightLedgers?.ToList() ?? [];
        PublicOffers = data.PublicOffers?.ToList() ?? [];
        PendingRequests = data.PendingRequests?.ToList() ?? [];
        RightAssetOverview = data.RightAssetOverview;

        PendingProposals = await _tagContentProposalService.GetPendingProposalsForTagAsync(tagId, cancellationToken);
        PendingNameProposals = await _tagNameProposalService.GetPendingProposalsForTagAsync(tagId, cancellationToken);
        AreAncestorsLocked = await _tagLockService.AreAncestorsLockedAsync(tagId, cancellationToken);
        IsTagOrSiblingLocked = await _tagLockService.IsTagOrSiblingLockedAsync(tagId, cancellationToken);
    }

    /// <summary>
    ///     このタグ自体の個別ロックをトグルする（管理者のみ）。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラーメッセージを返却するため捕捉する")]
    public async Task<(bool Success, string Message, bool IsLocked)> ToggleThisTagLockAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAdmin || IsTogglingTagLock || Tag is null)
        {
            return (false, "操作を実行する権限がないか、現在処理中です。", Tag?.IsLocked ?? false);
        }

        IsTogglingTagLock = true;
        try
        {
            bool isLocked = await _tagLockService.ToggleTagLockAsync(Tag.Id, cancellationToken);
            await LoadDataAsync(Tag.Id, cancellationToken);
            string message = isLocked ? "このタグを個別ロックしました。" : "このタグの個別ロックを解除しました。";
            return (true, message, isLocked);
        }
        catch (Exception ex)
        {
            return (false, $"タグのロック操作に失敗しました: {ex.Message}", Tag.IsLocked);
        }
        finally
        {
            IsTogglingTagLock = false;
        }
    }

    /// <summary>
    ///     このタグの祖先のロック状態を切り替える（管理者のみ）。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラーメッセージを返却するため捕捉する")]
    public async Task<(bool Success, string Message)> ToggleAncestorsLockAsync(bool shouldLock, CancellationToken cancellationToken = default)
    {
        if (!IsAdmin || IsTogglingAncestorsLock || Tag is null)
        {
            return (false, "操作を実行する権限がないか、現在処理中です。");
        }

        IsTogglingAncestorsLock = true;
        try
        {
            if (shouldLock)
            {
                await _tagLockService.LockAncestorsAsync(Tag.Id, cancellationToken);
                await LoadDataAsync(Tag.Id, cancellationToken);
                return (true, "このタグの祖先をロックしました。");
            }
            else
            {
                await _tagLockService.UnlockAncestorsAsync(Tag.Id, cancellationToken);
                await LoadDataAsync(Tag.Id, cancellationToken);
                return (true, "このタグの祖先のロックを解除しました。");
            }
        }
        catch (Exception ex)
        {
            string action = shouldLock ? "ロック" : "ロック解除";
            return (false, $"祖先の{action}に失敗しました: {ex.Message}");
        }
        finally
        {
            IsTogglingAncestorsLock = false;
        }
    }

    /// <summary>
    ///     タグのフォロー状態をトグルする。
    /// </summary>
    public async Task<bool> ToggleFollowAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null || Tag is null)
        {
            return false;
        }

        IsFollowing = await _tagDetailData.ToggleFollowAsync(Tag.Id, CurrentUserId);
        return IsFollowing;
    }

    /// <summary>
    ///     タグの自動承認設定を更新する。
    /// </summary>
    public async Task<bool> UpdateAutoAcceptAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (Tag is null)
        {
            return false;
        }

        var groupIds = Tag.AutoApproveUserGroups.Select(g => g.UserGroupId).ToList();
        bool updated = await _tagCommandService.UpdateTagAsync(Tag.Id, Tag.Name, Tag.Content, enabled, groupIds);
        if (updated)
        {
            Tag.AutoAcceptIncomingTaggingRequests = enabled;
        }

        return updated;
    }

    /// <summary>
    ///     タグ削除を実行する。
    /// </summary>
    public async Task<TagDeleteOperationResult> DeleteTagAsync(CancellationToken cancellationToken = default)
    {
        if (Tag is null)
        {
            return TagDeleteOperationResult.NotFound;
        }

        if (IsTagOrSiblingLocked && !IsAdmin)
        {
            return TagDeleteOperationResult.Locked;
        }

        if (!CanDelete)
        {
            return Tag.IsSystem ? TagDeleteOperationResult.SystemTag : TagDeleteOperationResult.Unauthorized;
        }

        return await _tagDetailData.DeleteTagWithResultAsync(Tag.Id, CurrentUserId, IsAdmin);
    }

    /// <summary>
    ///     内容編集提案を承認する。
    /// </summary>
    public async Task<Result<Data.Tag>> ApproveContentProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<Data.Tag> result = await _tagContentProposalService.ApproveProposalAsync(proposalId, CurrentUserId, cancellationToken);
        if (result is Success<Data.Tag> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     内容編集提案を却下する。
    /// </summary>
    public async Task<Result<bool>> RejectContentProposalAsync(int proposalId, string? comment, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<bool> result = await _tagContentProposalService.RejectProposalAsync(proposalId, CurrentUserId, comment, cancellationToken);
        if (result is Success<bool> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     内容編集提案を取り下げる。
    /// </summary>
    public async Task<Result<bool>> CancelContentProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<bool> result = await _tagContentProposalService.CancelProposalAsync(proposalId, CurrentUserId, cancellationToken);
        if (result is Success<bool> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     名前変更提案を承認する。
    /// </summary>
    public async Task<Result<Data.Tag>> ApproveNameProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<Data.Tag> result = await _tagNameProposalService.ApproveProposalAsync(proposalId, CurrentUserId, cancellationToken);
        if (result is Success<Data.Tag> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     名前変更提案を却下する。
    /// </summary>
    public async Task<Result<bool>> RejectNameProposalAsync(int proposalId, string? comment, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<bool> result = await _tagNameProposalService.RejectProposalAsync(proposalId, CurrentUserId, comment, cancellationToken);
        if (result is Success<bool> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     名前変更提案を取り下げる。
    /// </summary>
    public async Task<Result<bool>> CancelNameProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is null)
        {
            return new Failure("ログインが必要です。");
        }

        Result<bool> result = await _tagNameProposalService.CancelProposalAsync(proposalId, CurrentUserId, cancellationToken);
        if (result is Success<bool> && Tag is not null)
        {
            await LoadDataAsync(Tag.Id, cancellationToken);
        }

        return result;
    }
}