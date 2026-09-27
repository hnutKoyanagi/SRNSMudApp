namespace SRNSMudApp.Tests.Push;

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SRNSMudApp.Models.Push;
using SRNSMudApp.Services.Push;

using Xunit;

public class AzureNotificationHubPushServiceTests
{
    [Fact]
    public void BrowserNotification_InitializesWithCorrectPlatformAndHeaders()
    {
        // Arrange
        const string payload = "{\"title\":\"Hello\",\"body\":\"World\"}";
        var headers = new Dictionary<string, string>
        {
            { "P256DH", "test-p256dh" },
            { "Auth", "test-auth" }
        };

        // Act
        var notification = new BrowserNotification(payload, headers);

        // Assert
        Assert.Equal(payload, notification.Body);
        Assert.Equal("application/json;charset=utf-8", notification.ContentType);
        Assert.Equal("test-p256dh", notification.Headers["P256DH"]);
        Assert.Equal("test-auth", notification.Headers["Auth"]);

        // PlatformType (protected internal) は "browser" であること
        var prop = typeof(Notification).GetProperty("PlatformType", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal("browser", prop?.GetValue(notification));
    }

    [Fact]
    public void AzureNotificationHubOptions_IsConfigured_EvaluatesCorrectly()
    {
        // 1. 未設定
        var emptyOptions = new AzureNotificationHubOptions();
        Assert.False(emptyOptions.IsConfigured);

        // 2. 接続文字列のみ
        var connOnlyOptions = new AzureNotificationHubOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=xxx"
        };
        Assert.False(connOnlyOptions.IsConfigured);

        // 3. 両方設定済み
        var fullOptions = new AzureNotificationHubOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=xxx",
            HubName = "test-hub"
        };
        Assert.True(fullOptions.IsConfigured);
    }

    [Fact]
    public async Task AzureNotificationHubPushService_SendNotificationAsync_CallsHubClientDirectSend()
    {
        // Arrange
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockHubClient = new Mock<INotificationHubClient>();

        var sub = new PushSubscriptionDto(
            "https://fcm.googleapis.com/fcm/send/sample-endpoint",
            new PushSubscriptionKeysDto("sample-p256dh", "sample-auth")
        );
        var payload = new PushNotificationPayload("Test Title", "Test Message");

        mockHubClient.Setup(h => h.SendDirectNotificationAsync(
                It.IsAny<Notification>(),
                sub.Endpoint,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutcome());

        var anhOptions = Options.Create(new AzureNotificationHubOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=xxx",
            HubName = "my-hub"
        });
        var vapidOptions = Options.Create(new VapidOptions());

        var service = new AzureNotificationHubPushService(
            mockStore.Object,
            anhOptions,
            vapidOptions,
            NullLogger<AzureNotificationHubPushService>.Instance,
            mockHubClient.Object);

        // Act
        bool result = await service.SendNotificationAsync(sub, payload);

        // Assert
        Assert.True(result);
        Assert.True(service.IsUsingNotificationHub);
        mockHubClient.Verify(h => h.SendDirectNotificationAsync(
            It.Is<Notification>(n => n.Body.Contains("Test Title")),
            sub.Endpoint,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AzureNotificationHubPushService_SendNotificationToUserAsync_SendsOnlyToTargetUser()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var mockHubClient = new Mock<INotificationHubClient>();

        var userSub = new PushSubscriptionDto(
            "https://example.com/push/user-sub",
            new PushSubscriptionKeysDto("key1", "auth1")
        );
        var otherSub = new PushSubscriptionDto(
            "https://example.com/push/other-sub",
            new PushSubscriptionKeysDto("key2", "auth2")
        );

        await store.AddOrUpdateAsync(userSub, "user-123");
        await store.AddOrUpdateAsync(otherSub, "other-user");

        mockHubClient.Setup(h => h.SendDirectNotificationAsync(
                It.IsAny<Notification>(),
                userSub.Endpoint,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutcome());

        var anhOptions = Options.Create(new AzureNotificationHubOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=xxx",
            HubName = "my-hub"
        });
        var vapidOptions = Options.Create(new VapidOptions());

        var service = new AzureNotificationHubPushService(
            store,
            anhOptions,
            vapidOptions,
            NullLogger<AzureNotificationHubPushService>.Instance,
            mockHubClient.Object);

        // Act
        var result = await service.SendNotificationToUserAsync("user-123", new PushNotificationPayload("Title", "Body"));

        // Assert
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        mockHubClient.Verify(h => h.SendDirectNotificationAsync(
            It.IsAny<Notification>(),
            userSub.Endpoint,
            It.IsAny<CancellationToken>()), Times.Once);
        mockHubClient.Verify(h => h.SendDirectNotificationAsync(
            It.IsAny<Notification>(),
            otherSub.Endpoint,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AzureNotificationHubPushService_WhenUnconfigured_FallsBackToDirectWebPushMode()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var anhOptions = Options.Create(new AzureNotificationHubOptions());
        var vapidOptions = Options.Create(new VapidOptions
        {
            Subject = "mailto:admin@example.com",
            PublicKey = "pubkey",
            PrivateKey = "privkey"
        });

        var service = new AzureNotificationHubPushService(
            store,
            anhOptions,
            vapidOptions,
            NullLogger<AzureNotificationHubPushService>.Instance);

        // Act & Assert
        Assert.False(service.IsUsingNotificationHub);
        var result = await service.SendNotificationToAllAsync(new PushNotificationPayload("T", "B"));
        Assert.Equal(0, result.SucceededCount);
    }

    [Fact]
    public void AzureNotificationHubOptions_And_VapidOptions_BindFromEnvironmentVariables_Correctly()
    {
        // Arrange: App Service の環境変数形式 (ダブルアンダースコア) を検証
        try
        {
            Environment.SetEnvironmentVariable("AzureNotificationHub__ConnectionString", "Endpoint=sb://env-hub.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=secret");
            Environment.SetEnvironmentVariable("AzureNotificationHub__HubName", "env-hub-name");
            Environment.SetEnvironmentVariable("Vapid__Subject", "mailto:env-admin@example.com");
            Environment.SetEnvironmentVariable("Vapid__PublicKey", "env-pubkey");
            Environment.SetEnvironmentVariable("Vapid__PrivateKey", "env-privkey");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var anhOptions = new AzureNotificationHubOptions();
            configuration.GetSection(AzureNotificationHubOptions.SectionName).Bind(anhOptions);

            var vapidOptions = new VapidOptions();
            configuration.GetSection(VapidOptions.SectionName).Bind(vapidOptions);

            // Assert
            Assert.True(anhOptions.IsConfigured);
            Assert.Equal("Endpoint=sb://env-hub.servicebus.windows.net/;SharedAccessKeyName=Default;SharedAccessKey=secret", anhOptions.ConnectionString);
            Assert.Equal("env-hub-name", anhOptions.HubName);

            Assert.Equal("mailto:env-admin@example.com", vapidOptions.Subject);
            Assert.Equal("env-pubkey", vapidOptions.PublicKey);
            Assert.Equal("env-privkey", vapidOptions.PrivateKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AzureNotificationHub__ConnectionString", null);
            Environment.SetEnvironmentVariable("AzureNotificationHub__HubName", null);
            Environment.SetEnvironmentVariable("Vapid__Subject", null);
            Environment.SetEnvironmentVariable("Vapid__PublicKey", null);
            Environment.SetEnvironmentVariable("Vapid__PrivateKey", null);
        }
    }
}