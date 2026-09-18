#region

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     TagLockService の単体テスト (MSSQL Testcontainers)。
///     階層ロック設定、祖先ロック、および兄弟ロック制約を検証する。
/// </summary>
public class TagLockServiceTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(ApplicationDbContext db, TagLockService sut, string userId, string tid)> CreateScopeAsync()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var db = new ApplicationDbContext(_sharedDb.Options);
        var stubFactory = new DbContextFactoryStub(_sharedDb.Options);
        var sut = new TagLockService(stubFactory);

        var userId = $"user_{tid}";
        await db.SeedUsersAsync(userId);

        return (db, sut, userId, tid);
    }

    [Fact]
    public async Task HierarchyLockSetting_CanBeUpdatedAndRetrieved()
    {
        var (db, sut, _, _) = await CreateScopeAsync();
        await using (db)
        {
            // 初期状態は 0
            var initialLevel = await sut.GetLockedHierarchyLevelAsync();
            Assert.True(initialLevel >= 0);

            // 2 階層目に設定
            await sut.SetLockedHierarchyLevelAsync(2);
            var updatedLevel = await sut.GetLockedHierarchyLevelAsync();
            Assert.Equal(2, updatedLevel);

            // 0 に戻す
            await sut.SetLockedHierarchyLevelAsync(0);
            var resetLevel = await sut.GetLockedHierarchyLevelAsync();
            Assert.Equal(0, resetLevel);
        }
    }

    [Fact]
    public async Task SiblingLock_WhenOneChildLocked_SiblingsAreLockedAgainstEditAndDelete()
    {
        var (db, sut, userId, tid) = await CreateScopeAsync();
        await using (db)
        {
            await sut.SetLockedHierarchyLevelAsync(0); // 階層ロック無効

            Tag? rootTag = await db.Tags.FirstAsync(t => t.Name == Tag.RootTagName);

            // Level 1 親タグ作成
            HierarchyId parentNode = rootTag.Node.GetDescendant(null, null);
            var parentTag = new Tag
            {
                Name = $"Parent_{tid}",
                OwnerId = userId,
                Node = parentNode,
                ParentTagId = rootTag.Id,
                IsLocked = false
            };
            db.Tags.Add(parentTag);
            await db.SaveChangesAsync();

            // 子タグ1（Child1）と子タグ2（Child2）を作成（同じ親）
            HierarchyId child1Node = parentNode.GetDescendant(null, null);
            HierarchyId child2Node = parentNode.GetDescendant(child1Node, null);

            var child1 = new Tag
            {
                Name = $"Child1_{tid}",
                OwnerId = userId,
                Node = child1Node,
                ParentTagId = parentTag.Id,
                IsLocked = false
            };
            var child2 = new Tag
            {
                Name = $"Child2_{tid}",
                OwnerId = userId,
                Node = child2Node,
                ParentTagId = parentTag.Id,
                IsLocked = false
            };
            db.Tags.AddRange(child1, child2);
            await db.SaveChangesAsync();

            // 初期状態: どちらもロックされていない
            Assert.False(await sut.IsTagOrSiblingLockedAsync(child1.Id));
            Assert.False(await sut.IsTagOrSiblingLockedAsync(child2.Id));
            Assert.False(await sut.IsChildCreationRestrictedAsync(parentTag.Id));

            // Child1 を個別ロック
            await sut.ToggleTagLockAsync(child1.Id);

            // Child1 は自身がロックされているため true
            Assert.True(await sut.IsTagOrSiblingLockedAsync(child1.Id));

            // Child2 は自身はロックされていないが兄弟（Child1）がロックされているため true
            Assert.True(await sut.IsTagOrSiblingLockedAsync(child2.Id));

            // 親配下への新規作成も兄弟ロック制約により制限される
            Assert.True(await sut.IsChildCreationRestrictedAsync(parentTag.Id));

            // Child1 のロックを解除
            await sut.ToggleTagLockAsync(child1.Id);

            // 解除後は両方 false
            Assert.False(await sut.IsTagOrSiblingLockedAsync(child1.Id));
            Assert.False(await sut.IsTagOrSiblingLockedAsync(child2.Id));
            Assert.False(await sut.IsChildCreationRestrictedAsync(parentTag.Id));
        }
    }

    [Fact]
    public async Task HierarchyLevelLock_LocksAllTagsAtOrBelowLevel()
    {
        var (db, sut, userId, tid) = await CreateScopeAsync();
        await using (db)
        {
            Tag? rootTag = await db.Tags.FirstAsync(t => t.Name == Tag.RootTagName);

            // Level 1 タグ
            HierarchyId level1Node = rootTag.Node.GetDescendant(null, null);
            var tagLevel1 = new Tag
            {
                Name = $"L1_{tid}",
                OwnerId = userId,
                Node = level1Node,
                ParentTagId = rootTag.Id,
                IsLocked = false
            };
            db.Tags.Add(tagLevel1);
            await db.SaveChangesAsync();

            // Level 2 タグ
            HierarchyId level2Node = level1Node.GetDescendant(null, null);
            var tagLevel2 = new Tag
            {
                Name = $"L2_{tid}",
                OwnerId = userId,
                Node = level2Node,
                ParentTagId = tagLevel1.Id,
                IsLocked = false
            };
            db.Tags.Add(tagLevel2);
            await db.SaveChangesAsync();

            // Level 1 までロック
            await sut.SetLockedHierarchyLevelAsync(1);

            Assert.True(await sut.IsTagOrSiblingLockedAsync(tagLevel1.Id));
            Assert.False(await sut.IsTagOrSiblingLockedAsync(tagLevel2.Id));

            // Level 2 までロック
            await sut.SetLockedHierarchyLevelAsync(2);

            Assert.True(await sut.IsTagOrSiblingLockedAsync(tagLevel1.Id));
            Assert.True(await sut.IsTagOrSiblingLockedAsync(tagLevel2.Id));

            // クリーンアップ
            await sut.SetLockedHierarchyLevelAsync(0);
        }
    }

    [Fact]
    public async Task LockAncestorsAsync_LocksAllAncestorsOfTargetTag()
    {
        var (db, sut, userId, tid) = await CreateScopeAsync();
        await using (db)
        {
            await sut.SetLockedHierarchyLevelAsync(0);

            Tag? rootTag = await db.Tags.FirstAsync(t => t.Name == Tag.RootTagName);

            // GrandParent (Level 1)
            HierarchyId gpNode = rootTag.Node.GetDescendant(null, null);
            var grandParent = new Tag
            {
                Name = $"GP_{tid}",
                OwnerId = userId,
                Node = gpNode,
                ParentTagId = rootTag.Id,
                IsLocked = false
            };
            db.Tags.Add(grandParent);
            await db.SaveChangesAsync();

            // Parent (Level 2)
            HierarchyId pNode = gpNode.GetDescendant(null, null);
            var parent = new Tag
            {
                Name = $"P_{tid}",
                OwnerId = userId,
                Node = pNode,
                ParentTagId = grandParent.Id,
                IsLocked = false
            };
            db.Tags.Add(parent);
            await db.SaveChangesAsync();

            // Child (Level 3)
            HierarchyId cNode = pNode.GetDescendant(null, null);
            var child = new Tag
            {
                Name = $"C_{tid}",
                OwnerId = userId,
                Node = cNode,
                ParentTagId = parent.Id,
                IsLocked = false
            };
            db.Tags.Add(child);
            await db.SaveChangesAsync();

            // 初期状態: 祖先ロックは false
            Assert.False(await sut.AreAncestorsLockedAsync(child.Id));

            // 祖先ロック実行
            await sut.LockAncestorsAsync(child.Id);

            // 祖先がロックされたことを検証
            Assert.True(await sut.AreAncestorsLockedAsync(child.Id));

            // DB上の親と祖父の IsLocked が true になっているか検証
            Tag updatedGp = await db.Tags.AsNoTracking().FirstAsync(t => t.Id == grandParent.Id);
            Tag updatedP = await db.Tags.AsNoTracking().FirstAsync(t => t.Id == parent.Id);
            Tag updatedC = await db.Tags.AsNoTracking().FirstAsync(t => t.Id == child.Id);

            Assert.True(updatedGp.IsLocked);
            Assert.True(updatedP.IsLocked);
            Assert.False(updatedC.IsLocked); // 対象タグ自身は祖先ではないためロックされない

            // 祖先ロック解除実行
            await sut.UnlockAncestorsAsync(child.Id);
            Assert.False(await sut.AreAncestorsLockedAsync(child.Id));

            updatedGp = await db.Tags.AsNoTracking().FirstAsync(t => t.Id == grandParent.Id);
            updatedP = await db.Tags.AsNoTracking().FirstAsync(t => t.Id == parent.Id);
            Assert.False(updatedGp.IsLocked);
            Assert.False(updatedP.IsLocked);
        }
    }

    private sealed class DbContextFactoryStub(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }
}