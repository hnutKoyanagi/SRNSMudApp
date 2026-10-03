namespace SRNSMudApp.Models.Push;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// VAPID（Voluntary Application Server Identification）設定クラス。
/// Web Push（ブラウザ通知）の暗号化およびプッシュサービス（Google FCM, Mozilla, Apple）認証に使用されます。
/// </summary>
public sealed class VapidOptions
{
    /// <summary>
    /// 設定セクション名
    /// </summary>
    public const string SectionName = "Vapid";

    /// <summary>
    /// デフォルトの VAPID 公開鍵。
    /// </summary>
    public const string DefaultPublicKey = "BFvUOGK7GEZ9MS5KuJn6-TvVyaCszkE6bhusWaaEgYRbPohHLNIsAsbkybNvxjqdLx5xsrE_QKpAYlAB0vzEcjg";

    /// <summary>
    /// デフォルトの VAPID 秘密鍵。
    /// 環境変数 Vapid__PrivateKey または appsettings.json で未設定の場合にフォールバックとして使用されます。
    /// </summary>
    public const string DefaultPrivateKey = "IxUhCEyrptimLpAaPkOmpEIHi3pZBgpZRej0P02PXFc";

    /// <summary>
    /// 管理者への連絡先 URI（例: "mailto:admin@example.com"）。
    /// </summary>
    [Required]
    public string Subject { get; set; } = "mailto:admin@example.com";

    /// <summary>
    /// フロントエンドの PushManager に渡す VAPID 公開鍵。
    /// </summary>
    [Required]
    public string PublicKey { get; set; } = DefaultPublicKey;

    /// <summary>
    /// 送信署名に使用する VAPID 秘密鍵。
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// 有効な VAPID 秘密鍵を取得します。
    /// PrivateKey が明示的に設定されている場合はそれを返し、
    /// 空でかつ PublicKey がデフォルト値の場合はデフォルトの秘密鍵に安全にフォールバックします。
    /// </summary>
    public string GetEffectivePrivateKey()
    {
        if (!string.IsNullOrWhiteSpace(PrivateKey))
        {
            return PrivateKey;
        }

        if (string.Equals(PublicKey, DefaultPublicKey, StringComparison.Ordinal))
        {
            return DefaultPrivateKey;
        }

        return string.Empty;
    }

    /// <summary>
    /// 有効な VAPID 設定が存在するかどうか。
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(GetEffectivePrivateKey());
}