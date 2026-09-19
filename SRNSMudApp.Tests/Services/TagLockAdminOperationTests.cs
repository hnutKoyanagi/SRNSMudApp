using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     ロックされたタグツリーに対する管理者権限操作（Adminバイパス）の単体テスト。
///     TagCommandService, TagTableDataProvider, TagTreeDataProvider が
///     isAdmin=true の場合にロック制約をバイパスして操作可能であることを検証する。
/// </summary>
public class TagLockAdminOperationTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private (ApplicationDbContext dbContext, IDbContextFactory<ApplicationDbContext> dbFactory, Mock<ITagLockService> lockServiceMock, Mock<ITagEmbeddingService> embeddingMock, string tid) CreateScope()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var dbContext = new ApplicationDbContext(_sharedDb.Options);
        var mockDbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mockDbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_sharedDb.Options));

        var lockServiceMock = new Mock<ITagLockService>();
        var embeddingMock = new Mock<ITagEmbeddingService>();

        return (dbContext, mockDbFactory.Object, lockServiceMock, embeddingMock, tid);
    }

    [Fact]
    public async Task TagCommandService_UpdateTagAsync_WhenLocked_AdminSucceeds_NonAdminFails()
    {
        var (dbContext, dbFactory, lockServiceMock, embeddingMock, tid) = CreateScope();
        await using (dbContext)
        {
            var userId = $"u_{tid}";
            await dbContext.SeedUsersAsync(userId);

            var tag = new Tag
            {
                Name = $"LockedTag_{tid}",
                Content = "Initial",
                OwnerId = userId
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();

            // ロックサービスはロック中と判定
            lockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(tag.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sut = new TagCommandService(dbFactory, embeddingMock.Object, lockServiceMock.Object);

            // 非管理者はロックされているため更新失敗 (false)
            var nonAdminResult = await sut.UpdateTagAsync(tag.Id, tag.Name, "Updated NonAdmin", false, null, isAdmin: false);
            Assert.False(nonAdminResult);

            // 管理者はロックをバイパスして更新成功 (true)
            var adminResult = await sut.UpdateTagAsync(tag.Id, tag.Name, "Updated Admin", false, null, isAdmin: true);
            Assert.True(adminResult);

            dbContext.ChangeTracker.Clear();
            var reloaded = await dbContext.Tags.FindAsync(tag.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Updated Admin", reloaded.Content);
        }
    }

    [Fact]
    public async Task TagCommandService_CreateTagAsync_WhenParentRestricted_AdminSucceeds_NonAdminFails()
    {
        var (dbContext, dbFactory, lockServiceMock, embeddingMock, tid) = CreateScope();
        await using (dbContext)
        {
            var userId = $"u_{tid}";
            await dbContext.SeedUsersAsync(userId);

            var parentTag = new Tag
            {
                Name = $"ParentTag_{tid}",
                OwnerId = userId
            };
            dbContext.Tags.Add(parentTag);
            await dbContext.SaveChangesAsync();

            // 親タグ配下は子作成制限中
            lockServiceMock.Setup(s => s.IsChildCreationRestrictedAsync(parentTag.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sut = new TagCommandService(dbFactory, embeddingMock.Object, lockServiceMock.Object);

            var childTagNonAdmin = new Tag
            {
                Name = $"ChildNonAdmin_{tid}",
                ParentTagId = parentTag.Id,
                OwnerId = userId
            };

            // 非管理者は子タグ作成時に InvalidOperationException がスローされる
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateTagAsync(childTagNonAdmin, isAdmin: false));

            var childTagAdmin = new Tag
            {
                Name = $"ChildAdmin_{tid}",
                ParentTagId = parentTag.Id,
                OwnerId = userId
            };

            // 管理者は子タグ作成成功
            await sut.CreateTagAsync(childTagAdmin, isAdmin: true);

            dbContext.ChangeTracker.Clear();
            var reloaded = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == childTagAdmin.Name);
            Assert.NotNull(reloaded);
        }
    }

    [Fact]
    public async Task TagTableDataProvider_DeleteTagAsync_WhenLocked_AdminSucceeds_NonAdminFails()
    {
        var (dbContext, dbFactory, lockServiceMock, _, tid) = CreateScope();
        await using (dbContext)
        {
            var userId = $"u_{tid}";
            await dbContext.SeedUsersAsync(userId);

            var tag = new Tag
            {
                Name = $"DelTag_{tid}",
                OwnerId = userId
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();

            lockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(tag.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sut = new TagTableDataProvider(dbFactory, lockServiceMock.Object);

            // 非管理者は削除失敗 (false)
            var nonAdminResult = await sut.DeleteTagAsync(tag.Id, isAdmin: false);
            Assert.False(nonAdminResult);

            // 管理者は削除成功 (true)
            var adminResult = await sut.DeleteTagAsync(tag.Id, isAdmin: true);
            Assert.True(adminResult);

            dbContext.ChangeTracker.Clear();
            var reloaded = await dbContext.Tags.FindAsync(tag.Id);
            Assert.Null(reloaded);
        }
    }

    [Fact]
    public async Task TagTreeDataProvider_DeleteTagsAsync_WhenLocked_AdminSucceeds_NonAdminFails()
    {
        var (dbContext, dbFactory, lockServiceMock, _, tid) = CreateScope();
        await using (dbContext)
        {
            var userId = $"u_{tid}";
            await dbContext.SeedUsersAsync(userId);

            var tag = new Tag
            {
                Name = $"TreeDelTag_{tid}",
                OwnerId = userId
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();

            lockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(tag.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sut = new TagTreeDataProvider(dbFactory, lockServiceMock.Object);

            // 非管理者はロックされているためスキップ（UnauthorizedNamesに追加、DeletedCount=0）
            TagTreeDeleteResult nonAdminResult = await sut.DeleteTagsAsync(userId, [tag.Id], isAdmin: false);
            Assert.False(nonAdminResult.HasDeleted);
            Assert.Contains(tag.Name, nonAdminResult.UnauthorizedNames);

            // 管理者はロックをバイパスして削除成功
            TagTreeDeleteResult adminResult = await sut.DeleteTagsAsync(userId, [tag.Id], isAdmin: true);
            Assert.True(adminResult.HasDeleted);
            Assert.Equal(1, adminResult.DeletedCount);

            dbContext.ChangeTracker.Clear();
            var reloaded = await dbContext.Tags.FindAsync(tag.Id);
            Assert.Null(reloaded);
        }
    }

    [Fact]
    public async Task TagDetailDataProvider_DeleteTagAsync_WhenLocked_AdminSucceeds_NonAdminFails()
    {
        var (dbContext, dbFactory, lockServiceMock, _, tid) = CreateScope();
        await using (dbContext)
        {
            var userId = $"u_{tid}";
            await dbContext.SeedUsersAsync(userId);

            var tag = new Tag
            {
                Name = $"DetailDelTag_{tid}",
                OwnerId = userId
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();

            lockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(tag.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sut = new TagDetailDataProvider(dbFactory, lockServiceMock.Object);

            // 非管理者は削除失敗 (false)
            var nonAdminResult = await sut.DeleteTagAsync(tag.Id, isAdmin: false);
            Assert.False(nonAdminResult);

            // 管理者は削除成功 (true)
            var adminResult = await sut.DeleteTagAsync(tag.Id, isAdmin: true);
            Assert.True(adminResult);

            dbContext.ChangeTracker.Clear();
            var reloaded = await dbContext.Tags.FindAsync(tag.Id);
            Assert.Null(reloaded);
        }
    }
}