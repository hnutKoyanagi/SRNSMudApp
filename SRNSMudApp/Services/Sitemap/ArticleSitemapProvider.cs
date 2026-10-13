#region

using Microsoft.EntityFrameworkCore;

using Sidio.Sitemap.Core;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Services.Sitemap;

/// <summary>
/// データベース (AppDbContext) から記事を取得し、動的サイトマップノードを生成するプロバイダー
/// プライマリコンストラクタにより DbContext を注入し、非同期かつ読み取り専用で高速にデータを取得します
/// </summary>
public class ArticleSitemapProvider(AppDbContext dbContext) : IArticleSitemapProvider
{
    private readonly AppDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public Task<IReadOnlyList<SitemapNode>> GetNodesAsync(Uri baseUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return GetNodesAsync(baseUrl.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SitemapNode>> GetNodesAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var normalizedBaseUrl = baseUrl.TrimEnd('/');

        // サイトマップ生成処理では変更追跡不要 (AsNoTracking) でオーバーヘッドを抑制
        // URL生成に必要な項目のみを射影 (Select) してメモリ消費を最小化
        var articles = await _dbContext.Articles
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                LastModified = a.UpdatedAt ?? a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // 取得した記事情報から SitemapNode を構築
        return articles
            .Select(article => new SitemapNode($"{normalizedBaseUrl}/article/{article.Id}")
            {
                LastModified = article.LastModified,
                ChangeFrequency = ChangeFrequency.Weekly,
                Priority = 0.7m,
            })
            .ToList();
    }
}