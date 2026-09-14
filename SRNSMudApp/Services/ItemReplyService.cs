using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;

namespace SRNSMudApp.Services;

/// <summary>
///     アイテムへのリプライやメッセージング関連の操作を担当するドメインサービス。
/// </summary>
public class ItemReplyService(IDbContextFactory<ApplicationDbContext> dbFactory) : IItemReplyService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));

    public async Task<Item?> AddReplyToRequestAsync(int requestId, string userId, string message)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        var reply = new Item
        {
            TaggingRequestEntityId = requestId,
            OwnerId = userId,
            Content = message,
            CreatedDate = DateTime.UtcNow
        };

        _ = context.Items!.Add(reply);
        _ = await context.SaveChangesAsync();

        return await context.Items
            .Include(r => r.Owner)
            .Include(r => r.TagRelations)
            .ThenInclude(tr => tr.Tag)
            .FirstOrDefaultAsync(r => r.Id == reply.Id);
    }

    public async Task<IReadOnlyList<Item>> GetItemRepliesAsync(int parentItemId)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        return await context.Items!
            .Include(i => i.Owner)
            .Include(i => i.TagRelations)
            .ThenInclude(tr => tr.Tag)
            .Include(i => i.NotificationRecipients)
            .Where(i => i.ParentItemId == parentItemId)
            .OrderBy(i => i.CreatedDate)
            .ToListAsync();
    }

    public async Task<int> GetItemReplyCountAsync(int parentItemId)
    {
        if (parentItemId <= 0)
        {
            return 0;
        }

        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        return await context.Items!.CountAsync(i => i.ParentItemId == parentItemId);
    }

    public async Task<Item?> AddItemReplyAsync(int parentItemId, string content, string userId, IEnumerable<string>? targetUserIds = null)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();

        List<TagRelation> inheritedRelations = await context.TagRelations
            .Where(tr => tr.ItemId == parentItemId)
            .ToListAsync();

        var parentRootId = await context.Items
            .Where(i => i.Id == parentItemId)
            .Select(i => i.RootItemId)
            .FirstOrDefaultAsync();

        var replyItem = new Item
        {
            Content = content,
            OwnerId = userId,
            ParentItemId = parentItemId,
            RootItemId = parentRootId ?? parentItemId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _ = context.Items!.Add(replyItem);
        _ = await context.SaveChangesAsync();

        // 通知対象ユーザー（Twitterライクなメンション先）の永続化
        List<string> recipientIds;
        if (targetUserIds != null)
        {
            recipientIds = targetUserIds.Where(id => !string.IsNullOrWhiteSpace(id) && id != userId).Distinct().ToList();
        }
        else
        {
            // 省略時はスレッド参加者（親アイテムオーナー + 既存リプライ投稿者、自分を除く）を自動対象とする
            var parentOwnerId = await context.Items
                .Where(i => i.Id == parentItemId)
                .Select(i => i.OwnerId)
                .FirstOrDefaultAsync();

            var replierIds = await context.Items
                .Where(i => i.ParentItemId == parentItemId)
                .Select(i => i.OwnerId)
                .ToListAsync();

            recipientIds = [.. (new[] { parentOwnerId }.Concat(replierIds))
                .Where(id => !string.IsNullOrEmpty(id) && id != userId)
                .Distinct()!];
        }

        if (recipientIds.Count > 0)
        {
            var recipients = recipientIds.Select(rid => new ItemReplyNotificationRecipient
            {
                ReplyItemId = replyItem.Id,
                RecipientUserId = rid,
                CreatedDate = DateTimeOffset.UtcNow
            });
            context.ItemReplyNotificationRecipients.AddRange(recipients);
            _ = await context.SaveChangesAsync();
        }

        if (inheritedRelations.Count > 0)
        {
            var replyTagRelations = inheritedRelations
                .Select(relation => new TagRelation
                {
                    ItemId = replyItem.Id,
                    TagId = relation.TagId,
                    Weight = relation.Weight,
                    OwnerId = userId,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                })
                .ToList();

            context.TagRelations.AddRange(replyTagRelations);
            _ = await context.SaveChangesAsync();
        }

        return await context.Items
            .Include(i => i.Owner)
            .Include(i => i.TagRelations)
            .ThenInclude(tr => tr.Tag)
            .Include(i => i.NotificationRecipients)
            .FirstOrDefaultAsync(i => i.Id == replyItem.Id);
    }

    public async Task<bool> ToggleConversationOptOutAsync(int rootItemId, string userId)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        var existing = await context.ConversationOptOuts
            .FirstOrDefaultAsync(o => o.RootItemId == rootItemId && o.UserId == userId);

        if (existing is not null)
        {
            context.ConversationOptOuts.Remove(existing);
        }
        else
        {
            context.ConversationOptOuts.Add(new ConversationOptOut
            {
                RootItemId = rootItemId,
                UserId = userId,
                CreatedDate = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return existing is null; // Returns true if opted out, false if opted in
    }

    public async Task<bool> IsUserOptedOutAsync(int rootItemId, string userId)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        return await context.ConversationOptOuts
            .AnyAsync(o => o.RootItemId == rootItemId && o.UserId == userId);
    }

    public async Task<HashSet<string>> GetOptedOutUsersAsync(int rootItemId)
    {
        await using ApplicationDbContext context = await _dbFactory.CreateDbContextAsync();
        var userIds = await context.ConversationOptOuts
            .Where(o => o.RootItemId == rootItemId)
            .Select(o => o.UserId)
            .ToListAsync();

        return [.. userIds];
    }
}