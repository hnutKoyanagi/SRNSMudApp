using MudBlazor;

namespace SRNSMudApp.Components.UI;

/// <summary>
///     公開範囲バッジの判定に必要なコンテキスト情報。
/// </summary>
/// <param name="IsPrivate">非公開設定であるかどうか。</param>
/// <param name="TargetUserGroupName">公開対象のユーザーグループ名（グループ指定がない場合は null）。</param>
/// <param name="IsAdminHidden">管理者により強制非公開化されているかどうか。</param>
/// <param name="TargetUserGroupId">公開対象のユーザーグループID（グループ指定がない場合は null）。</param>
public sealed record PrivacyBadgeContext(
    bool IsPrivate,
    string? TargetUserGroupName,
    bool IsAdminHidden = false,
    int? TargetUserGroupId = null);

/// <summary>
///     公開範囲バッジの UI 表示モデル。
/// </summary>
/// <param name="Text">バッジに表示するテキスト。</param>
/// <param name="Icon">バッジに表示する MudBlazor アイコン。</param>
/// <param name="Color">バッジの表示色。</param>
/// <param name="Href">バッジタップ時の遷移先 URL（遷移不要な場合は null）。</param>
public sealed record PrivacyBadgeModel(string Text, string Icon, Color Color, string? Href = null);

/// <summary>
///     公開範囲バッジ生成のための Strategy インターフェース。
/// </summary>
public interface IPrivacyBadgeStrategy
{
    /// <summary>
    ///     指定されたコンテキストに対して本 Strategy が適用可能かどうかを判定する。
    /// </summary>
    /// <param name="context">判定コンテキスト。</param>
    /// <returns>適用可能な場合は true、それ以外は false。</returns>
    bool CanApply(PrivacyBadgeContext context);

    /// <summary>
    ///     コンテキストに応じたバッジ表示モデルを生成する。
    /// </summary>
    /// <param name="context">判定コンテキスト。</param>
    /// <returns>生成されたバッジモデル。</returns>
    PrivacyBadgeModel CreateBadge(PrivacyBadgeContext context);
}

/// <summary>
///     管理者非公開用のバッジ Strategy。
/// </summary>
public sealed class AdminHiddenPrivacyBadgeStrategy : IPrivacyBadgeStrategy
{
    /// <inheritdoc />
    public bool CanApply(PrivacyBadgeContext context) => context.IsAdminHidden;

    /// <inheritdoc />
    public PrivacyBadgeModel CreateBadge(PrivacyBadgeContext context) =>
        new("管理者非公開", Icons.Material.Filled.VisibilityOff, Color.Error);
}

/// <summary>
///     フォロワー限定公開用のバッジ Strategy。
/// </summary>
public sealed class FollowersOnlyPrivacyBadgeStrategy : IPrivacyBadgeStrategy
{
    /// <inheritdoc />
    public bool CanApply(PrivacyBadgeContext context) =>
        !context.IsAdminHidden && context.IsPrivate && string.IsNullOrEmpty(context.TargetUserGroupName);

    /// <inheritdoc />
    public PrivacyBadgeModel CreateBadge(PrivacyBadgeContext context) =>
        new("フォロワー限定", Icons.Material.Filled.Lock, Color.Warning);
}

/// <summary>
///     特定グループ限定公開用のバッジ Strategy。
/// </summary>
public sealed class GroupPrivacyBadgeStrategy : IPrivacyBadgeStrategy
{
    /// <inheritdoc />
    public bool CanApply(PrivacyBadgeContext context) =>
        !context.IsAdminHidden && context.IsPrivate && !string.IsNullOrEmpty(context.TargetUserGroupName);

    /// <inheritdoc />
    public PrivacyBadgeModel CreateBadge(PrivacyBadgeContext context)
    {
        string href = context.TargetUserGroupId.HasValue
            ? $"/UserGroup/Members/{context.TargetUserGroupId.Value}"
            : "/UserGroup/List";
        return new($"グループ: {context.TargetUserGroupName}", Icons.Material.Filled.Group, Color.Info, href);
    }
}

/// <summary>
///     公開範囲バッジ Strategy の解決リゾルバー。
/// </summary>
public static class PrivacyBadgeStrategyResolver
{
    private static readonly IReadOnlyList<IPrivacyBadgeStrategy> Strategies =
    [
        new AdminHiddenPrivacyBadgeStrategy(),
        new FollowersOnlyPrivacyBadgeStrategy(),
        new GroupPrivacyBadgeStrategy()
    ];

    /// <summary>
    ///     公開範囲設定に応じたバッジモデルを解決する。
    /// </summary>
    /// <param name="isPrivate">非公開設定であるかどうか。</param>
    /// <param name="targetUserGroupName">対象ユーザーグループ名。</param>
    /// <param name="isAdminHidden">管理者非公開であるかどうか。</param>
    /// <param name="targetUserGroupId">対象ユーザーグループID。</param>
    /// <returns>該当するバッジモデル。公開設定の場合は null。</returns>
    public static PrivacyBadgeModel? Resolve(
        bool isPrivate,
        string? targetUserGroupName,
        bool isAdminHidden = false,
        int? targetUserGroupId = null)
    {
        var context = new PrivacyBadgeContext(isPrivate, targetUserGroupName, isAdminHidden, targetUserGroupId);
        IPrivacyBadgeStrategy? strategy = Strategies.FirstOrDefault(candidate => candidate.CanApply(context));
        return strategy?.CreateBadge(context);
    }
}