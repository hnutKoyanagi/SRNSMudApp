namespace SRNSMudApp.Models.Push;

/// <summary>
/// Azure Notification Hubs の接続設定オプション。
/// Web Push（ブラウザ通知）を Azure Notification Hub 経由で送信する際に使用します。
/// </summary>
public sealed class AzureNotificationHubOptions
{
    /// <summary>
    /// 設定セクション名
    /// </summary>
    public const string SectionName = "AzureNotificationHub";

    /// <summary>
    /// Azure Notification Hub の接続文字列。
    /// （例: Endpoint=sb://&lt;namespace&gt;.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=...）
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure Notification Hub のハブ名。
    /// </summary>
    public string? HubName { get; set; }

    /// <summary>
    /// Azure Notification Hub の設定が有効（接続文字列とハブ名の両方が入力されている）かどうか。
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(HubName);
}