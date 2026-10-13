#region

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Models;

#endregion

namespace SRNSMudApp.Data;

/// <summary>
/// 記事等のコンテンツデータを管理するDbContext
/// プライマリコンストラクタにより DbContextOptions を受け取ります
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 記事テーブル
    /// </summary>
    public DbSet<Article> Articles => Set<Article>();
}