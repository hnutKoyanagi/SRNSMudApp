using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SRNSMudApp.Models;

/// <summary>
///     タグ付けインポートリクエストのルートオブジェクト。
/// </summary>
public class TaggingImportRoot
{
    [JsonPropertyName("TaggingRequestEntity")]
    public TaggingImportPayload? TaggingRequestEntity { get; set; }
}

/// <summary>
///     インポート対象のデータ群（Item, TagRelations, TagEdges）。
/// </summary>
public class TaggingImportPayload
{
    [JsonPropertyName("Item")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JSON deserialization DTO")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON deserialization DTO")]
    public List<ImportItemDto> Item { get; set; } = [];

    [JsonPropertyName("TagRelations")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JSON deserialization DTO")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON deserialization DTO")]
    public List<ImportTagRelationDto> TagRelations { get; set; } = [];

    [JsonPropertyName("TagEdges")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JSON deserialization DTO")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON deserialization DTO")]
    public List<ImportTagEdgeDto> TagEdges { get; set; } = [];
}

/// <summary>
///     インポートする Item 情報。
/// </summary>
public class ImportItemDto
{
    [JsonPropertyName("ItemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("SequenceOrder")]
    public int SequenceOrder { get; set; }

    [JsonPropertyName("Content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
///     インポートする TagRelation 情報。
/// </summary>
public class ImportTagRelationDto
{
    [JsonPropertyName("RelationId")]
    public string RelationId { get; set; } = string.Empty;

    [JsonPropertyName("SourceItemId")]
    public string SourceItemId { get; set; } = string.Empty;

    [JsonPropertyName("Tag")]
    public ImportTagDto Tag { get; set; } = new();
}

/// <summary>
///     インポートする Tag 情報。
/// </summary>
public class ImportTagDto
{
    [JsonPropertyName("HierarchyId")]
    public string? HierarchyId { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("TagKind")]
    public string TagKind { get; set; } = "UserCustomTag";
}

/// <summary>
///     インポートする TagEdge 情報。
/// </summary>
public class ImportTagEdgeDto
{
    [JsonPropertyName("EdgeId")]
    public string EdgeId { get; set; } = string.Empty;

    [JsonPropertyName("SourceTagId")]
    public string SourceTagId { get; set; } = string.Empty;

    [JsonPropertyName("TargetTagId")]
    public string TargetTagId { get; set; } = string.Empty;

    [JsonPropertyName("AppliedTags")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JSON deserialization DTO")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON deserialization DTO")]
    public List<string> AppliedTags { get; set; } = [];
}

/// <summary>
///     タグ未存在時の解決アクション。
/// </summary>
public enum TagResolutionAction
{
    CreateNew,
    UseExisting,
    Skip
}

/// <summary>
///     未存在タグの解決結果。
/// </summary>
public sealed record TagResolutionDecision(
    TagResolutionAction Action,
    int? SelectedExistingTagId,
    int? SelectedParentTagId,
    string? NewTagName);

/// <summary>
///     内部リンク置き換えの選択アクション。
/// </summary>
public enum TagLinkReplaceAction
{
    ReplaceWithFirstCandidate,
    ReplaceWithSelectedCandidate,
    DoNotReplace
}

/// <summary>
///     内部リンク置き換えダイアログの結果。
/// </summary>
public sealed record TagLinkReplaceDecision(
    TagLinkReplaceAction Action,
    int? SelectedTagId);