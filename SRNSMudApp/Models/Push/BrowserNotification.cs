namespace SRNSMudApp.Models.Push;

using System.Collections.Generic;

using Microsoft.Azure.NotificationHubs;

/// <summary>
/// Azure Notification Hubs の Browser (Web Push) プラットフォーム向けネイティブ通知モデル。
/// W3C Web Push プロトコルおよび ServiceBusNotification-Format: browser に準拠したペイロードをカプセル化します。
/// </summary>
public sealed class BrowserNotification : Notification
{
    /// <summary>
    /// 指定された JSON ペイロードおよび追加ヘッダーを用いて通知インスタンスを初期化します。
    /// </summary>
    /// <param name="jsonPayload">通知本文となる JSON 文字列。</param>
    /// <param name="additionalHeaders">P256DH や Auth など Web Push に必要なヘッダー。</param>
    public BrowserNotification(string jsonPayload, IDictionary<string, string>? additionalHeaders = null)
        : base(additionalHeaders ?? new Dictionary<string, string>(), null, "application/json;charset=utf-8")
    {
        Body = jsonPayload;
    }

    /// <inheritdoc />
    protected override string PlatformType => "browser";

    /// <inheritdoc />
    protected override void OnValidateAndPopulateHeaders()
    {
        // 必須のバリデーションや追加ヘッダーの自動付与があればここで実施
    }
}