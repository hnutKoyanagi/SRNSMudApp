#region

using SRNSMudApp.Data;

using Tag = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Services;

/// <summary>
///     タグ検索・一覧取得を担う CQS の Query 契約。
/// </summary>
public interface ITagSearchQueryService
{
    /// <summary>すべてのタグを非同期で取得する。</summary>
    Task<List<Tag>> GetAllTagsAsync();

    /// <summary>テキストおよび埋め込みベクトル検索によりタグを検索する。</summary>
    Task<List<Tag>> SearchTagsAsync(string searchText);

    /// <summary>指定された名前のタグを検索する。</summary>
    Task<Tag?> FindTagByNameAsync(string tagName);

    /// <summary>値が空の場合は全件、指定時は検索結果を返す。</summary>
    Task<List<Tag>> SearchTagsWithFallbackAsync(string? value, CancellationToken token = default);

    /// <summary>詳細情報（所有者・関連タグなど）を含むタグ一覧を取得する。</summary>
    Task<List<Tag>> GetTagsWithDetailsAsync();
}