#pragma warning disable CA1848, CA1873

namespace SRNSMudApp.Services.Push;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SRNSMudApp.Models.Push;

using WebPush;

/// <summary>
/// Azure Notification Hubs (ANH) を利用して Web ブラウザ（W3C Web Push）へのプッシュ通知配信を行うサービス実装。
/// Azure Notification Hubs の接続情報が設定されている場合は ANH 経由で送信し、未設定の場合はローカル WebPush に透過的にフォールバックします。
/// </summary>
public sealed class AzureNotificationHubPushService : IWebPushNotificationService
{
    private readonly IPushSubscriptionStore _subscriptionStore;
    private readonly AzureNotificationHubOptions _anhOptions;
    private readonly VapidOptions _vapidOptions;
    private readonly ILogger<AzureNotificationHubPushService> _logger;
    private readonly INotificationHubClient? _hubClient;

    /// <summary>
    /// コンストラクタ。DI コンテナから設定・ストア・ロガーを注入します。
    /// </summary>
    public AzureNotificationHubPushService(
        IPushSubscriptionStore subscriptionStore,
        IOptions<AzureNotificationHubOptions> anhOptions,
        IOptions<VapidOptions> vapidOptions,
        ILogger<AzureNotificationHubPushService> logger,
        INotificationHubClient? hubClient = null)
    {
        _subscriptionStore = subscriptionStore ?? throw new ArgumentNullException(nameof(subscriptionStore));
        _anhOptions = anhOptions?.Value ?? throw new ArgumentNullException(nameof(anhOptions));
        _vapidOptions = vapidOptions?.Value ?? throw new ArgumentNullException(nameof(vapidOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (hubClient != null)
        {
            _hubClient = hubClient;
        }
        else if (_anhOptions.IsConfigured)
        {
            _logger.LogInformation("Azure Notification Hub クライアントを初期化します: HubName={HubName}", _anhOptions.HubName);
            _hubClient = NotificationHubClient.CreateClientFromConnectionString(
                _anhOptions.ConnectionString,
                _anhOptions.HubName);
        }
        else
        {
            _logger.LogInformation("Azure Notification Hub の接続情報が未設定のため、直接 WebPush 送信モードで動作します。");
            _hubClient = null;
        }
    }

    /// <summary>
    /// Azure Notification Hub 経由で通知送信を行っているかどうか。
    /// </summary>
    public bool IsUsingNotificationHub => _hubClient != null;

    /// <inheritdoc />
    public async Task<PushSendResult> SendNotificationToAllAsync(PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var subscriptions = await _subscriptionStore.GetAllAsync(cancellationToken);
        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("プッシュ通知送信対象のサブスクリプションが存在しません。");
            return new PushSendResult(0, 0, 0);
        }

        return await SendToSubscriptionsAsync(subscriptions, payload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PushSendResult> SendNotificationToUserAsync(string userId, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new PushSendResult(0, 0, 0);
        }

        var subscriptions = await _subscriptionStore.GetByUserIdAsync(userId, cancellationToken);
        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("ユーザー {UserId} 宛てのプッシュ通知サブスクリプションが存在しません。", userId);
            return new PushSendResult(0, 0, 0);
        }

        return await SendToSubscriptionsAsync(subscriptions, payload, cancellationToken);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Individual client push failure should not abort remaining notifications")]
    private async Task<PushSendResult> SendToSubscriptionsAsync(
        IReadOnlyCollection<PushSubscriptionDto> subscriptions,
        PushNotificationPayload payload,
        CancellationToken cancellationToken)
    {
        int succeeded = 0;
        int failed = 0;
        int expired = 0;

        foreach (var sub in subscriptions)
        {
            try
            {
                bool success = await SendNotificationAsync(sub, payload, cancellationToken);
                if (success)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                }
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                expired++;
                _logger.LogWarning("サブスクリプションが無効化・失効しているため削除します: {Endpoint}", sub.Endpoint);
                await _subscriptionStore.RemoveAsync(sub.Endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "プッシュ通知送信中にエラーが発生しました: {Endpoint}", sub.Endpoint);
            }
        }

        _logger.LogInformation("プッシュ通知配信完了 (HubActive={IsHub}): 成功={Succeeded}, 失敗={Failed}, 失効削除={Expired}",
            IsUsingNotificationHub, succeeded, failed, expired);
        return new PushSendResult(succeeded, failed, expired);
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catching unexpected exceptions during client send to log and return false")]
    public async Task<bool> SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(payload);

        string jsonPayload = JsonSerializer.Serialize(payload);

        // 1. Azure Notification Hub 経由での送信
        if (_hubClient != null)
        {
            try
            {
                var browserPushHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "P256DH", subscription.Keys.P256Dh },
                    { "Auth", subscription.Keys.Auth }
                };

                var notification = new BrowserNotification(jsonPayload, browserPushHeaders);

                var outcome = await _hubClient.SendDirectNotificationAsync(
                    notification,
                    subscription.Endpoint,
                    cancellationToken);

                _logger.LogDebug("Azure Notification Hub Direct Send 完了: Endpoint={Endpoint}, State={State}",
                    subscription.Endpoint, outcome?.State);

                return outcome == null || outcome.State is NotificationOutcomeState.Completed or NotificationOutcomeState.Enqueued;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Notification Hub 経由のプッシュ通知送信に失敗しました: {Endpoint}", subscription.Endpoint);
                return false;
            }
        }

        // 2. ローカル WebPushClient へのフォールバック送信
        if (string.IsNullOrWhiteSpace(_vapidOptions.PublicKey) || string.IsNullOrWhiteSpace(_vapidOptions.PrivateKey))
        {
            throw new InvalidOperationException("VAPIDキー（PublicKey / PrivateKey）が設定されていません。");
        }

        using var client = new WebPushClient();
        var vapidDetails = new VapidDetails(
            _vapidOptions.Subject,
            _vapidOptions.PublicKey,
            _vapidOptions.PrivateKey);

        var pushSubscription = new PushSubscription(
            subscription.Endpoint,
            subscription.Keys.P256Dh,
            subscription.Keys.Auth);

        try
        {
            await client.SendNotificationAsync(pushSubscription, jsonPayload, vapidDetails, cancellationToken);
            _logger.LogDebug("ローカル WebPushClient でプッシュ通知を送信しました: {Endpoint}", subscription.Endpoint);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プッシュ通知の個別送信に失敗しました: {Endpoint}", subscription.Endpoint);
            return false;
        }
    }
}