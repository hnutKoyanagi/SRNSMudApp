using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Admin;

/// <summary>
///     TagManagement コンポーネントの表示・フィルタ処理を担当する ViewModel。
///     UI への依存を持たないため、単体テストで高速にテスト可能。
/// </summary>
public static class TagManagementViewModel
{
    /// <summary>
    ///     検索文字列に基づいてタグ一覧をフィルタリングする。
    ///     タグ名、親タグ名、または作成者ユーザー名に部分一致するものを抽出する。
    /// </summary>
    /// <param name="tags">対象のタグ一覧。</param>
    /// <param name="searchString">検索キーワード。</param>
    /// <returns>フィルタリングされたタグ列挙。</returns>
    public static IEnumerable<TagLockItemDto> FilterTags(IEnumerable<TagLockItemDto> tags, string? searchString)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (string.IsNullOrWhiteSpace(searchString))
        {
            return tags;
        }

        return tags.Where(t =>
            t.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
            (t.ParentTagName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (t.OwnerUserName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}