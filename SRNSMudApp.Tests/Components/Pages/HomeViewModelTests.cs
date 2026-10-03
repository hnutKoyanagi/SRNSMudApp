namespace SRNSMudApp.Tests.Components.Pages;

using System.Security.Claims;

using Moq;

using SRNSMudApp.Components.Pages;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

/// <summary>
///     <see cref="HomeViewModel" /> の単体テスト。
///     初期化時のユーザー判定、タグ・タイムライン取得、システムタグ割り当て・確保ロジックを検証する。
/// </summary>
public sealed class HomeViewModelTests
{
    private readonly Mock<IHomeDataProvider> _homeDataMock = new();
    private readonly Mock<ISystemTagEnsurer> _systemTagEnsurerMock = new();
    private readonly HomeViewModel _sut;

    public HomeViewModelTests()
    {
        _sut = new HomeViewModel(_homeDataMock.Object, _systemTagEnsurerMock.Object);
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HomeViewModel(null!, _systemTagEnsurerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new HomeViewModel(_homeDataMock.Object, null!));
    }

    [Fact]
    public async Task InitializeAsync_UnauthenticatedUser_SetsIsLoggedInFalse()
    {
        // Act
        await _sut.InitializeAsync(null);

        // Assert
        Assert.False(_sut.IsLoggedIn);
        Assert.Equal(string.Empty, _sut.CurrentUserId);
        Assert.Null(_sut.FollowedTagIds);
        Assert.Empty(_sut.AllTags);
        _homeDataMock.Verify(d => d.GetFollowedTagIdsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_AuthenticatedUser_LoadsUserDataAndTags()
    {
        // Arrange
        const string userId = "user-123";
        ClaimsPrincipal user = CreateClaimsPrincipal(userId);

        List<int> followedTagIds = [1, 2, 3];
        List<Tag> tags =
        [
            new() { Id = 10, Name = "good", OwnerId = userId, IsSystem = true },
            new() { Id = 11, Name = "bad", OwnerId = userId, IsSystem = true },
            new() { Id = 12, Name = ReactionTagNames.Shinji, OwnerId = userId, IsSystem = true },
            new() { Id = 13, Name = ReactionTagNames.Zen, OwnerId = userId, IsSystem = true },
            new() { Id = 14, Name = ReactionTagNames.Bi, OwnerId = userId, IsSystem = true }
        ];
        List<TagRelationToTag> relations = [];

        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(userId))
            .ReturnsAsync(followedTagIds);
        _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync())
            .ReturnsAsync((tags, relations));

        // Act
        await _sut.InitializeAsync(user);

        // Assert
        Assert.True(_sut.IsLoggedIn);
        Assert.Equal(userId, _sut.CurrentUserId);
        Assert.Equal(followedTagIds, _sut.FollowedTagIds);
        Assert.Equal(5, _sut.AllTags.Count);
        Assert.Equal(10, _sut.CurrentUserGoodTagId);
        Assert.Equal(11, _sut.CurrentUserBadTagId);
        Assert.Equal(12, _sut.CurrentUserShinjiTagId);
        Assert.Equal(13, _sut.CurrentUserZenTagId);
        Assert.Equal(14, _sut.CurrentUserBiTagId);
        _homeDataMock.Verify(d => d.GetFollowedTagIdsAsync(userId), Times.Once);
        _homeDataMock.Verify(d => d.GetTagsAndRelationsAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadUserDataAsync_EmptyUserId_DoesNothing()
    {
        // Act
        await _sut.LoadUserDataAsync();

        // Assert
        Assert.Null(_sut.FollowedTagIds);
        _homeDataMock.Verify(d => d.GetFollowedTagIdsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSystemTagsExistAsync_EmptyUserId_DoesNothing()
    {
        // Act
        await _sut.EnsureSystemTagsExistAsync();

        // Assert
        _systemTagEnsurerMock.Verify(e => e.EnsureAllAsync(
            It.IsAny<string>(),
            It.IsAny<SystemTagIds>(),
            It.IsAny<ReactionTagIds>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSystemTagsExistAsync_WithUserId_UpdatesIdsAndRefetchesWhenRequested()
    {
        // Arrange
        const string userId = "user-123";
        ClaimsPrincipal user = CreateClaimsPrincipal(userId);

        List<Tag> initialTags = [];
        List<Tag> refetchedTags =
        [
            new() { Id = 100, Name = "good", OwnerId = userId, IsSystem = true },
            new() { Id = 101, Name = "bad", OwnerId = userId, IsSystem = true },
            new() { Id = 102, Name = ReactionTagNames.Shinji, OwnerId = userId, IsSystem = true },
            new() { Id = 103, Name = ReactionTagNames.Zen, OwnerId = userId, IsSystem = true },
            new() { Id = 104, Name = ReactionTagNames.Bi, OwnerId = userId, IsSystem = true }
        ];

        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(userId)).ReturnsAsync([]);
        _homeDataMock.SetupSequence(d => d.GetTagsAndRelationsAsync())
            .ReturnsAsync((initialTags, []))
            .ReturnsAsync((refetchedTags, []));
        await _sut.InitializeAsync(user);

        var returnedVoteIds = new SystemTagIds(100, 101);
        var returnedReactionIds = new ReactionTagIds(102, 103, 104);

        _systemTagEnsurerMock.Setup(e => e.EnsureAllAsync(
                userId,
                It.IsAny<SystemTagIds>(),
                It.IsAny<ReactionTagIds>()))
            .ReturnsAsync((returnedVoteIds, returnedReactionIds, true));

        // Act
        await _sut.EnsureSystemTagsExistAsync();

        // Assert
        Assert.Equal(100, _sut.CurrentUserGoodTagId);
        Assert.Equal(101, _sut.CurrentUserBadTagId);
        Assert.Equal(102, _sut.CurrentUserShinjiTagId);
        Assert.Equal(103, _sut.CurrentUserZenTagId);
        Assert.Equal(104, _sut.CurrentUserBiTagId);

        // refetch が true なので GetTagsAndRelationsAsync が初回と合わせて2回呼ばれること
        _homeDataMock.Verify(d => d.GetTagsAndRelationsAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task EnsureSystemTagsExistAsync_WithoutRefetch_DoesNotReloadTags()
    {
        // Arrange
        const string userId = "user-123";
        ClaimsPrincipal user = CreateClaimsPrincipal(userId);

        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(userId)).ReturnsAsync([]);
        _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync()).ReturnsAsync(([], []));
        await _sut.InitializeAsync(user);

        var returnedVoteIds = new SystemTagIds(100, 101);
        var returnedReactionIds = new ReactionTagIds(102, 103, 104);

        _systemTagEnsurerMock.Setup(e => e.EnsureAllAsync(
                userId,
                It.IsAny<SystemTagIds>(),
                It.IsAny<ReactionTagIds>()))
            .ReturnsAsync((returnedVoteIds, returnedReactionIds, false));

        // Act
        await _sut.EnsureSystemTagsExistAsync();

        // Assert
        Assert.Equal(100, _sut.CurrentUserGoodTagId);
        _homeDataMock.Verify(d => d.GetTagsAndRelationsAsync(), Times.Once); // 初回の1回のみ
    }

    [Fact]
    public async Task LoadTimelineAsync_Unauthenticated_ReturnsEmptyResult()
    {
        // Unauthenticated user (CurrentUserId empty, FollowedTagIds null)
        var (groups, totalCount) = await _sut.LoadTimelineAsync(0, 10);
        Assert.Empty(groups);
        Assert.Equal(0, totalCount);

        _homeDataMock.Verify(d => d.LoadTimelineAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadTimelineAsync_AuthenticatedWithEmptyFollowedTags_CallsLoadTimelineForOwnPosts()
    {
        // Arrange
        const string userId = "user-123";
        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(userId)).ReturnsAsync([]);
        _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync()).ReturnsAsync(([], []));
        await _sut.InitializeAsync(CreateClaimsPrincipal(userId));

        List<TimelineFeedGroup> expectedGroups = [new() { TimelineTargetJson = "my-item-target" }];
        _homeDataMock.Setup(d => d.LoadTimelineAsync(It.Is<IReadOnlyList<int>>(l => l.Count == 0), 0, 10, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HomeTimelinePage(expectedGroups, 1));

        // Act
        var (groups, totalCount) = await _sut.LoadTimelineAsync(0, 10);

        // Assert: フォロータグが0件でも自分の投稿を読み込むために LoadTimelineAsync が呼ばれること
        Assert.Single(groups);
        Assert.Equal(1, totalCount);
        Assert.Equal("my-item-target", groups[0].TimelineTargetJson);
        _homeDataMock.Verify(d => d.LoadTimelineAsync(It.Is<IReadOnlyList<int>>(l => l.Count == 0), 0, 10, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadTimelineAsync_WithFollowedTags_ReturnsTimelinePage()
    {
        // Arrange
        const string userId = "user-123";
        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(userId)).ReturnsAsync([1, 2]);
        _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync()).ReturnsAsync(([], []));
        await _sut.InitializeAsync(CreateClaimsPrincipal(userId));

        List<TimelineFeedGroup> expectedGroups =
        [
            new() { TimelineTargetJson = "target1" },
            new() { TimelineTargetJson = "target2" }
        ];
        _homeDataMock.Setup(d => d.LoadTimelineAsync(It.IsAny<IReadOnlyList<int>>(), 0, 20, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HomeTimelinePage(expectedGroups, 50));

        // Act
        var (groups, totalCount) = await _sut.LoadTimelineAsync(0, 20);

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Equal(50, totalCount);
        Assert.Equal("target1", groups[0].TimelineTargetJson);
    }

    [Fact]
    public void TimelineFeedGroupComparer_ComparesCorrectly()
    {
        var comparer = new TimelineFeedGroupComparer();
        var groupA = new TimelineFeedGroup { TimelineTargetJson = "target-1" };
        var groupB = new TimelineFeedGroup { TimelineTargetJson = "target-1" };
        var groupC = new TimelineFeedGroup { TimelineTargetJson = "target-2" };

        Assert.True(comparer.Equals(groupA, groupB));
        Assert.False(comparer.Equals(groupA, groupC));
        Assert.False(comparer.Equals(groupA, null));
        Assert.False(comparer.Equals(null, groupA));
        Assert.True(comparer.Equals(null, null));

        Assert.Equal(comparer.GetHashCode(groupA), comparer.GetHashCode(groupB));
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}