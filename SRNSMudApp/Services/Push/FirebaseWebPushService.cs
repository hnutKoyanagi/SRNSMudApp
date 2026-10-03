#pragma warning disable CA1848, CA1873

namespace SRNSMudApp.Services.Push;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using FirebaseAdmin.Messaging;

using Google.Apis.Auth.OAuth2;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SRNSMudApp.Models.Push;

using WebPush;

/// <summary>
/// Firebase Cloud Messaging (FCM v1 API) を利用して Web ブラウザ（W3C Web Push）へのプッシュ通知配信を行うサービス実装。
/// Firebase の設定（ServiceAccountJson）が入力されている場合は FCM 経由で送信し、
/// 未設定の場合はローカル VAPID/WebPushClient に透過的にフォールバックします。
/// </summary>
public sealed class FirebaseWebPushService : IWebPushNotificationService
{
    private readonly IPushSubscriptionStore _subscriptionStore;
    private readonly FirebaseOptions _firebaseOptions;
    private readonly VapidOptions _vapidOptions;
    private readonly ILogger<FirebaseWebPushService> _logger;
    private readonly FirebaseMessaging? _messaging;

    /// <summary>
    /// コンストラクタ。DI コンテナから設定・ストア・ロガーを注入します。
    /// テスト時は <paramref name="messaging"/> を直接渡してモック差し替え可能。
    /// </summary>
    public FirebaseWebPushService(
        IPushSubscriptionStore subscriptionStore,
        IOptions<FirebaseOptions> firebaseOptions,
        IOptions<VapidOptions> vapidOptions,
        ILogger<FirebaseWebPushService> logger,
        FirebaseMessaging? messaging = null)
    {
        _subscriptionStore = subscriptionStore ?? throw new ArgumentNullException(nameof(subscriptionStore));
        _firebaseOptions = firebaseOptions?.Value ?? throw new ArgumentNullException(nameof(firebaseOptions));
        _vapidOptions = vapidOptions?.Value ?? throw new ArgumentNullException(nameof(vapidOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (messaging != null)
        {
            // テスト用インジェクション
            _messaging = messaging;
        }
        else if (_firebaseOptions.IsConfigured)
        {
            _messaging = InitializeFirebaseMessaging(_firebaseOptions, _logger);
        }
        else
        {
            _logger.LogInformation("Firebase の接続情報が未設定のため、直接 WebPush 送信モード（VAPID）で動作します。");
            _messaging = null;
        }
    }

    /// <summary>
    /// Firebase Messaging 経由で送信しているかどうか。
    /// </summary>
    public bool IsUsingFirebase => _messaging != null;

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

        _logger.LogInformation("プッシュ通知配信完了 (Firebase={IsFirebase}): 成功={Succeeded}, 失敗={Failed}, 失効削除={Expired}",
            IsUsingFirebase, succeeded, failed, expired);
        return new PushSendResult(succeeded, failed, expired);
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catching unexpected exceptions during client send to log and return false")]
    public async Task<bool> SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(payload);

        string jsonPayload = JsonSerializer.Serialize(payload);

        // 1. Firebase FCM v1 API 経由での送信（WebPushConfig を使ってブラウザ Web Push）
        if (_messaging != null)
        {
            try
            {
                var message = new Message
                {
                    // FCM v1 の WebPushConfig でブラウザの W3C Web Push エンドポイントへ送信
                    Webpush = new WebpushConfig
                    {
                        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            // 暗号化キー情報をヘッダーとして付与
                            { "Encryption-Key", subscription.Keys.P256Dh },
                            { "Auth", subscription.Keys.Auth }
                        },
                        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "payload", jsonPayload }
                        },
                        Notification = new WebpushNotification
                        {
                            Title = payload.Title,
                            Body = payload.Body,
                            Icon = payload.Icon
                        },
                        FcmOptions = new WebpushFcmOptions
                        {
                            Link = payload.Url
                        }
                    },
                    // ブラウザの PushSubscription.endpoint は FCM トークンを含む URL であるため、
                    // エンドポイント URL から FCM トークン部分を抽出して Token に設定する。
                    Token = ExtractFcmTokenFromEndpoint(subscription.Endpoint)
                };

                // FCM トークンが抽出できない（非 FCM エンドポイント）場合は VAPID フォールバックへ
                if (string.IsNullOrWhiteSpace(message.Token))
                {
                    _logger.LogDebug("エンドポイントから FCM トークンを抽出できませんでした。VAPID フォールバックを使用します: {Endpoint}", subscription.Endpoint);
                    return await SendViaVapidAsync(subscription, jsonPayload, cancellationToken);
                }

                string messageId = await _messaging.SendAsync(message, cancellationToken);
                _logger.LogDebug("Firebase FCM 送信完了: MessageId={MessageId}, Endpoint={Endpoint}", messageId, subscription.Endpoint);
                return true;
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
            {
                // FCM トークンが無効・登録解除済みの場合は Gone 相当として上位で削除処理できるよう
                // WebPushException (410 Gone) に変換してスローする
                _logger.LogWarning("FCM トークンが無効化されています。サブスクリプションを削除します: {Endpoint}", subscription.Endpoint);
                var pushSub = new PushSubscription(subscription.Endpoint, subscription.Keys.P256Dh, subscription.Keys.Auth);
                throw new WebPushException("FCM token is unregistered.", pushSub, new HttpResponseMessage(HttpStatusCode.Gone));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firebase FCM 経由のプッシュ通知送信に失敗しました: {Endpoint}", subscription.Endpoint);
                return false;
            }
        }

        // 2. ローカル VAPID/WebPushClient へのフォールバック送信
        return await SendViaVapidAsync(subscription, jsonPayload, cancellationToken);
    }

    /// <summary>
    /// VAPID 認証を使用して直接 Web Push エンドポイントへ通知を送信します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catching unexpected exceptions during WebPush send to log and return false")]
    private async Task<bool> SendViaVapidAsync(PushSubscriptionDto subscription, string jsonPayload, CancellationToken cancellationToken)
    {
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
            _logger.LogDebug("VAPID WebPushClient でプッシュ通知を送信しました: {Endpoint}", subscription.Endpoint);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            // 上位の一括配信処理でハンドリングできるようにリスロー
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プッシュ通知の個別送信に失敗しました: {Endpoint}", subscription.Endpoint);
            return false;
        }
    }

    /// <summary>
    /// W3C PushSubscription の endpoint URL から FCM 登録トークンを抽出します。
    /// FCM エンドポイントは <c>https://fcm.googleapis.com/fcm/send/{token}</c> の形式です。
    /// 非 FCM エンドポイントの場合は null を返します。
    /// </summary>
    private static string? ExtractFcmTokenFromEndpoint(string endpoint)
    {
        // FCM v1 Web Push エンドポイントの形式:
        //   https://fcm.googleapis.com/fcm/send/<token>
        const string fcmPrefix = "https://fcm.googleapis.com/fcm/send/";
        if (endpoint.StartsWith(fcmPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string token = endpoint[fcmPrefix.Length..];
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        return null;
    }

    /// <summary>
    /// Firebase Admin SDK の FirebaseApp を初期化して FirebaseMessaging インスタンスを返します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Firebase initialization failure should fall back to VAPID mode, not crash the app")]
#pragma warning disable CS0618 // GoogleCredential.FromFile / FromJson は現時点で利用可能な標準 API のため使用
    private static FirebaseMessaging? InitializeFirebaseMessaging(FirebaseOptions options, ILogger logger)
    {
        try
        {
            // FirebaseApp は Singleton のため、既存インスタンスが存在すれば再利用する
            FirebaseAdmin.FirebaseApp? existingApp = null;
            try
            {
                existingApp = FirebaseAdmin.FirebaseApp.DefaultInstance;
            }
            catch (Exception)
            {
                // DefaultInstance が存在しない場合は例外が投げられる可能性があるため無視
            }

            if (existingApp == null)
            {
                GoogleCredential credential;

                // ServiceAccountJson がファイルパスかどうかを判定
                string serviceAccountValue = options.ServiceAccountJson!;
                if (File.Exists(serviceAccountValue))
                {
                    // ファイルパスとして扱う
                    credential = GoogleCredential.FromFile(serviceAccountValue)
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                    logger.LogInformation("Firebase サービスアカウントをファイルから初期化します: {Path}", serviceAccountValue);
                }
                else
                {
                    // JSON 文字列として扱う
                    credential = GoogleCredential.FromJson(serviceAccountValue)
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                    logger.LogInformation("Firebase サービスアカウントを JSON 文字列から初期化します。");
                }

                var appOptions = new FirebaseAdmin.AppOptions
                {
                    Credential = credential,
                    ProjectId = options.ProjectId
                };

                _ = FirebaseAdmin.FirebaseApp.Create(appOptions);
                logger.LogInformation("Firebase アプリを初期化しました。");
            }
            else
            {
                logger.LogInformation("既存の Firebase アプリ インスタンスを再利用します。");
            }

            return FirebaseMessaging.DefaultInstance;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Firebase アプリの初期化に失敗しました。VAPID フォールバックモードで動作します。");
            return null;
        }
    }
#pragma warning restore CS0618
}

