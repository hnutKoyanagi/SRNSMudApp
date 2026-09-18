namespace SRNSMudApp.Services;

/// <summary>
///     タグ管理画面表示用のタグおよびロック状態情報。
/// </summary>
public sealed record TagLockItemDto(
    int Id,
    string Name,
    int Level,
    int? ParentTagId,
    string? ParentTagName,
    string? OwnerUserName,
    bool IsDirectlyLocked,
    bool IsLevelLocked,
    bool IsSiblingLocked,
    bool IsLockedEffective);

/// <summary>
///     タグのロック管理および階層・兄弟制約を制御するサービスのインターフェース。
/// </summary>
public interface ITagLockService
{
    /// <summary>現在設定されているロック階層レベル（n階層目までロック）を取得する。</summary>
    Task<int> GetLockedHierarchyLevelAsync(CancellationToken cancellationToken = default);

    /// <summary>ロック階層レベル（n階層目までロック）を設定・更新する。</summary>
    Task SetLockedHierarchyLevelAsync(int level, CancellationToken cancellationToken = default);

    /// <summary>指定したタグが個別または階層設定により直接ロックされているか判定する。</summary>
    Task<bool> IsTagLockedAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     指定したタグ自身、またはその兄弟タグがロックされているか判定する。
    ///     真の場合、そのタグの編集・削除は禁止される。
    /// </summary>
    Task<bool> IsTagOrSiblingLockedAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     指定した親タグの配下に新たな子タグを作成可能か（＝ロックされているタグの兄弟作成に当たらないか）判定する。
    ///     真の場合、子タグの新規作成は禁止される。
    /// </summary>
    Task<bool> IsChildCreationRestrictedAsync(int? parentTagId, CancellationToken cancellationToken = default);

    /// <summary>指定したタグのすべての祖先タグ（ルートを除く）をロックする。</summary>
    Task LockAncestorsAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>指定したタグのすべての祖先タグ（ルートを除く）のロックを解除する。</summary>
    Task UnlockAncestorsAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>指定したタグの祖先タグ（ルートを除く）がロックされているか判定する。</summary>
    Task<bool> AreAncestorsLockedAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>タグ個別のロック状態を反転（トグル）する。</summary>
    Task<bool> ToggleTagLockAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>タグ管理画面向けに、全タグの階層・ロック状態を含む一覧を取得する。</summary>
    Task<IReadOnlyList<TagLockItemDto>> GetAllTagsWithLockStatusAsync(CancellationToken cancellationToken = default);
}