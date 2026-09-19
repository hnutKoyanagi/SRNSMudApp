// Components/Diagram/TagDiagramFocusBarViewModel.cs
#region

using SRNSMudApp.Data;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Diagram;

/// <summary>
///     TagDiagramFocusBar コンポーネントのエッジ検出および階層関連判定ロジックを担当する ViewModel。
/// </summary>
public static class TagDiagramFocusBarViewModel
{
    /// <summary>
    ///     指定した2つのタグ間に存在する既存のエッジ（順方向または逆方向）を取得する。
    /// </summary>
    /// <param name="edges">全エッジリスト。</param>
    /// <param name="tagId1">1つ目のタグ ID。</param>
    /// <param name="tagId2">2つ目のタグ ID。</param>
    /// <returns>合致するエッジ。存在しない場合は null。</returns>
    public static TagEdge? GetExistingEdgeBetween(IEnumerable<TagEdge>? edges, int tagId1, int tagId2)
    {
        if (edges is null)
        {
            return null;
        }

        return edges.FirstOrDefault(e =>
            (e.SourceTagId == tagId1 && e.TargetTagId == tagId2) ||
            (e.SourceTagId == tagId2 && e.TargetTagId == tagId1));
    }

    /// <summary>
    ///     指定したタグの直接の子タグ数を集計する。
    /// </summary>
    /// <param name="allTags">タグ一覧。</param>
    /// <param name="tagId">親タグ ID。</param>
    /// <returns>子タグ数。</returns>
    public static int GetChildTagsCount(IEnumerable<TagEntity>? allTags, int tagId)
    {
        if (allTags is null)
        {
            return 0;
        }

        return allTags.Count(t => t.ParentTagId == tagId);
    }
}

