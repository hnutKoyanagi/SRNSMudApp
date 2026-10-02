namespace SRNSMudApp.Tests.Components.UI;

using System.Threading.Tasks;

using Moq;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Services;

using Xunit;

public class NotificationBadgeViewModelTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private NotificationBadgeViewModel CreateViewModel()
    {
        return new NotificationBadgeViewModel(_notificationServiceMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenUserNull_SetsUnreadCountZeroAndNotVisible()
    {
        var vm = CreateViewModel();

        await vm.InitializeAsync(null, "https://localhost/");

        Assert.Null(vm.CurrentUserId);
        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.IsVisible);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthenticatedOnRegularPage_FetchesUnreadCount()
    {
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(5);

        var vm = CreateViewModel();

        await vm.InitializeAsync("u1", "https://localhost/Item/ItemList");

        Assert.Equal("u1", vm.CurrentUserId);
        Assert.Equal(5, vm.UnreadCount);
        Assert.True(vm.IsVisible);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync("u1"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenOnNotificationsPage_SetsUnreadCountZero()
    {
        var vm = CreateViewModel();

        await vm.InitializeAsync("u1", "https://localhost/notifications");

        Assert.Equal("u1", vm.CurrentUserId);
        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.IsVisible);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnLocationChangedAsync_WhenSameBasePath_ReturnsFalseAndSkipsFetch()
    {
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(3);

        var vm = CreateViewModel();
        await vm.InitializeAsync("u1", "https://localhost/Item/ItemList?page=1");

        // Act - 同じベースパスでクエリのみ変更
        var changed = await vm.OnLocationChangedAsync("https://localhost/Item/ItemList?page=2");

        // Assert
        Assert.False(changed);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync("u1"), Times.Once); // 初期化時のみ
    }

    [Fact]
    public async Task OnLocationChangedAsync_WhenDifferentBasePath_ReturnsTrueAndRefreshes()
    {
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(3);

        var vm = CreateViewModel();
        await vm.InitializeAsync("u1", "https://localhost/Item/ItemList");

        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(4);

        // Act - 異なるベースパスへ遷移
        var changed = await vm.OnLocationChangedAsync("https://localhost/Tag/TagList");

        // Assert
        Assert.True(changed);
        Assert.Equal(4, vm.UnreadCount);
        _notificationServiceMock.Verify(s => s.GetUnreadCountAsync("u1"), Times.Exactly(2));
    }

    [Fact]
    public async Task OnLocationChangedAsync_WhenNavigatingToNotifications_SetsZero()
    {
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(3);

        var vm = CreateViewModel();
        await vm.InitializeAsync("u1", "https://localhost/Item/ItemList");
        Assert.Equal(3, vm.UnreadCount);

        // Act - 通知画面へ遷移
        var changed = await vm.OnLocationChangedAsync("https://localhost/notifications");

        // Assert
        Assert.True(changed);
        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public async Task RefreshAsync_RefreshesUnreadCount()
    {
        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(2);

        var vm = CreateViewModel();
        await vm.InitializeAsync("u1", "https://localhost/Item/ItemList");
        Assert.Equal(2, vm.UnreadCount);

        _notificationServiceMock.Setup(s => s.GetUnreadCountAsync("u1"))
            .ReturnsAsync(7);

        // Act
        await vm.RefreshAsync("https://localhost/Item/ItemList");

        // Assert
        Assert.Equal(7, vm.UnreadCount);
    }

    [Theory]
    [InlineData("https://localhost/notifications", true)]
    [InlineData("https://localhost/notifications?tab=1", true)]
    [InlineData("/notifications", true)]
    [InlineData("https://localhost/notifications/", true)]
    [InlineData("https://localhost/Item/ItemList", false)]
    [InlineData("", false)]
    public void IsNotificationsPage_DetectsNotificationsPath(string uri, bool expected)
    {
        var result = NotificationBadgeViewModel.IsNotificationsPage(uri);
        Assert.Equal(expected, result);
    }
}