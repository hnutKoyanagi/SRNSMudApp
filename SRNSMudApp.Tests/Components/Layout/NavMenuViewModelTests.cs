using Moq;

using SRNSMudApp.Components.Layout;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Layout;

/// <summary>
///     NavMenuViewModel の単体テスト。
///     通知数の取得、ロケーション変更に応じた未読数リセット、およびイベント購読解除を検証する。
/// </summary>
public sealed class NavMenuViewModelTests
{
    private const string CurrentUserId = "user-test-42";
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly NavMenuViewModel _sut;

    public NavMenuViewModelTests()
    {
        _sut = new NavMenuViewModel(_notificationServiceMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenUserHasUnread_UpdatesUnreadCount()
    {
        // Arrange
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(7);

        // Act
        await _sut.InitializeAsync(CurrentUserId, "https://localhost/item-list", "item-list");

        // Assert
        Assert.Equal(7, _sut.UnreadNotificationCount);
        Assert.Equal("item-list", _sut.CurrentUrl);
    }

    [Fact]
    public async Task InitializeAsync_WhenUserOnNotificationsPage_SetsUnreadCountToZero()
    {
        // Arrange
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(7);

        // Act
        await _sut.InitializeAsync(CurrentUserId, "https://localhost/notifications", "notifications");

        // Assert
        Assert.Equal(0, _sut.UnreadNotificationCount);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_WhenUserIdIsNull_SetsUnreadCountToZero()
    {
        // Act
        await _sut.InitializeAsync(null, "https://localhost/home", "home");

        // Assert
        Assert.Equal(0, _sut.UnreadNotificationCount);
    }

    [Fact]
    public async Task HandleLocationChangedAsync_WhenSamePath_DoesNotUpdateCount()
    {
        // Arrange
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(3);
        await _sut.InitializeAsync(CurrentUserId, "https://localhost/item-list?page=1", "item-list?page=1");

        bool eventFired = false;
        _sut.StateChanged += (_, _) => eventFired = true;

        // Act
        await _sut.HandleLocationChangedAsync("https://localhost/item-list?page=2", "item-list?page=2");

        // Assert
        Assert.False(eventFired);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync(CurrentUserId), Times.Once); // Initial only
    }

    [Fact]
    public async Task HandleLocationChangedAsync_WhenDifferentPath_UpdatesCountAndFiresEvent()
    {
        // Arrange
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(3);
        await _sut.InitializeAsync(CurrentUserId, "https://localhost/home", "home");

        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(5);

        bool eventFired = false;
        _sut.StateChanged += (_, _) => eventFired = true;

        // Act
        await _sut.HandleLocationChangedAsync("https://localhost/item-list", "item-list");

        // Assert
        Assert.True(eventFired);
        Assert.Equal(5, _sut.UnreadNotificationCount);
        Assert.Equal("item-list", _sut.CurrentUrl);
    }

    [Fact]
    public async Task OnNotificationsChanged_UpdatesCountAndFiresEvent()
    {
        // Arrange
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(2);
        await _sut.InitializeAsync(CurrentUserId, "https://localhost/home", "home");

        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync(CurrentUserId))
            .ReturnsAsync(8);

        bool eventFired = false;
        _sut.StateChanged += (_, _) => eventFired = true;

        // Act
        _notificationServiceMock.Raise(s => s.NotificationsChanged += null, EventArgs.Empty);

        // Assert
        // Event is raised asynchronously, wait a moment
        await Task.Delay(50);
        Assert.True(eventFired);
        Assert.Equal(8, _sut.UnreadNotificationCount);
    }

    [Fact]
    public void Dispose_UnsubscribesFromNotificationsChanged()
    {
        // Act
        _sut.Dispose();

        // Raising event after dispose should not throw or affect
        _notificationServiceMock.Raise(s => s.NotificationsChanged += null, EventArgs.Empty);
    }
}