using System.Security.Claims;

using Microsoft.JSInterop;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.UI;

using Xunit;

#pragma warning disable BL0016

namespace SRNSMudApp.Tests.Components.UI;

public class PushNotificationPromptViewModelTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    private PushNotificationPromptViewModel CreateSut()
    {
        return new PushNotificationPromptViewModel(_jsRuntimeMock.Object, _snackbarMock.Object);
    }

    [Fact]
    public async Task LoadSubscriptionStatusAsync_WhenAuthenticatedUser_SetsUserIdAndStatus()
    {
        // Arrange
        var sut = CreateSut();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscriptionStatusResult>(
                "PushNotificationInterop.getSubscriptionStatus",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SubscriptionStatusResult(true, "granted", true));

        // Act
        await sut.LoadSubscriptionStatusAsync(principal);

        // Assert
        Assert.Equal("user-123", sut.UserId);
        Assert.True(sut.IsSupported);
        Assert.Equal("granted", sut.Permission);
        Assert.True(sut.IsSubscribed);
    }

    [Fact]
    public async Task LoadSubscriptionStatusAsync_WhenJsThrows_CatchesAndMaintainsDefaultState()
    {
        // Arrange
        var sut = CreateSut();
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscriptionStatusResult>(
                "PushNotificationInterop.getSubscriptionStatus",
                It.IsAny<object?[]?>()))
            .ThrowsAsync(new JSException("Browser error"));

        // Act & Assert (Should not throw)
        await sut.LoadSubscriptionStatusAsync(null);
        Assert.True(sut.IsSupported);
        Assert.Equal("default", sut.Permission);
        Assert.False(sut.IsSubscribed);
    }

    [Fact]
    public async Task RequestPushPermissionAsync_WhenSuccess_SetsSubscribedAndGranted()
    {
        // Arrange
        var sut = CreateSut();
        sut.UserId = "user-123";

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscribeResult>(
                "PushNotificationInterop.enablePushNotifications",
                It.Is<object?[]?>(args => args != null && args.Length == 1 && (string?)args[0] == "user-123")))
            .ReturnsAsync(new PushNotificationPromptViewModel.SubscribeResult(true, null, "https://push.example.com"));

        // Act
        await sut.RequestPushPermissionAsync();

        // Assert
        Assert.True(sut.IsSubscribed);
        Assert.Equal("granted", sut.Permission);
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("プッシュ通知を有効化しました！", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task RequestPushPermissionAsync_WhenFailed_ShowsWarningAndReloadsStatus()
    {
        // Arrange
        var sut = CreateSut();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscribeResult>(
                "PushNotificationInterop.enablePushNotifications",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SubscribeResult(false, "Permission denied by user", null));

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscriptionStatusResult>(
                "PushNotificationInterop.getSubscriptionStatus",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SubscriptionStatusResult(true, "denied", false));

        // Act
        await sut.RequestPushPermissionAsync();

        // Assert
        Assert.False(sut.IsSubscribed);
        Assert.Equal("denied", sut.Permission);
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("Permission denied by user", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public async Task RequestPushPermissionAsync_WhenExceptionThrown_ShowsErrorSnackbar()
    {
        // Arrange
        var sut = CreateSut();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SubscribeResult>(
                "PushNotificationInterop.enablePushNotifications",
                It.IsAny<object?[]?>()))
            .ThrowsAsync(new JSException("Connection lost"));

        // Act
        await sut.RequestPushPermissionAsync();

        // Assert
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(msg => msg.Contains("Connection lost", StringComparison.Ordinal)), Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenSuccess_SetsSubscribedFalseAndShowsInfo()
    {
        // Arrange
        var sut = CreateSut();
        sut.IsSubscribed = true;

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SimpleResult>(
                "PushNotificationInterop.unsubscribe",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SimpleResult(true, "Unsubscribed", null));

        // Act
        await sut.UnsubscribeAsync();

        // Assert
        Assert.False(sut.IsSubscribed);
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("プッシュ通知の購読を解除しました。", Severity.Info, null, null), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenFailed_ShowsError()
    {
        // Arrange
        var sut = CreateSut();
        sut.IsSubscribed = true;

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SimpleResult>(
                "PushNotificationInterop.unsubscribe",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SimpleResult(false, null, "Failed to unsubscribe"));

        // Act
        await sut.UnsubscribeAsync();

        // Assert
        Assert.True(sut.IsSubscribed);
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("Failed to unsubscribe", Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenSuccess_ShowsSuccessMessage()
    {
        // Arrange
        var sut = CreateSut();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SimpleResult>(
                "PushNotificationInterop.sendTestNotification",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SimpleResult(true, "Sent", null));

        // Act
        await sut.SendTestNotificationAsync();

        // Assert
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("Sent", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenFailed_ShowsErrorMessage()
    {
        // Arrange
        var sut = CreateSut();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<PushNotificationPromptViewModel.SimpleResult>(
                "PushNotificationInterop.sendTestNotification",
                It.IsAny<object?[]?>()))
            .ReturnsAsync(new PushNotificationPromptViewModel.SimpleResult(false, "Internal error", null));

        // Act
        await sut.SendTestNotificationAsync();

        // Assert
        Assert.False(sut.IsProcessing);
        _snackbarMock.Verify(s => s.Add("Internal error", Severity.Error, null, null), Times.Once);
    }
}