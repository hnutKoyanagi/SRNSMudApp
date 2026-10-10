#region

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
/// <see cref="TagCardDataProvider" /> のタグ交換・リレーション操作ロジックおよび同時操作競合の単体テスト (MSSQL Testcontainers)。
/// </summary>
public class TagCardDataProviderTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(ApplicationDbContext db, TagCardDataProvider sut, string userId, int targetTagId, int tag1Id, int tag2Id, int tag3Id, string tid)> CreateScopeAsync()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var db = new ApplicationDbContext(_sharedDb.Options);
        var sut = new TagCardDataProvider(new DbContextFactoryStub(_sharedDb.Options));

        var userId = $"tagcard_user_{tid}";
        await db.SeedUsersAsync(userId);

        var targetTag = new Tag { Name = $"Target_{tid}", OwnerId = userId, CachedWeight = 0 };
        var tag1 = new Tag { Name = $"Tag1_{tid}", OwnerId = userId, CachedWeight = 1 };
        var tag2 = new Tag { Name = $"Tag2_{tid}", OwnerId = userId, CachedWeight = 1 };
        var tag3 = new Tag { Name = $"Tag3_{tid}", OwnerId = userId, CachedWeight = 1 };

        db.Tags.AddRange(targetTag, tag1, tag2, tag3);
        await db.SaveChangesAsync();

        return (db, sut, userId, targetTag.Id, tag1.Id, tag2.Id, tag3.Id, tid);
    }

    [Fact]
    public async Task ChangeRelationTagAsync_Success_UpdatesRelationTargetTag()
    {
        var (db, sut, userId, targetTagId, tag1Id, tag2Id, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var relation = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag1Id,
                Weight = 1,
                OwnerId = userId
            };
            db.TagRelationToTags.Add(relation);
            await db.SaveChangesAsync();

            // tag1 から tag2 へ交換
            TagCardOperationResult result = await sut.ChangeRelationTagAsync(relation.Id, targetTagId, tag2Id, userId);

            Assert.Equal(TagCardOperationResult.Success, result);

            await using var verifyDb = new ApplicationDbContext(_sharedDb.Options);
            TagRelationToTag? updated = await verifyDb.TagRelationToTags.FindAsync(relation.Id);
            Assert.NotNull(updated);
            Assert.Equal(tag2Id, updated.TagId);
            Assert.NotEqual(default, updated.UpdatedDate);
        }
    }

    [Fact]
    public async Task ChangeRelationTagAsync_WhenTargetAlreadyHasNewTag_ReturnsAlreadyExists()
    {
        var (db, sut, userId, targetTagId, tag1Id, tag2Id, _, _) = await CreateScopeAsync();
        await using (db)
        {
            // targetTag に対して tag1 と tag2 が既に両方紐付いている
            var relation1 = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag1Id,
                Weight = 1,
                OwnerId = userId
            };
            var relation2 = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag2Id,
                Weight = 1,
                OwnerId = userId
            };
            db.TagRelationToTags.AddRange(relation1, relation2);
            await db.SaveChangesAsync();

            // relation1 を tag2 に変更しようとする（既に存在するため重複）
            TagCardOperationResult result = await sut.ChangeRelationTagAsync(relation1.Id, targetTagId, tag2Id, userId);

            Assert.Equal(TagCardOperationResult.AlreadyExists, result);

            // 元のリレーションは書き換わっていないこと
            await using var verifyDb = new ApplicationDbContext(_sharedDb.Options);
            TagRelationToTag? unchanged = await verifyDb.TagRelationToTags.FindAsync(relation1.Id);
            Assert.NotNull(unchanged);
            Assert.Equal(tag1Id, unchanged.TagId);
        }
    }

    [Fact]
    public async Task ChangeRelationTagAsync_WhenRelationDeleted_ReturnsNotFound()
    {
        var (db, sut, userId, targetTagId, _, tag2Id, _, _) = await CreateScopeAsync();
        await using (db)
        {
            // 存在しない relation ID を指定
            const int nonExistentRelationId = 999999;
            TagCardOperationResult result = await sut.ChangeRelationTagAsync(nonExistentRelationId, targetTagId, tag2Id, userId);

            Assert.Equal(TagCardOperationResult.NotFound, result);
        }
    }

    [Fact]
    public async Task ChangeRelationTagAsync_ConcurrentSwapToSameTag_OnlyOneSucceedsOtherFails()
    {
        var (db, sut, userId, targetTagId, tag1Id, tag2Id, tag3Id, _) = await CreateScopeAsync();
        await using (db)
        {
            // targetTag に tag1 と tag2 が紐付いている
            var relation1 = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag1Id,
                Weight = 1,
                OwnerId = userId
            };
            var relation2 = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag2Id,
                Weight = 1,
                OwnerId = userId
            };
            db.TagRelationToTags.AddRange(relation1, relation2);
            await db.SaveChangesAsync();

            // 両方のリレーションを同時に同じ tag3 に付け替えようとする
            // 1人目 (relation1 -> tag3)
            TagCardOperationResult result1 = await sut.ChangeRelationTagAsync(relation1.Id, targetTagId, tag3Id, userId);
            // 2人目 (relation2 -> tag3)
            TagCardOperationResult result2 = await sut.ChangeRelationTagAsync(relation2.Id, targetTagId, tag3Id, userId);

            // 1人目は成功し、2人目は AlreadyExists として拒否されること
            Assert.Equal(TagCardOperationResult.Success, result1);
            Assert.Equal(TagCardOperationResult.AlreadyExists, result2);

            // DB上の状態検証: targetTagId に tag3Id は1件のみ紐付いていること
            await using var verifyDb = new ApplicationDbContext(_sharedDb.Options);
            List<TagRelationToTag> tag3Relations = await verifyDb.TagRelationToTags
                .Where(tr => tr.TargetTagId == targetTagId && tr.TagId == tag3Id)
                .ToListAsync();
            Assert.Single(tag3Relations);
        }
    }

    [Fact]
    public async Task ChangeRelationTagAsync_ConcurrentRemoveAndSwap_SwapReturnsNotFoundWhenRemovedFirst()
    {
        var (db, sut, userId, targetTagId, tag1Id, tag2Id, _, _) = await CreateScopeAsync();
        await using (db)
        {
            var relation = new TagRelationToTag
            {
                TargetTagId = targetTagId,
                TagId = tag1Id,
                Weight = 1,
                OwnerId = userId
            };
            db.TagRelationToTags.Add(relation);
            await db.SaveChangesAsync();

            // 競合シナリオ: 同時に操作され、先に削除操作が完了した場合
            TagCardOperationResult removeResult = await sut.RemoveRelationAsync(relation.Id, userId);
            Assert.Equal(TagCardOperationResult.Success, removeResult);

            // その後で交換操作が実行された場合、対象リレーションが消えているため NotFound となること
            TagCardOperationResult swapResult = await sut.ChangeRelationTagAsync(relation.Id, targetTagId, tag2Id, userId);
            Assert.Equal(TagCardOperationResult.NotFound, swapResult);
        }
    }

    /// <summary>テスト用のシンプルなファクトリ。</summary>
    private sealed class DbContextFactoryStub(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}