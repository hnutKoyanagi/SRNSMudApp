// Services/TagVisibilityExtensions.cs
#region

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Services;

/// <summary>
///     タグの可視性制御をクエリおよびエンティティに適用する拡張メソッド。
///     BAN されたユーザーがオーナーのタグは不可視とし、
///     プライベートモード（フォロワー限定・グループ限定）および管理者非公開の制御を行う。
/// </summary>
public static class TagVisibilityExtensions
{
    /// <summary>
    ///     オーナーが BAN されていないタグのみにフィルタリングする（後方互換用）。
    /// </summary>
    public static IQueryable<Tag> WhereVisibleToUser(this IQueryable<Tag> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Where(t => t.Owner == null || !t.Owner.IsBanned);
    }

    /// <summary>
    ///     指定されたユーザーが閲覧可能なタグのみにフィルタリングする。
    ///     1. BAN されたユーザーのタグは一律除外。
    ///     2. 管理者（isAdmin == true）でない場合、管理者により強制非公開化されたタグ（IsAdminHidden）は除外。
    ///     3. 閲覧者が未ログインの場合: パブリックタグ（!IsPrivate）のみ閲覧可能。
    ///     4. 閲覧者がログイン済みの場合:
    ///        - 自身が作成したタグ（OwnerId == currentUserId）
    ///        - パブリックタグ（!IsPrivate）
    ///        - プライベート（フォロワー限定、TargetUserGroupId == null）で、閲覧者が作成者をフォローしている場合
    ///        - プライベート（グループ限定、TargetUserGroupId != null）で、閲覧者がそのグループのメンバーまたはオーナーである場合
    /// </summary>
    public static IQueryable<Tag> WhereVisibleToUser(
        this IQueryable<Tag> query,
        ApplicationDbContext context,
        string? currentUserId,
        bool isAdmin = false)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        query = query.Where(t => t.Owner == null || !t.Owner.IsBanned);

        if (!isAdmin)
        {
            query = query.Where(t => !t.IsAdminHidden);
        }

        return string.IsNullOrEmpty(currentUserId)
            ? query.Where(t => !t.IsPrivate)
            : query.Where(t =>
                isAdmin
                || t.OwnerId == currentUserId
                || !t.IsPrivate
                || (t.TargetUserGroupId == null && context.UserFollows.Any(f => f.OwnerId == currentUserId && f.FollowedUserId == t.OwnerId))
                || (t.TargetUserGroupId != null && (
                    context.UserGroupMembers.Any(m => m.UserGroupId == t.TargetUserGroupId && m.UserId == currentUserId)
                    || context.UserGroups.Any(g => g.Id == t.TargetUserGroupId && g.OwnerId == currentUserId)
                ))
            );
    }

    /// <summary>
    ///     メモリ上の単一タグに対して、タグが可視（オーナーが BAN されていない）かを判定する（後方互換用）。
    /// </summary>
    public static bool IsTagVisibleToUser(this Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.Owner == null || !tag.Owner.IsBanned;
    }

    /// <summary>
    ///     メモリ上の単一タグに対して、指定ユーザーが閲覧可能かを非同期で判定する。
    /// </summary>
    public static async Task<bool> IsTagVisibleToUserAsync(
        this Tag tag,
        ApplicationDbContext context,
        string? currentUserId,
        bool isAdmin = false)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(context);

        if (tag.Owner != null && tag.Owner.IsBanned)
        {
            return false;
        }

        if (isAdmin)
        {
            return true;
        }

        if (tag.IsAdminHidden)
        {
            return false;
        }

        if (!tag.IsPrivate)
        {
            return true;
        }

        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        if (tag.OwnerId == currentUserId)
        {
            return true;
        }

        return tag.TargetUserGroupId is null
            ? await context.UserFollows.AnyAsync(f => f.OwnerId == currentUserId && f.FollowedUserId == tag.OwnerId)
            : await context.UserGroupMembers.AnyAsync(m => m.UserGroupId == tag.TargetUserGroupId && m.UserId == currentUserId)
               || await context.UserGroups.AnyAsync(g => g.Id == tag.TargetUserGroupId && g.OwnerId == currentUserId);
    }

    /// <summary>
    ///     メモリ上のタグが指定ユーザーに可視かを判定する。
    ///     フォロー先ユーザーID集合および所属グループID集合を受け取り、DBアクセスなしで判定する。
    /// </summary>
    public static bool IsTagVisibleInMemory(
        this Tag tag,
        string? currentUserId,
        bool isAdmin = false,
        IReadOnlySet<string>? followedUserIds = null,
        IReadOnlySet<int>? memberGroupIds = null)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Owner != null && tag.Owner.IsBanned)
        {
            return false;
        }

        if (tag.IsSystem || Tag.VoteTagNames.Contains(tag.Name) || Tag.ReactionTagNames.Contains(tag.Name) || tag.Name == Tag.RootTagName)
        {
            return true;
        }

        if (isAdmin)
        {
            return true;
        }

        if (tag.IsAdminHidden)
        {
            return false;
        }

        if (!tag.IsPrivate)
        {
            return true;
        }

        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        if (tag.OwnerId == currentUserId)
        {
            return true;
        }

        return tag.TargetUserGroupId is null
            ? followedUserIds?.Contains(tag.OwnerId) == true
            : memberGroupIds?.Contains(tag.TargetUserGroupId.Value) == true;
    }

    /// <summary>
    ///     指定されたアイテム群に紐づく TagRelations のうち、閲覧ユーザーに不可視なタグを持つリレーションを除外（マスク）する。
    /// </summary>
    public static async Task FilterVisibleTagRelationsAsync(
        this IEnumerable<Item> items,
        ApplicationDbContext context,
        string? currentUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);

        if (isAdmin)
        {
            return;
        }

        HashSet<string> followedUserIds = !string.IsNullOrEmpty(currentUserId)
            ? (await context.UserFollows
                .Where(f => f.OwnerId == currentUserId)
                .Select(f => f.FollowedUserId)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];

        HashSet<int> memberGroupIds = !string.IsNullOrEmpty(currentUserId)
            ? (await context.UserGroupMembers
                .Where(m => m.UserId == currentUserId)
                .Select(m => m.UserGroupId)
                .Concat(context.UserGroups.Where(g => g.OwnerId == currentUserId).Select(g => g.Id))
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];

        foreach (Item item in items)
        {
            if (item.TagRelations.Count > 0)
            {
                item.TagRelations = item.TagRelations
                    .Where(tr => tr.Tag == null || tr.Tag.IsTagVisibleInMemory(currentUserId, isAdmin, followedUserIds, memberGroupIds))
                    .ToList();
            }
        }
    }
}