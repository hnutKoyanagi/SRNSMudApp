#region

using Sidio.Sitemap.Core;

#endregion

namespace SRNSMudApp.Services.Sitemap;

/// <summary>
/// データベースから記事ページURLを動的に取得するサイトマッププロバイダーインターフェース
/// </summary>
public interface IArticleSitemapProvider
{
    /// <summary>
    /// ベースURLを元に公開中の記事ページのサイトマップノード一覧を非同期で取得します
    /// </summary>
    /// <param name="baseUrl">アプリケーションのルートURL (例: https://example.com)</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>サイトマップノードのリスト</returns>
    Task<IReadOnlyList<SitemapNode>> GetNodesAsync(string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// ベースURIを元に公開中の記事ページのサイトマップノード一覧を非同期で取得します
    /// </summary>
    /// <param name="baseUrl">アプリケーションのルートURI</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>サイトマップノードのリスト</returns>
    Task<IReadOnlyList<SitemapNode>> GetNodesAsync(Uri baseUrl, CancellationToken cancellationToken = default);
}