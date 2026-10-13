#region

namespace SRNSMudApp.Models;

#endregion

/// <summary>
/// サイトマップ生成用の記事エンティティ
/// </summary>
public class Article
{
    /// <summary>
    /// 記事の一意識別子
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 記事タイトル
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URLスラッグ
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 最終更新日時
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 公開状態フラグ (true: 公開中, false: 非公開)
    /// </summary>
    public bool IsPublished { get; set; } = true;
}