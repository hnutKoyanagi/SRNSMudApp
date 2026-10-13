namespace SRNSMudApp.Models;

/// <summary>全体公開スコープ</summary>
public sealed record PublicTagScope : TagVisibility;

/// <summary>フォロワー限定非公開スコープ</summary>
public sealed record PrivateFollowersTagScope : TagVisibility;

/// <summary>特定グループ限定非公開スコープ</summary>
public sealed record PrivateGroupTagScope(int GroupId) : TagVisibility;

/// <summary>
///     タグの公開範囲を表す型安全なドメインモデル。
///     「公開（パブリック）」のときはグループIDを持てず、
///     「プライベート」のときのみフォロワー全体または特定グループを指定できる制約を型で保証する。
/// </summary>
public abstract record TagVisibility
{
    private protected TagVisibility() { }

    public static TagVisibility Public() => new PublicTagScope();
    public static TagVisibility PrivateFollowers() => new PrivateFollowersTagScope();
    public static TagVisibility PrivateGroup(int groupId) => new PrivateGroupTagScope(groupId);

    /// <summary>
    ///     bool と int? の組み合わせから型安全な TagVisibility を生成する。
    /// </summary>
    public static TagVisibility FromBooleans(bool isPrivate, int? targetGroupId) =>
        isPrivate
            ? (targetGroupId.HasValue ? PrivateGroup(targetGroupId.Value) : PrivateFollowers())
            : Public();

    /// <summary>非公開かどうか</summary>
    public bool IsPrivate => this is not PublicTagScope;

    /// <summary>特定グループ限定の場合のグループID（それ以外は null）</summary>
    public int? TargetUserGroupId => this switch
    {
        PrivateGroupTagScope g => g.GroupId,
        _ => null
    };
}