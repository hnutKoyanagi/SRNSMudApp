using Microsoft.AspNetCore.Identity;

using Moq;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.User;

/// <summary>
///     UserDetailActionViewModel の単体テスト。
///     bUnit を使わずにフォローのトグルや Admin 昇格ロジックを検証する。
/// </summary>
public sealed class UserDetailActionViewModelTests
{
    private const string CurrentUserId = "current-user-1";
    private const string TargetUserId = "target-user-2";

    private readonly Mock<IUserDataProvider> _userDataMock = new();
    private readonly Mock<IUserStore<ApplicationUser>> _userStoreMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly UserDetailActionViewModel _sut;

    public UserDetailActionViewModelTests()
    {
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            _userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _sut = new UserDetailActionViewModel(
            _userDataMock.Object,
            _userManagerMock.Object);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenValid_CallsUserDataProvider()
    {
        _userDataMock
            .Setup(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId))
            .ReturnsAsync(true);

        bool result = await _sut.ToggleFollowAsync(CurrentUserId, TargetUserId);

        Assert.True(result);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(CurrentUserId, TargetUserId), Times.Once);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenSelf_DoesNothing()
    {
        bool result = await _sut.ToggleFollowAsync(CurrentUserId, CurrentUserId);

        Assert.False(result);
        _userDataMock.Verify(u => u.ToggleFollowUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
}