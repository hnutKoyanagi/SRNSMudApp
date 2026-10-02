using Microsoft.AspNetCore.Identity;

using Moq;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.User;

using Item = SRNSMudApp.Data.Item;
using Tag = SRNSMudApp.Data.Tag;

/// <summary>
///     UserDetailViewModel の単体テスト。
///     データ読み込み、状態保持、フォロー/アンフォロー、Admin昇格ロジックを検証する。
/// </summary>
public sealed class UserDetailViewModelTests
{
    private const string CurrentUserId = "current-user-1";
    private const string TargetUserId = "target-user-2";

    private readonly Mock<IUserDataProvider> _userDataMock = new();
    private readonly Mock<IUserStore<ApplicationUser>> _userStoreMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly UserDetailViewModel _sut;

    public UserDetailViewModelTests()
    {
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            _userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _sut = new UserDetailViewModel(
            _userDataMock.Object,
            _userManagerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenValid_LoadsDataAndSetsProperties()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var tags = new List<Tag> { new() { Id = 1, Name = "Tag1", OwnerId = TargetUserId } };
        var items = new List<Item> { new() { Id = 10, Content = "Content1", OwnerId = TargetUserId } };
        var following = new List<ApplicationUser> { new() { Id = "u3", UserName = "User3" } };
        var followers = new List<ApplicationUser> { new() { Id = "u4", UserName = "User4" } };
        var pageData = new UserDetailPageData(
            targetUser,
            tags,
            items,
            IsFollowing: true,
            FollowingCount: 5,
            FollowersCount: 12,
            FollowingUsers: following,
            FollowerUsers: followers);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(pageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        Assert.Equal(TargetUserId, _sut.UserId);
        Assert.Equal(CurrentUserId, _sut.CurrentUserId);
        Assert.True(_sut.IsLoggedIn);
        Assert.False(_sut.IsLoading);
        Assert.Equal(targetUser, _sut.User);
        Assert.Equal(tags, _sut.UserTags);
        Assert.Equal(items, _sut.UserItems);
        Assert.True(_sut.IsFollowing);
        Assert.Equal(5, _sut.FollowingCount);
        Assert.Equal(12, _sut.FollowersCount);
        Assert.Equal(following, _sut.FollowingUsers);
        Assert.Equal(followers, _sut.FollowerUsers);
        Assert.True(_sut.CanFollow);
    }

    [Fact]
    public async Task InitializeAsync_WhenFallbackNeeded_CallsFallbackMethod()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var pageData = new UserDetailPageData(targetUser, [], [], false, 0, 0);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .Returns(Task.FromResult<UserDetailPageData>(null!));

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId))
            .ReturnsAsync(pageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        Assert.Equal(targetUser, _sut.User);
        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public async Task LoadDataAsync_WhenUserIdIsEmpty_DoesNotCallProvider()
    {
        await _sut.InitializeAsync(string.Empty, CurrentUserId, isLoggedIn: true);

        Assert.Null(_sut.User);
        Assert.False(_sut.IsLoading);
        _userDataMock.Verify(u => u.GetUserDetailAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenCanFollow_TogglesAndReloads()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var initialPageData = new UserDetailPageData(targetUser, [], [], IsFollowing: false, FollowingCount: 1, FollowersCount: 5);
        var updatedPageData = new UserDetailPageData(targetUser, [], [], IsFollowing: true, FollowingCount: 1, FollowersCount: 6);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(initialPageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        _userDataMock
            .Setup(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId))
            .ReturnsAsync(true);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(updatedPageData);

        bool result = await _sut.ToggleFollowAsync();

        Assert.True(result);
        Assert.True(_sut.IsFollowing);
        Assert.Equal(6, _sut.FollowersCount);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId), Times.Once);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenUnfollowing_DecrementsFollowersCount()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var initialPageData = new UserDetailPageData(targetUser, [], [], IsFollowing: true, FollowingCount: 1, FollowersCount: 5);
        var updatedPageData = new UserDetailPageData(targetUser, [], [], IsFollowing: false, FollowingCount: 1, FollowersCount: 4);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(initialPageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        _userDataMock
            .Setup(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId))
            .ReturnsAsync(false);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(updatedPageData);

        bool result = await _sut.ToggleFollowAsync();

        Assert.False(result);
        Assert.False(_sut.IsFollowing);
        Assert.Equal(4, _sut.FollowersCount);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenCannotFollow_ReturnsFalse()
    {
        // Not logged in
        await _sut.InitializeAsync(TargetUserId, null, isLoggedIn: false);

        bool result = await _sut.ToggleFollowAsync();

        Assert.False(result);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenSelf_ReturnsFalse()
    {
        var selfUser = new ApplicationUser { Id = CurrentUserId, UserName = "Self" };
        var pageData = new UserDetailPageData(selfUser, [], [], false, 0, 0);
        _userDataMock.Setup(u => u.GetUserDetailAsync(CurrentUserId, CurrentUserId)).ReturnsAsync(pageData);

        await _sut.InitializeAsync(CurrentUserId, CurrentUserId, isLoggedIn: true);

        Assert.False(_sut.CanFollow);
        bool result = await _sut.ToggleFollowAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task ToggleFollowAsync_TwoParamOverload_WhenSelf_ReturnsFalse()
    {
        bool result = await _sut.ToggleFollowAsync(CurrentUserId, CurrentUserId);

        Assert.False(result);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFollowAsync_TwoParamOverload_WhenValid_CallsProvider()
    {
        _userDataMock
            .Setup(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId))
            .ReturnsAsync(true);

        bool result = await _sut.ToggleFollowAsync(CurrentUserId, TargetUserId);

        Assert.True(result);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId), Times.Once);
    }

    [Fact]
    public async Task MakeAdminAsync_WhenUserExists_AddsToAdminRole()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "Target" };
        _userManagerMock.Setup(m => m.FindByIdAsync(TargetUserId)).ReturnsAsync(targetUser);
        _userManagerMock.Setup(m => m.AddToRoleAsync(targetUser, "Admin")).ReturnsAsync(IdentityResult.Success);

        IdentityResult result = await _sut.MakeAdminAsync(TargetUserId);

        Assert.True(result.Succeeded);
        _userManagerMock.Verify(m => m.AddToRoleAsync(targetUser, "Admin"), Times.Once);
    }

    [Fact]
    public async Task MakeAdminAsync_WhenUserIdEmpty_ReturnsFailed()
    {
        IdentityResult result = await _sut.MakeAdminAsync(string.Empty);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MakeAdminAsync_WhenUserNotFound_ReturnsFailed()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync(TargetUserId)).ReturnsAsync((ApplicationUser?)null);

        IdentityResult result = await _sut.MakeAdminAsync(TargetUserId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MakeAdminAsync_WhenUserManagerNull_ReturnsFailed()
    {
        var sutWithoutManager = new UserDetailViewModel(_userDataMock.Object, null);

        IdentityResult result = await sutWithoutManager.MakeAdminAsync(TargetUserId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoadDataAsync_PopulatesReactionTags_SortedInCanonicalOrder()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var zenTag = new Tag { Id = 1, Name = "善", OwnerId = TargetUserId, CachedWeight = 5 };
        var biTag = new Tag { Id = 2, Name = "美", OwnerId = TargetUserId, CachedWeight = 2 };
        var shinjiTag = new Tag { Id = 3, Name = "真実", OwnerId = TargetUserId, CachedWeight = 10 };
        var customTag = new Tag { Id = 4, Name = "CustomTag", OwnerId = TargetUserId, CachedWeight = 1 };

        // 順不同で渡す
        var tags = new List<Tag> { zenTag, customTag, biTag, shinjiTag };
        var pageData = new UserDetailPageData(targetUser, tags, []);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(pageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        Assert.Equal(3, _sut.ReactionTags.Count);
        // 真実 (Shinji), 善 (Zen), 美 (Bi) の順でソートされていること
        Assert.Equal("真実", _sut.ReactionTags[0].Name);
        Assert.Equal("善", _sut.ReactionTags[1].Name);
        Assert.Equal("美", _sut.ReactionTags[2].Name);
        Assert.DoesNotContain(customTag, _sut.ReactionTags);
    }

    [Fact]
    public async Task LoadDataAsync_WhenExplicitReactionTagsProvided_UsesThem()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var explicitReaction = new List<Tag>
        {
            new() { Id = 10, Name = "真実", OwnerId = TargetUserId }
        };
        var pageData = new UserDetailPageData(
            targetUser,
            [],
            [],
            ReactionTags: explicitReaction);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(pageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        Assert.Equal(explicitReaction, _sut.ReactionTags);
    }

    [Fact]
    public async Task LoadDataAsync_WhenNoReactionTags_ReactionTagsIsEmpty()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser" };
        var customTag = new Tag { Id = 1, Name = "OnlyCustom", OwnerId = TargetUserId };
        var pageData = new UserDetailPageData(targetUser, [customTag], []);

        _userDataMock
            .Setup(u => u.GetUserDetailAsync(TargetUserId, CurrentUserId))
            .ReturnsAsync(pageData);

        await _sut.InitializeAsync(TargetUserId, CurrentUserId, isLoggedIn: true);

        Assert.Empty(_sut.ReactionTags);
    }
}