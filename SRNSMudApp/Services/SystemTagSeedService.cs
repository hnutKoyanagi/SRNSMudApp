#pragma warning disable CA1848, CA1873

#region

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SRNSMudApp.Data;
using SRNSMudApp.Models;

#endregion

namespace SRNSMudApp.Services;

/// <summary>
///     本番・開発環境の初回起動時に、公式システム分類タグツリーをシード投入するサービス。
/// </summary>
public static class SystemTagSeedService
{
    private const string EmbeddedResourceName = "SRNSMudApp.Data.SeedData.system_tags_seed.json";

    /// <summary>
    ///     JSON シードデータからシステムタグツリーをデータベースに投入する。
    ///     既にシステムタグが存在する場合はスキップする（冪等性）。
    /// </summary>
    /// <param name="dbContext">データベースコンテキスト。</param>
    /// <param name="systemUserId">システムユーザーの ID。</param>
    /// <param name="logger">ロガー（省略可能）。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>新規投入されたタグの件数。</returns>
    public static async Task<int> SeedSystemTagsAsync(
        ApplicationDbContext dbContext,
        string systemUserId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemUserId);

        Tag? rootTag = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.Name == Tag.RootTagName, cancellationToken);

        if (rootTag is null)
        {
            logger?.LogWarning("ルートタグ '{RootTagName}' が存在しないため、システムタグのシードをスキップしました。", Tag.RootTagName);
            return 0;
        }

        // ルートタグ以外のシステムタグが既に存在していればシード済みと判断
        bool alreadySeeded = await dbContext.Tags.AnyAsync(
            t => t.OwnerId == systemUserId && t.Name != Tag.RootTagName,
            cancellationToken);

        if (alreadySeeded)
        {
            return 0;
        }

        List<SystemTagSeedModel>? seedItems = await LoadSeedItemsAsync(cancellationToken);
        if (seedItems is null || seedItems.Count == 0)
        {
            logger?.LogWarning("システムタグのシードデータが見つかりませんでした。");
            return 0;
        }

        logger?.LogInformation("システムタグのシードを開始します（合計 {Count} 件）...", seedItems.Count);

        // 階層の深さ（/ の出現数 - 1）ごとにグループ化し、親から子の順に登録
        IOrderedEnumerable<IGrouping<int, SystemTagSeedModel>> depthGroups = seedItems
            .GroupBy(item => item.Node.Count(c => c == '/') - 1)
            .OrderBy(g => g.Key);

        var nodeToIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["/"] = rootTag.Id
        };

        DateTime now = DateTime.UtcNow;
        int totalInserted = 0;

        foreach (IGrouping<int, SystemTagSeedModel> group in depthGroups)
        {
            var batch = new List<(Tag Tag, string Node)>();

            foreach (SystemTagSeedModel item in group)
            {
                int? parentId = nodeToIdMap.TryGetValue(item.ParentNode, out int pid) ? pid : rootTag.Id;

                var tag = new Tag
                {
                    Name = item.Name,
                    Content = item.Content,
                    IsSystem = true,
                    OwnerId = systemUserId,
                    IsLocked = item.IsLocked,
                    AutoAcceptIncomingTaggingRequests = item.AutoAcceptIncomingTaggingRequests,
                    Node = HierarchyId.Parse(item.Node),
                    ParentTagId = parentId,
                    CreatedDate = now,
                    UpdatedDate = now
                };

                _ = dbContext.Tags.Add(tag);
                batch.Add((tag, item.Node));
            }

            _ = await dbContext.SaveChangesAsync(cancellationToken);
            totalInserted += batch.Count;

            foreach ((Tag tag, string node) in batch)
            {
                nodeToIdMap[node] = tag.Id;
            }
        }

        logger?.LogInformation("システムタグのシードが完了しました（投入件数: {TotalInserted} 件）。", totalInserted);
        return totalInserted;
    }

    private static async Task<List<SystemTagSeedModel>?> LoadSeedItemsAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(SystemTagSeedService).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName);

        if (stream is not null)
        {
            return await JsonSerializer.DeserializeAsync<List<SystemTagSeedModel>>(stream, cancellationToken: cancellationToken);
        }

        // リソースとして取得できなかった場合のファイルシステムフォールバック
        string localPath = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "system_tags_seed.json");
        if (!File.Exists(localPath))
        {
            localPath = Path.Combine(Directory.GetCurrentDirectory(), "SRNSMudApp", "Data", "SeedData", "system_tags_seed.json");
        }

        if (File.Exists(localPath))
        {
            await using FileStream fs = File.OpenRead(localPath);
            return await JsonSerializer.DeserializeAsync<List<SystemTagSeedModel>>(fs, cancellationToken: cancellationToken);
        }

        return null;
    }
}
