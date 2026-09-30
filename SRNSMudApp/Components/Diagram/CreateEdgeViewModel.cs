namespace SRNSMudApp.Components.Diagram;

using SRNSMudApp.Data;

/// <summary>
///     Edge（タグ間関係）作成ダイアログ (CreateEdgeDialog) の入力状態および検索ロジックを担う ViewModel。
/// </summary>
public sealed class CreateEdgeViewModel
{
    public IReadOnlyList<Tag> AvailableTags { get; set; } = [];
    public Tag? SourceTag { get; set; }
    public Tag? TargetTag { get; set; }

    /// <summary>
    ///     Source と Target が選択され、かつ異なるタグである場合に確定可能。
    /// </summary>
    public bool CanSubmit =>
        SourceTag != null &&
        TargetTag != null &&
        SourceTag.Id != TargetTag.Id;

    /// <summary>
    ///     ダイアログ確定時の結果 (SourceTagId, TargetTagId)。
    /// </summary>
    public (int SourceTagId, int TargetTagId)? SubmitResult =>
        CanSubmit ? (SourceTag!.Id, TargetTag!.Id) : null;

    /// <summary>
    ///     初期設定を行う。
    /// </summary>
    public void Initialize(IReadOnlyList<Tag> availableTags, Tag? initialSourceTag, Tag? initialTargetTag)
    {
        AvailableTags = availableTags ?? [];
        if (initialSourceTag != null)
        {
            SourceTag = initialSourceTag;
        }

        if (initialTargetTag != null)
        {
            TargetTag = initialTargetTag;
        }
    }

    /// <summary>
    ///     始点タグ (Source) の候補を検索する（TargetTag は除外）。
    /// </summary>
    public IEnumerable<Tag> SearchSourceTags(string? query) =>
        SearchTagsInternal(query, TargetTag?.Id);

    /// <summary>
    ///     終点タグ (Target) の候補を検索する（SourceTag は除外）。
    /// </summary>
    public IEnumerable<Tag> SearchTargetTags(string? query) =>
        SearchTagsInternal(query, SourceTag?.Id);

    private IEnumerable<Tag> SearchTagsInternal(string? query, int? excludeId)
    {
        IEnumerable<Tag> source = AvailableTags;
        if (excludeId.HasValue)
        {
            source = source.Where(t => t.Id != excludeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return source.Take(20);
    }
}