using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Services;

/// <summary>
/// <see cref="NotificationsDataProvider"/> の通知生データ取得ロジックの単体テスト (MSSQL Testcontainers)。
/// LINQ クエリが EF Core によって正常に SQL 変換され、CommentItem を含む TagRelation コメントが正しく抽出されるかを検証する。
/// </summary>
public class NotificationsDataProviderTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(ApplicationDbContext db, NotificationsDataProvider sut, string itemOwnerId, string commenterId, int itemId, int tagId)> CreateScopeAsync()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var db = new ApplicationDbContext(_sharedDb.Options);
        var sut = new NotificationsDataProvider(new DbContextFactoryStub(_sharedDb.Options));

        var itemOwnerId = $"item_owner_{tid}";
        var commenterId = $"commenter_{tid}";

        await db.SeedUsersAsync(itemOwnerId, commenterId);

        var tag = new Tag { Name = $"Tag_{tid}", IsSystem = true, OwnerId = itemOwnerId, CachedWeight = 0 };
        var item = new Item { Content = $"TargetItem_{tid}", OwnerId = itemOwnerId };

        db.Tags.Add(tag);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return (db, sut, itemOwnerId, commenterId, item.Id, tag.Id);
    }

    [Fact]
    public async Task GetNotificationRawDataAsync_TranslatesTagRelationCommentsQuery_AndRetrievesCommentItem()
    {
        // Arrange
        var (db, sut, itemOwnerId, commenterId, itemId, tagId) = await CreateScopeAsync();
        await using (db)
        {
            var commentItem = new Item
            {
                OwnerId = commenterId,
                Content = "素晴らしい記事です！",
                ItemKindJson = System.Text.Json.JsonSerializer.Serialize(new TagCommentItem(itemId, tagId))
            };
            db.Items.Add(commentItem);
            await db.SaveChangesAsync();

            var tagRelation = new TagRelation
            {
                ItemId = itemId,
                TagId = tagId,
                OwnerId = commenterId,
                Weight = 1,
                CommentItemId = commentItem.Id
            };
            db.TagRelations.Add(tagRelation);
            await db.SaveChangesAsync();

            // Act: itemOwnerId 宛ての通知を取得
            NotificationRawData rawData = await sut.GetNotificationRawDataAsync(itemOwnerId);

            // Assert
            Assert.NotNull(rawData.TagRelationComments);
            TagRelation? targetCommentRelation = rawData.TagRelationComments.FirstOrDefault(tr => tr.Id == tagRelation.Id);
            Assert.NotNull(targetCommentRelation);
            Assert.Equal("素晴らしい記事です！", targetCommentRelation.Comment);
            Assert.NotNull(targetCommentRelation.CommentItem);
            Assert.Equal("素晴らしい記事です！", targetCommentRelation.CommentItem.Content);
        }
    }

    [Fact]
    public async Task GetNotificationRawDataAsync_ExcludesOwnCommentOrEmptyComment()
    {
        // Arrange
        var (db, sut, itemOwnerId, commenterId, itemId, tagId) = await CreateScopeAsync();
        await using (db)
        {
            // 1. 自身のコメント（通知対象外）
            var ownCommentItem = new Item
            {
                OwnerId = itemOwnerId,
                Content = "自己コメント",
                ItemKindJson = System.Text.Json.JsonSerializer.Serialize(new TagCommentItem(itemId, tagId))
            };
            db.Items.Add(ownCommentItem);
            await db.SaveChangesAsync();

            var ownRelation = new TagRelation
            {
                ItemId = itemId,
                TagId = tagId,
                OwnerId = itemOwnerId,
                Weight = 1,
                CommentItemId = ownCommentItem.Id
            };
            db.TagRelations.Add(ownRelation);

            // 2. コメント無しの他者 Relation（通知対象外）
            var noCommentRelation = new TagRelation
            {
                ItemId = itemId,
                TagId = tagId,
                OwnerId = commenterId,
                Weight = 1,
                CommentItemId = null
            };
            db.TagRelations.Add(noCommentRelation);
            await db.SaveChangesAsync();

            // Act: itemOwnerId 宛ての通知を取得
            NotificationRawData rawData = await sut.GetNotificationRawDataAsync(itemOwnerId);

            // Assert
            Assert.DoesNotContain(rawData.TagRelationComments, tr => tr.Id == ownRelation.Id);
            Assert.DoesNotContain(rawData.TagRelationComments, tr => tr.Id == noCommentRelation.Id);
        }
    }

    private sealed class DbContextFactoryStub(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}