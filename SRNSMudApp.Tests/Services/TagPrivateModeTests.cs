#region

using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     タグのプライベートモード（フォロワー限定、グループ限定、管理者非公開）における可視性制御およびクロススコープ整合性の単体・統合テスト。
/// </summary>
public class TagPrivateModeTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(
        ApplicationDbContext db,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        TagTableDataProvider tagTableProvider,
        TagDetailDataProvider tagDetailProvider,
        TagTreeDataProvider tagTreeProvider,
        TagCommandService tagCommandService,
        ItemListDataProvider itemListProvider,
        string authorId,
        string followerId,
        string strangerId,
        string groupMemberId,
        int groupId)> CreateScopeAsync()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var db = new ApplicationDbContext(_sharedDb.Options);
        var stubFactory = new DbContextFactoryStub(_sharedDb.Options);
        var tagEmbeddingMock = new Mock<ITagEmbeddingService>();

        var tagTableProvider = new TagTableDataProvider(stubFactory);
        var tagDetailProvider = new TagDetailDataProvider(stubFactory);
        var tagTreeProvider = new TagTreeDataProvider(stubFactory);
        var tagCommandService = new TagCommandService(stubFactory, tagEmbeddingMock.Object);
        var itemListProvider = new ItemListDataProvider(stubFactory, tagEmbeddingMock.Object);

        var authorId = $"author_{tid}";
        var followerId = $"follower_{tid}";
        var strangerId = $"stranger_{tid}";
        var groupMemberId = $"member_{tid}";

        await db.SeedUsersAsync(authorId, followerId, strangerId, groupMemberId);

        // フォロー関係: followerId が authorId をフォロー
        db.UserFollows.Add(new UserFollow
        {
            OwnerId = followerId,
            FollowedUserId = authorId
        });

        // ユーザーグループ: authorId がオーナー、groupMemberId がメンバー
        var group = new UserGroup
        {
            Name = $"Group_{tid}",
            OwnerId = authorId
        };
        db.UserGroups.Add(group);
        await db.SaveChangesAsync();

        db.UserGroupMembers.Add(new UserGroupMember
        {
            UserGroupId = group.Id,
            UserId = groupMemberId,
            OwnerId = authorId
        });
        await db.SaveChangesAsync();

        return (db, stubFactory, tagTableProvider, tagDetailProvider, tagTreeProvider, tagCommandService, itemListProvider,
            authorId, followerId, strangerId, groupMemberId, group.Id);
    }

    [Fact]
    public async Task PublicTag_ShouldBeVisibleToEveryone()
    {
        var (db, _, tagTableProvider, tagDetailProvider, _, _, _, authorId, followerId, strangerId, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var tag = new Tag
            {
                Name = $"PublicTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = false
            };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            // 1. WhereVisibleToUser クエリの検証
            var visibleAnonymous = await db.Tags.WhereVisibleToUser(db, null).ToListAsync();
            var visibleStranger = await db.Tags.WhereVisibleToUser(db, strangerId).ToListAsync();
            var visibleFollower = await db.Tags.WhereVisibleToUser(db, followerId).ToListAsync();

            Assert.Contains(visibleAnonymous, t => t.Id == tag.Id);
            Assert.Contains(visibleStranger, t => t.Id == tag.Id);
            Assert.Contains(visibleFollower, t => t.Id == tag.Id);

            // 2. TagTableDataProvider での取得検証
            var tableAnonymous = await tagTableProvider.GetAllTagsAsync(null);
            var tableStranger = await tagTableProvider.GetAllTagsAsync(strangerId);
            Assert.Contains(tableAnonymous, t => t.Id == tag.Id);
            Assert.Contains(tableStranger, t => t.Id == tag.Id);

            // 3. TagDetailDataProvider での取得検証
            var detailAnonymous = await tagDetailProvider.GetTagDetailAsync(tag.Id, null);
            var detailStranger = await tagDetailProvider.GetTagDetailAsync(tag.Id, strangerId);
            Assert.NotNull(detailAnonymous);
            Assert.NotNull(detailStranger);
        }
    }

    [Fact]
    public async Task PrivateTag_FollowersOnly_ShouldBeVisibleToAuthorAndFollowers_AndHiddenFromStrangers()
    {
        var (db, _, tagTableProvider, tagDetailProvider, _, _, _, authorId, followerId, strangerId, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var tag = new Tag
            {
                Name = $"FollowersTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = true,
                TargetUserGroupId = null
            };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            // 1. 未ログイン・見知らぬ人には非表示
            var visibleAnonymous = await db.Tags.WhereVisibleToUser(db, null).ToListAsync();
            var visibleStranger = await db.Tags.WhereVisibleToUser(db, strangerId).ToListAsync();
            Assert.DoesNotContain(visibleAnonymous, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleStranger, t => t.Id == tag.Id);

            var tableAnonymous = await tagTableProvider.GetAllTagsAsync(null);
            var tableStranger = await tagTableProvider.GetAllTagsAsync(strangerId);
            Assert.DoesNotContain(tableAnonymous, t => t.Id == tag.Id);
            Assert.DoesNotContain(tableStranger, t => t.Id == tag.Id);

            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, null));
            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, strangerId));

            // 2. 作成者本人およびフォロワーには表示
            var visibleAuthor = await db.Tags.WhereVisibleToUser(db, authorId).ToListAsync();
            var visibleFollower = await db.Tags.WhereVisibleToUser(db, followerId).ToListAsync();
            Assert.Contains(visibleAuthor, t => t.Id == tag.Id);
            Assert.Contains(visibleFollower, t => t.Id == tag.Id);

            var tableAuthor = await tagTableProvider.GetAllTagsAsync(authorId);
            var tableFollower = await tagTableProvider.GetAllTagsAsync(followerId);
            Assert.Contains(tableAuthor, t => t.Id == tag.Id);
            Assert.Contains(tableFollower, t => t.Id == tag.Id);

            Assert.NotNull(await tagDetailProvider.GetTagDetailAsync(tag.Id, authorId));
            Assert.NotNull(await tagDetailProvider.GetTagDetailAsync(tag.Id, followerId));
        }
    }

    [Fact]
    public async Task PrivateTag_GroupOnly_ShouldBeVisibleToGroupMembersAndOwner_AndHiddenFromOthers()
    {
        var (db, _, tagTableProvider, tagDetailProvider, _, _, _, authorId, followerId, strangerId, groupMemberId, groupId) = await CreateScopeAsync();
        await using (db)
        {
            var tag = new Tag
            {
                Name = $"GroupTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = true,
                TargetUserGroupId = groupId
            };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            // 1. 未ログイン、見知らぬ人、およびグループ外のフォロワーには非表示
            var visibleAnonymous = await db.Tags.WhereVisibleToUser(db, null).ToListAsync();
            var visibleStranger = await db.Tags.WhereVisibleToUser(db, strangerId).ToListAsync();
            var visibleFollower = await db.Tags.WhereVisibleToUser(db, followerId).ToListAsync();
            Assert.DoesNotContain(visibleAnonymous, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleStranger, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleFollower, t => t.Id == tag.Id);

            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, null));
            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, strangerId));
            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, followerId));

            // 2. 作成者本人およびグループメンバーには表示
            var visibleAuthor = await db.Tags.WhereVisibleToUser(db, authorId).ToListAsync();
            var visibleMember = await db.Tags.WhereVisibleToUser(db, groupMemberId).ToListAsync();
            Assert.Contains(visibleAuthor, t => t.Id == tag.Id);
            Assert.Contains(visibleMember, t => t.Id == tag.Id);

            Assert.NotNull(await tagDetailProvider.GetTagDetailAsync(tag.Id, authorId));
            Assert.NotNull(await tagDetailProvider.GetTagDetailAsync(tag.Id, groupMemberId));
        }
    }

    [Fact]
    public async Task AdminHiddenTag_ShouldNotBeVisibleToRegularUsers_ButVisibleToAdmin()
    {
        var (db, _, _, tagDetailProvider, _, _, _, authorId, followerId, strangerId, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var adminId = $"admin_{Guid.NewGuid().ToString("N")[..8]}";
            await db.SeedUsersAsync(adminId);

            var tag = new Tag
            {
                Name = $"AdminHiddenTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = false,
                IsAdminHidden = true,
                AdminHiddenAt = DateTimeOffset.UtcNow,
                AdminHiddenReason = "Violation"
            };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            // 1. WhereVisibleToUser クエリの検証 (通常ユーザー vs 管理者)
            var visibleAnonymous = await db.Tags.WhereVisibleToUser(db, null, isAdmin: false).ToListAsync();
            var visibleAuthor = await db.Tags.WhereVisibleToUser(db, authorId, isAdmin: false).ToListAsync();
            var visibleFollower = await db.Tags.WhereVisibleToUser(db, followerId, isAdmin: false).ToListAsync();
            var visibleStranger = await db.Tags.WhereVisibleToUser(db, strangerId, isAdmin: false).ToListAsync();
            var visibleAdmin = await db.Tags.WhereVisibleToUser(db, adminId, isAdmin: true).ToListAsync();

            Assert.DoesNotContain(visibleAnonymous, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleAuthor, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleFollower, t => t.Id == tag.Id);
            Assert.DoesNotContain(visibleStranger, t => t.Id == tag.Id);
            Assert.Contains(visibleAdmin, t => t.Id == tag.Id);

            // 2. TagDetailDataProvider での検証
            Assert.Null(await tagDetailProvider.GetTagDetailAsync(tag.Id, authorId, isAdmin: false));
            Assert.NotNull(await tagDetailProvider.GetTagDetailAsync(tag.Id, adminId, isAdmin: true));
        }
    }

    [Fact]
    public async Task CrossScope_PrivateTagOnPublicItem_ShouldBeMaskedForUnprivilegedUser()
    {
        var (db, _, _, _, _, _, itemListProvider, authorId, followerId, strangerId, _, _) = await CreateScopeAsync();
        await using (db)
        {
            // パブリックなアイテム
            var item = new Item
            {
                Content = "Public post with private tag",
                OwnerId = authorId,
                IsPrivate = false
            };
            db.Items.Add(item);

            // フォロワー限定のプライベートタグ
            var privateTag = new Tag
            {
                Name = $"SecretTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = true,
                TargetUserGroupId = null
            };
            // パブリックなタグ
            var publicTag = new Tag
            {
                Name = $"PublicTag_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = false
            };
            db.Tags.AddRange(privateTag, publicTag);
            await db.SaveChangesAsync();

            // アイテムに両方のタグを付与
            db.TagRelations.Add(new TagRelation { ItemId = item.Id, TagId = privateTag.Id, OwnerId = authorId });
            db.TagRelations.Add(new TagRelation { ItemId = item.Id, TagId = publicTag.Id, OwnerId = authorId });
            await db.SaveChangesAsync();

            // 1. 未認可ユーザー（部外者・未ログイン）がアイテム一覧を取得したとき
            var strangerResult = await itemListProvider.LoadItemsAndTagsAsync([], [], currentUserId: strangerId);
            var retrievedItemStranger = strangerResult.Items.FirstOrDefault(i => i.Id == item.Id);
            Assert.NotNull(retrievedItemStranger);
            // パブリックタグは見え、プライベートタグはマスクされて除外されていること
            Assert.Contains(retrievedItemStranger.TagRelations, it => it.TagId == publicTag.Id);
            Assert.DoesNotContain(retrievedItemStranger.TagRelations, it => it.TagId == privateTag.Id);

            // 2. 認可ユーザー（フォロワー）がアイテム一覧を取得したとき
            var followerResult = await itemListProvider.LoadItemsAndTagsAsync([], [], currentUserId: followerId);
            var retrievedItemFollower = followerResult.Items.FirstOrDefault(i => i.Id == item.Id);
            Assert.NotNull(retrievedItemFollower);
            // フォロワーには両方のタグが見えること
            Assert.Contains(retrievedItemFollower.TagRelations, it => it.TagId == publicTag.Id);
            Assert.Contains(retrievedItemFollower.TagRelations, it => it.TagId == privateTag.Id);
        }
    }

    [Fact]
    public async Task SystemTag_AlwaysRemainsPublic()
    {
        var (db, _, _, _, _, tagCommandService, _, authorId, _, _, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var systemTag = new Tag
            {
                Name = "good",
                OwnerId = authorId,
                IsPrivate = false
            };
            db.Tags.Add(systemTag);
            await db.SaveChangesAsync();

            // システムタグをプライベートに変更しようと試みる
            await tagCommandService.UpdateTagAsync(systemTag.Id, "good", "content", isPrivate: true, targetUserGroupId: 123);

            // DBから再取得してパブリック固定されていることを確認
            var reloaded = await db.Tags.FindAsync(systemTag.Id);
            Assert.NotNull(reloaded);
            Assert.False(reloaded.IsPrivate);
            Assert.Null(reloaded.TargetUserGroupId);
        }
    }

    [Fact]
    public async Task CreateChildTag_WhenParentIsPrivate_InheritsParentScope()
    {
        var (db, _, _, _, tagTreeProvider, _, _, authorId, _, _, _, groupId) = await CreateScopeAsync();
        await using (db)
        {
            var parentTag = new Tag
            {
                Name = $"PrivateParent_{Guid.NewGuid():N}",
                OwnerId = authorId,
                IsPrivate = true,
                TargetUserGroupId = groupId
            };
            db.Tags.Add(parentTag);
            await db.SaveChangesAsync();

            // 子タグを追加（引数で isPrivate: false を渡しても親が private なら継承されること）
            var childTag = new Tag
            {
                Name = $"ChildTag_{Guid.NewGuid():N}",
                Content = "desc",
                OwnerId = authorId,
                ParentTagId = parentTag.Id,
                IsPrivate = false,
                TargetUserGroupId = null
            };
            await tagTreeProvider.AddTagAsync(childTag);

            Assert.True(childTag.IsPrivate);
            Assert.Equal(groupId, childTag.TargetUserGroupId);
        }
    }

    private sealed class DbContextFactoryStub(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }
}