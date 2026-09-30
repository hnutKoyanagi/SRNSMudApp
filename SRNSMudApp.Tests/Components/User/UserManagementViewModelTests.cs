using Microsoft.AspNetCore.Identity;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.User;

public sealed class UserManagementViewModelTests
{
    private const string CurrentAdminId = "admin-100";
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly UserManagementViewModel _sut;

    public UserManagementViewModelTests()
    {
        _userDataProviderMock.Setup(d => d.GetAllUsersAsync()).ReturnsAsync([]);
        _sut = new UserManagementViewModel(
            _userDataProviderMock.Object,
            _snackbarMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_LoadsUsersAndMapsRolesAndBanStatus()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "user-1", UserName = "alice", IsBanned = false },
            new() { Id = "user-2", UserName = "bob", IsBanned = true }
        };

        _userDataProviderMock.Setup(d => d.GetAllUsersAsync()).ReturnsAsync(users);
        _userDataProviderMock.Setup(d => d.IsUserInRoleAsync(users[0], "Admin")).ReturnsAsync(true);
        _userDataProviderMock.Setup(d => d.IsUserInRoleAsync(users[1], "Admin")).ReturnsAsync(false);

        await _sut.InitializeAsync(CurrentAdminId);

        Assert.Equal(CurrentAdminId, _sut.CurrentUserId);
        Assert.NotNull(_sut.Users);
        Assert.Equal(2, _sut.Users.Count);

        Assert.Equal("user-1", _sut.Users[0].User.Id);
        Assert.True(_sut.Users[0].IsAdmin);
        Assert.False(_sut.Users[0].IsBanned);

        Assert.Equal("user-2", _sut.Users[1].User.Id);
        Assert.False(_sut.Users[1].IsAdmin);
        Assert.True(_sut.Users[1].IsBanned);
    }

    [Theory]
    [InlineData("admin-100", "admin", true)]
    [InlineData("other-id", "system", true)]
    [InlineData("other-id", "SYSTEM", true)]
    [InlineData("other-id", "alice", false)]
    public async Task IsProtectedUser_ReturnsExpectedResult(string userId, string userName, bool expected)
    {
        await _sut.InitializeAsync(CurrentAdminId);
        var user = new ApplicationUser { Id = userId, UserName = userName };

        bool isProtected = _sut.IsProtectedUser(user);

        Assert.Equal(expected, isProtected);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenValueUnchanged_ReturnsFalse()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice" },
            IsAdmin = true
        };

        bool result = await _sut.ToggleAdminAsync(item, newIsAdmin: true);

        Assert.False(result);
        _userDataProviderMock.Verify(d => d.UpdateUserAdminRoleAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenProtectedUserAndDemoting_ShowsWarningAndReturnsFalse()
    {
        await _sut.InitializeAsync(CurrentAdminId);
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = CurrentAdminId, UserName = "myself" },
            IsAdmin = true
        };

        bool result = await _sut.ToggleAdminAsync(item, newIsAdmin: false);

        Assert.False(result);
        Assert.True(item.IsAdmin);
        _snackbarMock.Verify(s => s.Add("保護されたユーザーの管理者権限は剥奪できません。", Severity.Warning, null, null), Times.Once);
        _userDataProviderMock.Verify(d => d.UpdateUserAdminRoleAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenValid_UpdatesRoleAndShowsSuccess()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice" },
            IsAdmin = false
        };

        _userDataProviderMock
            .Setup(d => d.UpdateUserAdminRoleAsync("user-1", true))
            .ReturnsAsync(IdentityResult.Success);

        bool result = await _sut.ToggleAdminAsync(item, newIsAdmin: true);

        Assert.True(result);
        Assert.True(item.IsAdmin);
        _userDataProviderMock.Verify(d => d.UpdateUserAdminRoleAsync("user-1", true), Times.Once);
        _snackbarMock.Verify(s => s.Add("alice にAdmin権限を付与しました。", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenFailed_ShowsErrorSnackbarAndReturnsFalse()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice" },
            IsAdmin = false
        };

        _userDataProviderMock
            .Setup(d => d.UpdateUserAdminRoleAsync("user-1", true))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role error" }));

        bool result = await _sut.ToggleAdminAsync(item, newIsAdmin: true);

        Assert.False(result);
        Assert.False(item.IsAdmin);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(msg => msg.Contains("Role error")), Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task ToggleBanAsync_WhenValueUnchanged_ReturnsFalse()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice", IsBanned = false },
            IsBanned = false
        };

        bool result = await _sut.ToggleBanAsync(item, newIsBanned: false);

        Assert.False(result);
        _userDataProviderMock.Verify(d => d.SetUserBanStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleBanAsync_WhenProtectedUserAndBanning_ShowsWarningAndReturnsFalse()
    {
        await _sut.InitializeAsync(CurrentAdminId);
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = CurrentAdminId, UserName = "myself", IsBanned = false },
            IsBanned = false
        };

        bool result = await _sut.ToggleBanAsync(item, newIsBanned: true);

        Assert.False(result);
        Assert.False(item.IsBanned);
        _snackbarMock.Verify(s => s.Add("保護されたユーザーを利用停止にすることはできません。", Severity.Warning, null, null), Times.Once);
        _userDataProviderMock.Verify(d => d.SetUserBanStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleBanAsync_WhenValid_UpdatesBanAndShowsSnackbar()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice", IsBanned = false },
            IsBanned = false
        };

        _userDataProviderMock
            .Setup(d => d.SetUserBanStatusAsync("user-1", true, null))
            .ReturnsAsync(IdentityResult.Success);

        bool result = await _sut.ToggleBanAsync(item, newIsBanned: true);

        Assert.True(result);
        Assert.True(item.IsBanned);
        Assert.True(item.User.IsBanned);
        _userDataProviderMock.Verify(d => d.SetUserBanStatusAsync("user-1", true, null), Times.Once);
        _snackbarMock.Verify(s => s.Add("alice を利用停止（BAN）にしました。", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public async Task ToggleBanAsync_WhenFailed_ShowsErrorSnackbarAndReturnsFalse()
    {
        var item = new UserManagementItemViewModel
        {
            User = new ApplicationUser { Id = "user-1", UserName = "alice", IsBanned = false },
            IsBanned = false
        };

        _userDataProviderMock
            .Setup(d => d.SetUserBanStatusAsync("user-1", true, null))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Ban error" }));

        bool result = await _sut.ToggleBanAsync(item, newIsBanned: true);

        Assert.False(result);
        Assert.False(item.IsBanned);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(msg => msg.Contains("Ban error")), Severity.Error, null, null), Times.Once);
    }
}