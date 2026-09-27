namespace SRNSMudApp.Controllers;

using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using SRNSMudApp.Models.Push;
using SRNSMudApp.Services.Push;

/// <summary>
/// Web Push 通知の購読管理および配信リクエストを処理する API コントローラー。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PushNotificationController(
    IPushSubscriptionStore subscriptionStore,
    IWebPushNotificationService pushService,
    IOptions<VapidOptions> vapidOptions) : ControllerBase
{
    private readonly IPushSubscriptionStore _subscriptionStore = subscriptionStore ?? throw new ArgumentNullException(nameof(subscriptionStore));
    private readonly IWebPushNotificationService _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
    private readonly VapidOptions _vapidOptions = vapidOptions?.Value ?? throw new ArgumentNullException(nameof(vapidOptions));

    /// <summary>
    /// クライアントが PushManager.subscribe() を呼び出す際に必要な VAPID 公開鍵を取得します。
    /// </summary>
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        return Ok(new { publicKey = _vapidOptions.PublicKey });
    }

    /// <summary>
    /// フロントエンドから送信されたプッシュ購読情報（Endpoint, p256dh, auth, userId）を保存します。
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto? subscription, CancellationToken cancellationToken)
    {
        if (subscription == null ||
            string.IsNullOrWhiteSpace(subscription.Endpoint) ||
            subscription.Keys == null ||
            string.IsNullOrWhiteSpace(subscription.Keys.P256Dh) ||
            string.IsNullOrWhiteSpace(subscription.Keys.Auth))
        {
            return BadRequest(new { message = "無効なサブスクリプション情報です。" });
        }

        // ログイン中のユーザーIDを優先、なければリクエストボディのUserIdを使用
        string? userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? subscription.UserId;

        await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
        return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
    }

    /// <summary>
    /// クライアントのプッシュ購読解除を処理します。
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { message = "エンドポイントが指定されていません。" });
        }

        await _subscriptionStore.RemoveAsync(request.Endpoint, cancellationToken);
        return Ok(new { message = "サブスクリプションを解除しました。" });
    }

    /// <summary>
    /// 保存されている全購読者に対してプッシュ通知を送信します。
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] PushNotificationPayload? payload, CancellationToken cancellationToken)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Title) || string.IsNullOrWhiteSpace(payload.Body))
        {
            return BadRequest(new { message = "タイトルと本文は必須です。" });
        }

        var result = await _pushService.SendNotificationToAllAsync(payload, cancellationToken);
        return Ok(new
        {
            message = "通知の配信処理が完了しました。",
            result.SucceededCount,
            result.FailedCount,
            result.ExpiredCount
        });
    }

    /// <summary>
    /// 現在のログインユーザーに対してテストプッシュ通知を送信します。
    /// </summary>
    [HttpPost("test-me")]
    public async Task<IActionResult> TestMe([FromBody] PushSubscriptionDto? fallbackSubscription, CancellationToken cancellationToken)
    {
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var payload = new PushNotificationPayload(
            Title: "SRNS テスト通知",
            Body: "PWA への Web Push 通知が正常に動作しています！",
            Icon: "/images/icons/icon-192x192.png",
            Url: "/notifications"
        );

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var userResult = await _pushService.SendNotificationToUserAsync(userId, payload, cancellationToken);
            if (userResult.SucceededCount > 0)
            {
                return Ok(new { message = "テスト通知を送信しました。", result = userResult });
            }
        }

        // ユーザーに紐づくサブスクリプションが無いか未ログインで fallbackSubscription がある場合
        if (fallbackSubscription != null && !string.IsNullOrWhiteSpace(fallbackSubscription.Endpoint))
        {
            bool directSuccess = await _pushService.SendNotificationAsync(fallbackSubscription, payload, cancellationToken);
            return Ok(new { message = directSuccess ? "テスト通知を送信しました。" : "通知の送信に失敗しました。" });
        }

        return BadRequest(new { message = "送信先のサブスクリプションが見つかりません。通知を許可して購読してください。" });
    }
}