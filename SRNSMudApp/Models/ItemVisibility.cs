namespace SRNSMudApp.Models;

/// <summary>全体公開スコープ</summary>
public sealed record PublicItemScope : ItemVisibility;

/// <summary>フォロワー限定非公開スコープ</summary>
public sealed record PrivateFollowersItemScope : ItemVisibility;

/// <summary>特定グループ限定非公開スコープ</summary>
public sealed record PrivateGroupItemScope(int GroupId) : ItemVisibility;

/// <summary>
///     アイテムの公開範囲を表す型安全なドメインモデル。
///     「公開（パブリック）」のときはグループIDを持てず、
///     「プライベート」のときのみフォロワー全体または特定グループを指定できる制約を型で保証する。
/// </summary>
public abstract record ItemVisibility
{
    private protected ItemVisibility() { }

    public static ItemVisibility Public() => new PublicItemScope();
    public static ItemVisibility PrivateFollowers() => new PrivateFollowersItemScope();
    public static ItemVisibility PrivateGroup(int groupId) => new PrivateGroupItemScope(groupId);

    /// <summary>
    ///     レガシーな bool と int? の組み合わせから型安全な ItemVisibility を生成する。
    /// </summary>
    public static ItemVisibility FromBooleans(bool isPrivate, int? targetGroupId) =>
        isPrivate
            ? (targetGroupId.HasValue ? PrivateGroup(targetGroupId.Value) : PrivateFollowers())
            : Public();

    /// <summary>非公開かどうか</summary>
    public bool IsPrivate => this is not PublicItemScope;

    /// <summary>特定グループ限定の場合のグループID（それ以外は null）</summary>
    public int? TargetUserGroupId => this switch
    {
        PrivateGroupItemScope g => g.GroupId,
        _ => null
    };
}