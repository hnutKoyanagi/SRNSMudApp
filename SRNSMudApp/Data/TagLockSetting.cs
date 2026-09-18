namespace SRNSMudApp.Data;

/// <summary>
///     タグの階層ロック設定を保持するエンティティ。
/// </summary>
public class TagLockSetting
{
    /// <summary>設定レコードの識別子（単一レコード運用）。</summary>
    public int Id { get; set; }

    /// <summary>
    ///     ロック対象とする階層レベル（n階層目までロック）。
    ///     0 の場合は階層レベルによる自動ロックなし。
    ///     1 の場合は第1階層（ルート直下）がロック対象、2 の場合は第1階層および第2階層がロック対象となる。
    /// </summary>
    public int LockedHierarchyLevel { get; set; }

    /// <summary>最終更新日時（UTC）。</summary>
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}