#region

using Sidio.Sitemap.Core;

#endregion

namespace SRNSMudApp.Services.Sitemap;

/// <summary>
/// 固定ページ（トップ、アバウト、問い合わせ等）のサイトマップノードを提供するプロバイダーインターフェース
/// </summary>
public interface IStaticPageSitemapProvider
{
    /// <summary>
    /// ベースURLを元に固定ページのサイトマップノード一覧を取得します
    /// </summary>
    /// <param name="baseUrl">アプリケーションのルートURL (例: https://example.com)</param>
    /// <returns>サイトマップノードのリスト</returns>
    IReadOnlyList<SitemapNode> GetNodes(string baseUrl);

    /// <summary>
    /// ベースURIを元に固定ページのサイトマップノード一覧を取得します
    /// </summary>
    /// <param name="baseUrl">アプリケーションのルートURI</param>
    /// <returns>サイトマップノードのリスト</returns>
    IReadOnlyList<SitemapNode> GetNodes(Uri baseUrl);
}