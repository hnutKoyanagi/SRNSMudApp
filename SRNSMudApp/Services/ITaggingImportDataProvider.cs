using SRNSMudApp.Data;
using SRNSMudApp.Models;

namespace SRNSMudApp.Services;

/// <summary>
///     インポート実行結果情報。
/// </summary>
public sealed record TaggingImportResult(
    int CreatedItemsCount,
    int CreatedTagsCount,
    int CreatedRelationsCount,
    int CreatedEdgesCount,
    int CreatedAttachmentsCount,
    IReadOnlyList<string> Messages);

/// <summary>
///     Item・Tag・TagRelation・TagEdge インポート用のデータアクセスプロバイダー。
/// </summary>
public interface ITaggingImportDataProvider
{
    /// <summary>
    ///     タグ名と所有者から既存のタグを検索する。
    /// </summary>
    Task<Tag?> FindExistingTagAsync(string name, string? ownerId, bool isSystem, CancellationToken cancellationToken = default);

    /// <summary>
    ///     親タグや置換先タグの手動選択用検索。
    /// </summary>
    Task<IReadOnlyList<Tag>> SearchTagsAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>
    ///     親タグ候補（Level >= 2）の手動選択用検索。
    /// </summary>
    Task<IReadOnlyList<Tag>> SearchParentCandidateTagsAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>
    ///     新規タグを作成して永続化する。
    /// </summary>
    Task<Tag> CreateTagAsync(
        string name,
        string ownerId,
        bool isSystem,
        int? parentTagId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     インポートデータ全体（Item, TagRelation, TagEdge, TagEdgeTagAttachment）をトランザクション内で作成・保存する。
    /// </summary>
    Task<TaggingImportResult> ExecuteImportAsync(
        string currentUserId,
        TaggingImportPayload payload,
        IReadOnlyDictionary<string, int> relationIdToTagIdMap,
        IReadOnlyList<string> processedItemContents,
        CancellationToken cancellationToken = default);
}