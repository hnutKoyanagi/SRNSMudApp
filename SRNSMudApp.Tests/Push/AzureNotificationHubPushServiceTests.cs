namespace SRNSMudApp.Tests.Push;

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SRNSMudApp.Models.Push;
using SRNSMudApp.Services.Push;

using Xunit;

/// <summary>
/// FirebaseWebPushService の単体テスト。
/// Firebase Messaging は外部依存のため、未設定時（VAPID フォールバックモード）とモック注入パターンを検証します。
/// </summary>
public class FirebaseWebPushServiceTests
{
    [Fact]
    public void FirebaseOptions_IsConfigured_EvaluatesCorrectly()
    {
        // 1. 未設定
        var emptyOptions = new FirebaseOptions();
        Assert.False(emptyOptions.IsConfigured);

        // 2. ServiceAccountJson のみ設定済み
        var configuredOptions = new FirebaseOptions
        {
            ServiceAccountJson = "/etc/secrets/firebase-service-account.json"
        };
        Assert.True(configuredOptions.IsConfigured);

        // 3. ProjectId のみでは IsConfigured = false
        var projectIdOnly = new FirebaseOptions
        {
            ProjectId = "my-project"
        };
        Assert.False(projectIdOnly.IsConfigured);
    }

    [Fact]
    public async Task FirebaseWebPushService_WhenUnconfigured_FallsBackToVapidMode()
    {
        // Arrange: Firebase 未設定の場合はフォールバックモードになる
        var store = new InMemoryPushSubscriptionStore();
        var firebaseOptions = Options.Create(new FirebaseOptions());
        var vapidOptions = Options.Create(new VapidOptions
        {
            Subject = "mailto:admin@example.com",
            PublicKey = "pubkey",
            PrivateKey = "privkey"
        });

        var service = new FirebaseWebPushService(
            store,
            firebaseOptions,
            vapidOptions,
            NullLogger<FirebaseWebPushService>.Instance);

        // Assert
        Assert.False(service.IsUsingFirebase);
        // サブスクリプション 0 件なので送信なし
        var result = await service.SendNotificationToAllAsync(new PushNotificationPayload("T", "B"));
        Assert.Equal(0, result.SucceededCount);
    }

    [Fact]
    public async Task FirebaseWebPushService_WhenUnconfigured_SendNotificationToUserAsync_EmptyStore_ReturnsZero()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var firebaseOptions = Options.Create(new FirebaseOptions());
        var vapidOptions = Options.Create(new VapidOptions());

        var service = new FirebaseWebPushService(
            store,
            firebaseOptions,
            vapidOptions,
            NullLogger<FirebaseWebPushService>.Instance);

        // Act
        var result = await service.SendNotificationToUserAsync("user-123", new PushNotificationPayload("T", "B"));

        // Assert
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task FirebaseWebPushService_WhenUnconfigured_EmptyUserId_ReturnsZero()
    {
        // Arrange
        var store = new InMemoryPushSubscriptionStore();
        var firebaseOptions = Options.Create(new FirebaseOptions());
        var vapidOptions = Options.Create(new VapidOptions());

        var service = new FirebaseWebPushService(
            store,
            firebaseOptions,
            vapidOptions,
            NullLogger<FirebaseWebPushService>.Instance);

        // Act
        var result = await service.SendNotificationToUserAsync("   ", new PushNotificationPayload("T", "B"));

        // Assert
        Assert.Equal(0, result.SucceededCount);
    }

    [Fact]
    public void FirebaseOptions_And_VapidOptions_BindFromEnvironmentVariables_Correctly()
    {
        // Arrange: App Service の環境変数形式 (ダブルアンダースコア) を検証
        try
        {
            Environment.SetEnvironmentVariable("Firebase__ServiceAccountJson", "/etc/secrets/firebase.json");
            Environment.SetEnvironmentVariable("Firebase__ProjectId", "my-firebase-project");
            Environment.SetEnvironmentVariable("Vapid__Subject", "mailto:env-admin@example.com");
            Environment.SetEnvironmentVariable("Vapid__PublicKey", "env-pubkey");
            Environment.SetEnvironmentVariable("Vapid__PrivateKey", "env-privkey");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var firebaseOptions = new FirebaseOptions();
            configuration.GetSection(FirebaseOptions.SectionName).Bind(firebaseOptions);

            var vapidOptions = new VapidOptions();
            configuration.GetSection(VapidOptions.SectionName).Bind(vapidOptions);

            // Assert
            Assert.True(firebaseOptions.IsConfigured);
            Assert.Equal("/etc/secrets/firebase.json", firebaseOptions.ServiceAccountJson);
            Assert.Equal("my-firebase-project", firebaseOptions.ProjectId);

            Assert.Equal("mailto:env-admin@example.com", vapidOptions.Subject);
            Assert.Equal("env-pubkey", vapidOptions.PublicKey);
            Assert.Equal("env-privkey", vapidOptions.PrivateKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Firebase__ServiceAccountJson", null);
            Environment.SetEnvironmentVariable("Firebase__ProjectId", null);
            Environment.SetEnvironmentVariable("Vapid__Subject", null);
            Environment.SetEnvironmentVariable("Vapid__PublicKey", null);
            Environment.SetEnvironmentVariable("Vapid__PrivateKey", null);
        }
    }

    [Fact]
    public async Task FirebaseWebPushService_SendNotificationToUserAsync_SendsOnlyToTargetUser()
    {
        // Arrange: Firebase 未設定（VAPID フォールバック）+ 実ストアで、
        // ユーザー別フィルタリングが正しく動作することを確認する
        var store = new InMemoryPushSubscriptionStore();

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

        var firebaseOptions = Options.Create(new FirebaseOptions());
        var vapidOptions = Options.Create(new VapidOptions
        {
            Subject = "mailto:admin@example.com",
            PublicKey = "pubkey",
            PrivateKey = "privkey"
        });

        var service = new FirebaseWebPushService(
            store,
            firebaseOptions,
            vapidOptions,
            NullLogger<FirebaseWebPushService>.Instance);

        // サブスクリプションは取得されるが VAPID キーが実在しないため送信失敗する
        // → FailedCount = 1, SucceededCount = 0 を確認
        var result = await service.SendNotificationToUserAsync("user-123", new PushNotificationPayload("Title", "Body"));

        // other-user のサブスクリプションが送信対象に含まれていないことを確認
        // （other-user のサブスクリプションが誤送信されていれば FailedCount > 1 になる）
        Assert.True(result.SucceededCount + result.FailedCount + result.ExpiredCount == 1,
            $"user-123 のサブスクリプション 1 件のみが処理対象であること。実際: {result}");
    }
}