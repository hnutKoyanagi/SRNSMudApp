#region

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     <see cref="UserDataProvider" /> のBAN（利用停止）設定に関する単体テスト。
/// </summary>
public sealed class UserDataProviderBanTests
{
    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            storeMock.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private sealed class DummyDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new NotSupportedException();
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task SetUserBanStatusAsync_ShouldBanUserAndLockout()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var user = new ApplicationUser
        {
            Id = "user1",
            UserName = "target@example.com",
            IsBanned = false,
            LockoutEnabled = false,
            LockoutEnd = null
        };

        _ = userManagerMock.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
        _ = userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
        _ = userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = new UserDataProvider(new DummyDbContextFactory(), userManagerMock.Object);

        // Act
        var result = await sut.SetUserBanStatusAsync("user1", true, "不正利用の疑い");

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(user.IsBanned);
        Assert.NotNull(user.BannedAt);
        Assert.Equal("不正利用の疑い", user.BanReason);
        Assert.True(user.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        userManagerMock.Verify(m => m.UpdateSecurityStampAsync(user), Times.Once);
        userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetUserBanStatusAsync_ShouldUnbanUserAndClearLockout()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var user = new ApplicationUser
        {
            Id = "user2",
            UserName = "banned@example.com",
            IsBanned = true,
            BannedAt = DateTimeOffset.UtcNow.AddDays(-1),
            BanReason = "以前のBAN",
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue
        };

        _ = userManagerMock.Setup(m => m.FindByIdAsync("user2")).ReturnsAsync(user);
        _ = userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
        _ = userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = new UserDataProvider(new DummyDbContextFactory(), userManagerMock.Object);

        // Act
        var result = await sut.SetUserBanStatusAsync("user2", false);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(user.IsBanned);
        Assert.Null(user.BannedAt);
        Assert.Null(user.BanReason);
        Assert.Null(user.LockoutEnd);
        userManagerMock.Verify(m => m.UpdateSecurityStampAsync(user), Times.Once);
        userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetUserBanStatusAsync_CannotBanSystemUser()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var systemUser = new ApplicationUser
        {
            Id = "system",
            UserName = "system",
            IsBanned = false
        };

        _ = userManagerMock.Setup(m => m.FindByIdAsync("system")).ReturnsAsync(systemUser);

        var sut = new UserDataProvider(new DummyDbContextFactory(), userManagerMock.Object);

        // Act
        var result = await sut.SetUserBanStatusAsync("system", true);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Description.Contains("システムユーザー"));
        Assert.False(systemUser.IsBanned);
        userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SetUserBanStatusAsync_UserNotFound_ReturnsFailed()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        _ = userManagerMock.Setup(m => m.FindByIdAsync("not-exist")).ReturnsAsync((ApplicationUser?)null);

        var sut = new UserDataProvider(new DummyDbContextFactory(), userManagerMock.Object);

        // Act
        var result = await sut.SetUserBanStatusAsync("not-exist", true);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Description.Contains("見つかりません"));
    }
}