#region

using Moq;

using MudBlazor;

using SRNSMudApp.Components.UserGroup;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using UserGroupEntity = SRNSMudApp.Data.UserGroup;

#endregion

namespace SRNSMudApp.Tests.Components.UserGroup;

/// <summary>
///     <see cref="UserGroupListViewModel" /> の単体テスト。
///     グループ一覧の取得、削除、オーナー判定、およびダイアログパラメータ生成のロジックを検証する。
/// </summary>
public sealed class UserGroupListViewModelTests
{
    private readonly Mock<IUserGroupDataProvider> _mockDataProvider = new();

    private UserGroupListViewModel CreateViewModel()
    {
        return new UserGroupListViewModel(_mockDataProvider.Object);
    }

    [Fact]
    public void Constructor_WhenProviderIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new UserGroupListViewModel(null!));
    }

    [Fact]
    public void InitialState_PropertiesDefaultCorrectly()
    {
        var vm = CreateViewModel();

        Assert.Empty(vm.Groups);
        Assert.Equal(string.Empty, vm.CurrentUserId);
        Assert.True(vm.IsLoading);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task LoadGroupsAsync_WhenCurrentUserIdIsNullOrWhiteSpace_SetsEmptyGroups(string? userId)
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = userId!;

        await vm.LoadGroupsAsync();

        Assert.Empty(vm.Groups);
        Assert.False(vm.IsLoading);
        _mockDataProvider.Verify(p => p.GetUserGroupsForUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadGroupsAsync_WhenValidUserId_LoadsGroupsFromProvider()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        var groups = new List<UserGroupEntity>
        {
            new() { Id = 1, Name = "Group A", OwnerId = "user-123" },
            new() { Id = 2, Name = "Group B", OwnerId = "user-456" }
        };
        _mockDataProvider.Setup(p => p.GetUserGroupsForUserAsync("user-123"))
            .ReturnsAsync(groups);

        await vm.LoadGroupsAsync();

        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("Group A", vm.Groups[0].Name);
        Assert.Equal("Group B", vm.Groups[1].Name);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadGroupsAsync_WhenProviderReturnsNull_SetsEmptyList()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        _mockDataProvider.Setup(p => p.GetUserGroupsForUserAsync("user-123"))
            .ReturnsAsync((IReadOnlyList<UserGroupEntity>)null!);

        await vm.LoadGroupsAsync();

        Assert.NotNull(vm.Groups);
        Assert.Empty(vm.Groups);
        Assert.False(vm.IsLoading);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteGroupAsync_WhenCurrentUserIdIsNullOrWhiteSpace_ReturnsFailure(string userId)
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = userId;

        var result = await vm.DeleteGroupAsync(1);

        Assert.True(result is Failure f && f.ErrorMessage == "ユーザー情報が取得できませんでした。");
        _mockDataProvider.Verify(p => p.DeleteUserGroupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenDeleteFails_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        _mockDataProvider.Setup(p => p.DeleteUserGroupAsync(10, "user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await vm.DeleteGroupAsync(10);

        Assert.True(result is Failure f && f.ErrorMessage == "グループの削除に失敗しました。");
        _mockDataProvider.Verify(p => p.GetUserGroupsForUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenDeleteSucceeds_ReloadsGroupsAndReturnsSuccess()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        _mockDataProvider.Setup(p => p.DeleteUserGroupAsync(10, "user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockDataProvider.Setup(p => p.GetUserGroupsForUserAsync("user-123"))
            .ReturnsAsync(new List<UserGroupEntity>());

        var result = await vm.DeleteGroupAsync(10);

        Assert.True(result is Success<bool> s && s.Value);
        _mockDataProvider.Verify(p => p.GetUserGroupsForUserAsync("user-123"), Times.Once);
    }

    [Fact]
    public void IsOwner_WhenGroupIsNull_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        Assert.False(vm.IsOwner(null));
    }

    [Fact]
    public void IsOwner_WhenCurrentUserIdIsEmpty_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "";
        var group = new UserGroupEntity { Name = "Test Group", OwnerId = "user-123" };

        Assert.False(vm.IsOwner(group));
    }

    [Fact]
    public void IsOwner_WhenOwnerIdMatches_ReturnsTrue()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";
        var group = new UserGroupEntity { Name = "Test Group", OwnerId = "user-123" };

        Assert.True(vm.IsOwner(group));
    }

    [Fact]
    public void IsOwner_WhenOwnerIdDoesNotMatch_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";
        var group = new UserGroupEntity { Name = "Test Group", OwnerId = "user-456" };

        Assert.False(vm.IsOwner(group));
    }

    [Fact]
    public void CreateDialogParameters_ContainsCurrentUserId()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        var parameters = vm.CreateDialogParameters();

        Assert.Equal("user-123", parameters[nameof(UserGroupCreateEditDialog.CurrentUserId)]);
    }

    [Fact]
    public void EditDialogParameters_WhenGroupIsNull_ThrowsArgumentNullException()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        _ = Assert.Throws<ArgumentNullException>(() => vm.EditDialogParameters(null!));
    }

    [Fact]
    public void EditDialogParameters_ContainsGroupAndCurrentUserId()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";
        var group = new UserGroupEntity { Id = 5, Name = "Test Group", OwnerId = "user-123" };

        var parameters = vm.EditDialogParameters(group);

        Assert.Same(group, parameters[nameof(UserGroupCreateEditDialog.Group)]);
        Assert.Equal("user-123", parameters[nameof(UserGroupCreateEditDialog.CurrentUserId)]);
    }

    [Fact]
    public void MembersDialogParameters_WhenGroupIsNull_ThrowsArgumentNullException()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";

        _ = Assert.Throws<ArgumentNullException>(() => vm.MembersDialogParameters(null!));
    }

    [Fact]
    public void MembersDialogParameters_ContainsGroupAndCurrentUserId()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-123";
        var group = new UserGroupEntity { Id = 5, Name = "Test Group", OwnerId = "user-123" };

        var parameters = vm.MembersDialogParameters(group);

        Assert.Same(group, parameters[nameof(UserGroupMembersDialog.Group)]);
        Assert.Equal("user-123", parameters[nameof(UserGroupMembersDialog.CurrentUserId)]);
    }
}