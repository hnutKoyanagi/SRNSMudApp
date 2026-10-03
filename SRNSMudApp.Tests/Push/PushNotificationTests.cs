namespace SRNSMudApp.Tests.Push;

using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SRNSMudApp.Controllers;
using SRNSMudApp.Models.Push;
using SRNSMudApp.Services.Push;

using WebPush;

using Xunit;

public class PushNotificationTests
{
    [Fact]
    public void VapidHelper_CanGenerateValidKeys()
    {
        // Act
        var keys = VapidHelper.GenerateVapidKeys();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(keys.PublicKey));
        Assert.False(string.IsNullOrWhiteSpace(keys.PrivateKey));
    }

    [Fact]
    public async Task InMemoryPushSubscriptionStore_AddAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var dto = new PushSubscriptionDto(
            "https://fcm.googleapis.com/fcm/send/sample-token",
            new PushSubscriptionKeysDto("p256dh-key", "auth-key")
        );

        // Act
        await store.AddOrUpdateAsync(dto);
        var all = await store.GetAllAsync();

        // Assert
        Assert.Single(all);
        Assert.Contains(all, s => s.Endpoint == dto.Endpoint);

        // Remove
        await store.RemoveAsync(dto.Endpoint);
        var afterRemove = await store.GetAllAsync();
        Assert.Empty(afterRemove);
    }

    [Fact]
    public async Task PushNotificationController_GetVapidPublicKey_ReturnsConfiguredKey()
    {
        // Arrange
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions
        {
            Subject = "mailto:admin@example.com",
            PublicKey = "test-public-key",
            PrivateKey = "test-private-key"
        });

        var controller = new PushNotificationController(mockStore.Object, mockPush.Object, options);

        // Act
        var result = controller.GetVapidPublicKey() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var val = result.Value;
        Assert.NotNull(val);
        var property = val.GetType().GetProperty("publicKey");
        Assert.Equal("test-public-key", property?.GetValue(val));
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_ValidPayload_ReturnsOk()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions
        {
            Subject = "mailto:admin@example.com",
            PublicKey = "test-public-key",
            PrivateKey = "test-private-key"
        });

        var controller = new PushNotificationController(store, mockPush.Object, options);
        var dto = new PushSubscriptionDto(
            "https://example.com/push/123",
            new PushSubscriptionKeysDto("key1", "key2")
        );

        // Act
        var result = await controller.Subscribe(dto, default) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var stored = await store.GetAllAsync();
        Assert.Single(stored);
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_InvalidPayload_ReturnsBadRequest()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(store, mockPush.Object, options);

        // Act
        var result = await controller.Subscribe(new PushSubscriptionDto("", null!), default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PushNotificationController_Send_ValidPayload_ReturnsOk()
    {
        // Arrange
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockPush = new Mock<IWebPushNotificationService>();
        mockPush.Setup(p => p.SendNotificationToAllAsync(It.IsAny<PushNotificationPayload>(), default))
            .ReturnsAsync(new PushSendResult(1, 0, 0));

        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(mockStore.Object, mockPush.Object, options);

        // Act
        var result = await controller.SendNotification(new PushNotificationPayload("Title", "Body"), default) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        mockPush.Verify(p => p.SendNotificationToAllAsync(It.IsAny<PushNotificationPayload>(), default), Times.Once);
    }

    [Fact]
    public async Task WebPushNotificationService_SendNotificationToAllAsync_NoSubscriptions_ReturnsZero()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var options = Options.Create(new VapidOptions
        {
            Subject = "mailto:test@example.com",
            PublicKey = "pub",
            PrivateKey = "priv"
        });
        var service = new WebPushNotificationService(store, options, NullLogger<WebPushNotificationService>.Instance);

        // Act
        var result = await service.SendNotificationToAllAsync(new PushNotificationPayload("T", "B"));

        // Assert
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.ExpiredCount);
    }

    [Fact]
    public void PushNotificationController_SendNotification_HasAdminAuthorizeAttribute()
    {
        // Act: SEC-01 SendNotification に [Authorize(Roles = "Admin")] が付与されていることを確認
        var method = typeof(PushNotificationController).GetMethod(nameof(PushNotificationController.SendNotification));
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal("Admin", authorizeAttr.Roles);
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_WhenAuthenticated_UsesClaimUserId()
    {
        // Arrange: SEC-01 認証済みユーザーの場合、トークンの UserId（ClaimTypes.NameIdentifier）を使用する
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(mockStore.Object, mockPush.Object, options);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "auth-user-123") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var dto = new PushSubscriptionDto(
            "https://example.com/push/auth1",
            new PushSubscriptionKeysDto("p256", "auth"),
            UserId: "spoofed-user-id" // クライアントが別の UserId を詐称送信
        );

        // Act
        var result = await controller.Subscribe(dto, default) as OkObjectResult;

        // Assert: 詐称された spoofed-user-id ではなく認証済みの auth-user-123 が渡される
        Assert.NotNull(result);
        var expectedDto = dto with { UserId = "auth-user-123" };
        mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_WhenAuthenticatedWithSub_UsesSubClaimUserId()
    {
        // Arrange: SEC-01 sub クレームを持つ認証済みユーザー
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(mockStore.Object, mockPush.Object, options);

        var claims = new[] { new Claim("sub", "sub-user-456") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var dto = new PushSubscriptionDto(
            "https://example.com/push/auth2",
            new PushSubscriptionKeysDto("p256", "auth")
        );

        // Act
        var result = await controller.Subscribe(dto, default) as OkObjectResult;

        // Assert: sub クレームの値が使用される
        Assert.NotNull(result);
        var expectedDto = dto with { UserId = "sub-user-456" };
        mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, "sub-user-456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull()
    {
        // Arrange: SEC-01 未認証ユーザーがリクエストボディで UserId を指定しても詐称を防止して null を保存する
        var mockStore = new Mock<IPushSubscriptionStore>();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(mockStore.Object, mockPush.Object, options);

        // 未認証 HttpContext
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var dto = new PushSubscriptionDto(
            "https://example.com/push/anon",
            new PushSubscriptionKeysDto("p256", "auth"),
            UserId: "victim-user-id" // 詐称
        );

        // Act
        var result = await controller.Subscribe(dto, default) as OkObjectResult;

        // Assert: 未認証時は null が渡されること
        Assert.NotNull(result);
        var expectedDto = dto with { UserId = null };
        mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushNotificationController_Subscribe_WhenUnauthenticatedWithSpoofedUserId_RealStoreDoesNotIndexVictim()
    {
        // SEC-01: 実装のモック化による偽装パスを防ぐため、実ストア(InMemoryPushSubscriptionStore)を用いた結合ユニットテスト
        var realStore = new InMemoryPushSubscriptionStore();
        var mockPush = new Mock<IWebPushNotificationService>();
        var options = Options.Create(new VapidOptions());
        var controller = new PushNotificationController(realStore, mockPush.Object, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var attackerDto = new PushSubscriptionDto(
            "https://example.com/push/exploit-test",
            new PushSubscriptionKeysDto("p256", "auth"),
            UserId: "victim-account-id"
        );

        var result = await controller.Subscribe(attackerDto, default) as OkObjectResult;
        Assert.NotNull(result);

        var victimSubs = await realStore.GetByUserIdAsync("victim-account-id");
        Assert.Empty(victimSubs);

        var allSubs = await realStore.GetAllAsync();
        var stored = Assert.Single(allSubs);
        Assert.Null(stored.UserId);
    }
}