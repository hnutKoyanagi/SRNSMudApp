#region

using Sidio.Sitemap.Core;

#endregion

namespace SRNSMudApp.Services.Sitemap;

/// <summary>
/// 固定ページのサイトマップノードを構築するプロバイダー実装
/// ステートレスであるためシングルトンとして安全に利用できます
/// </summary>
public class StaticPageSitemapProvider : IStaticPageSitemapProvider
{
    /// <inheritdoc />
    public IReadOnlyList<SitemapNode> GetNodes(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return GetNodes(baseUrl.ToString());
    }

    /// <inheritdoc />
    public IReadOnlyList<SitemapNode> GetNodes(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var normalizedBaseUrl = baseUrl.TrimEnd('/');

        // C# 13 コレクション式を使用して固定ページ一覧を定義
        return
        [
            new SitemapNode($"{normalizedBaseUrl}/")
            {
                ChangeFrequency = ChangeFrequency.Daily,
                Priority = 1.0m,
                LastModified = DateTime.UtcNow,
            },
            new SitemapNode($"{normalizedBaseUrl}/about")
            {
                ChangeFrequency = ChangeFrequency.Monthly,
                Priority = 0.8m,
            },
            new SitemapNode($"{normalizedBaseUrl}/contact")
            {
                ChangeFrequency = ChangeFrequency.Monthly,
                Priority = 0.5m,
            },
        ];
    }
}