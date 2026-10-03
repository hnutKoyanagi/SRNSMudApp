namespace SRNSMudApp.Components.Pages;

using System.Security.Claims;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     Home 画面（タイムライン表示・システムタグ確保など）の状態管理およびデータフェッチを担当する ViewModel。
///     Razor コンポーネントからの直接的なサービス依存を排除し、単体テストを可能にする。
/// </summary>
public sealed class HomeViewModel
{
    private readonly IHomeDataProvider _homeData;
    private readonly ISystemTagEnsurer _systemTagEnsurer;

    public HomeViewModel(IHomeDataProvider homeData, ISystemTagEnsurer systemTagEnsurer)
    {
        _homeData = homeData ?? throw new ArgumentNullException(nameof(homeData));
        _systemTagEnsurer = systemTagEnsurer ?? throw new ArgumentNullException(nameof(systemTagEnsurer));
    }

    public bool IsLoggedIn { get; private set; }
    public string CurrentUserId { get; private set; } = string.Empty;
    public IReadOnlyList<int>? FollowedTagIds { get; private set; }
    public IReadOnlyList<Tag> AllTags { get; private set; } = [];
    public IReadOnlyList<TagRelationToTag> AllTagRelationsToTags { get; private set; } = [];

    public int? CurrentUserGoodTagId { get; private set; }
    public int? CurrentUserBadTagId { get; private set; }
    public int? CurrentUserShinjiTagId { get; private set; }
    public int? CurrentUserZenTagId { get; private set; }
    public int? CurrentUserBiTagId { get; private set; }

    /// <summary>
    ///     認証状態に基づいて画面の初期化処理を行う。
    /// </summary>
    public async Task InitializeAsync(ClaimsPrincipal? user)
    {
        IsLoggedIn = user?.Identity?.IsAuthenticated ?? false;

        if (IsLoggedIn && user is not null)
        {
            CurrentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                await LoadUserDataAsync();
            }
        }
    }

    /// <summary>
    ///     ログインユーザーのフォロータグおよび全タグ情報を読み込む。
    /// </summary>
    public async Task LoadUserDataAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        FollowedTagIds = await _homeData.GetFollowedTagIdsAsync(CurrentUserId);
        await FetchTagsAsync();
    }

    /// <summary>
    ///     タグおよびタグ間関連一覧を取得し、システムタグの割り当てを行う。
    /// </summary>
    public async Task FetchTagsAsync()
    {
        var (tags, relations) = await _homeData.GetTagsAndRelationsAsync();
        AllTags = tags ?? [];
        AllTagRelationsToTags = relations ?? [];

        if (!string.IsNullOrEmpty(CurrentUserId))
        {
            AssignSystemTags();
        }
    }

    /// <summary>
    ///     全タグの中から現在のユーザーに対応するシステムタグ（Good, Bad, 反応タグ等）を特定して設定する。
    /// </summary>
    public void AssignSystemTags()
    {
        var goodTag = AllTags.FirstOrDefault(t => t.OwnerId == CurrentUserId && t.Name == "good" && t.IsSystem);
        var badTag = AllTags.FirstOrDefault(t => t.OwnerId == CurrentUserId && t.Name == "bad" && t.IsSystem);
        CurrentUserGoodTagId = goodTag?.Id;
        CurrentUserBadTagId = badTag?.Id;

        ReactionTagIds reactionIds = ResourceListViewModel.FindReactionTags(AllTags.ToList(), CurrentUserId);
        CurrentUserShinjiTagId = reactionIds.ShinjiTagId;
        CurrentUserZenTagId = reactionIds.ZenTagId;
        CurrentUserBiTagId = reactionIds.BiTagId;
    }

    /// <summary>
    ///     必要なシステムタグが存在することを保証し、必要に応じて再取得を行う。
    /// </summary>
    public async Task EnsureSystemTagsExistAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        var (voteIds, reactionIds, refetch) = await _systemTagEnsurer.EnsureAllAsync(
            CurrentUserId,
            new SystemTagIds(CurrentUserGoodTagId, CurrentUserBadTagId),
            new ReactionTagIds(CurrentUserShinjiTagId, CurrentUserZenTagId, CurrentUserBiTagId));

        CurrentUserGoodTagId = voteIds.GoodTagId;
        CurrentUserBadTagId = voteIds.BadTagId;
        CurrentUserShinjiTagId = reactionIds.ShinjiTagId;
        CurrentUserZenTagId = reactionIds.ZenTagId;
        CurrentUserBiTagId = reactionIds.BiTagId;

        if (refetch)
        {
            await FetchTagsAsync();
        }
    }

    /// <summary>
    ///     タイムラインのページネーションデータを取得する。
    /// </summary>
    public async Task<(IReadOnlyList<TimelineFeedGroup> Groups, int TotalCount)> LoadTimelineAsync(int startIndex, int count)
    {
        if (string.IsNullOrEmpty(CurrentUserId) && (FollowedTagIds is null or { Count: 0 }))
        {
            return ([], 0);
        }

        IReadOnlyList<int> followedTagIds = FollowedTagIds ?? [];
        HomeTimelinePage page = await _homeData.LoadTimelineAsync(
            followedTagIds,
            startIndex,
            count,
            CurrentUserId);

        return (page.Groups ?? [], page.TotalCount);
    }
}