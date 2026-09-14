using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models;

namespace SRNSMudApp.Services;

/// <summary>
///     特定の URL 種別（Item, Tag, User, 外部 OGP 等）に応じたリンクプレビューを生成する Strategy プロバイダーインターフェース。
///     プレビュー生成ロジックを個別プロバイダーに分離し、開放閉鎖の原則 (OCP) を実現する。
/// </summary>
public interface ILinkPreviewProvider
{
    /// <summary>
    ///     プロバイダーの評価順序を取得する。値が小さいものほど優先的に評価される（既定値: 100）。
    /// </summary>
    int Order => 100;

    /// <summary>
    ///     指定された URI を本プロバイダーで処理可能かどうかを判定する。
    /// </summary>
    /// <param name="uri">判定対象の URI。</param>
    /// <returns>処理可能な場合は true。それ以外は false。</returns>
    bool CanHandle(Uri uri);

    /// <summary>
    ///     リンクプレビューデータを非同期で生成・取得する。
    /// </summary>
    /// <param name="uri">解析対象の URI。</param>
    /// <param name="originalUrl">リクエスト元の元 URL 文字列。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>生成された <see cref="LinkPreviewData"/>。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Original URL representation")]
    Task<LinkPreviewData> GetPreviewAsync(Uri uri, string originalUrl, CancellationToken cancellationToken = default);
}