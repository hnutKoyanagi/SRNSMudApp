using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TagHierarchyAndImportServiceTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;
    private IDbContextFactory<ApplicationDbContext> _dbFactory = null!;
    private readonly Mock<ITagEmbeddingService> _embeddingMock = new();
    private readonly Mock<IHttpClientFactory> _httpFactoryMock = new();
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
        var services = new ServiceCollection();
        _ = services.AddMsSqlDbFactory(_sharedDb.ConnectionString);

        var sp = services.BuildServiceProvider();
        _dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        _ = _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        await using ApplicationDbContext db = await _dbFactory.CreateDbContextAsync();
        await db.SeedUsersAsync("system", "import_user1");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SuggestParentTagAsync_PrefersLevel2OrHigherTags()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var testUser = $"user_{tid}";

        await using (ApplicationDbContext db = await _dbFactory.CreateDbContextAsync())
        {
            await db.SeedUsersAsync(testUser);

            var rootTag = await db.Tags.FirstOrDefaultAsync(t => t.Name == SRNSMudApp.Data.Tag.RootTagName);
            if (rootTag is null)
            {
                rootTag = new SRNSMudApp.Data.Tag
                {
                    Name = SRNSMudApp.Data.Tag.RootTagName,
                    OwnerId = "system",
                    IsSystem = true,
                    Node = HierarchyId.GetRoot(),
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };
                _ = db.Tags.Add(rootTag);
                _ = await db.SaveChangesAsync();
            }

            var level1Tag = new SRNSMudApp.Data.Tag
            {
                Name = $"大分類_{tid}",
                OwnerId = testUser,
                ParentTagId = rootTag.Id,
                Node = HierarchyId.Parse("/999/"),
                Embedding = [0.1f, 0.2f, 0.3f],
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            var level2Tag = new SRNSMudApp.Data.Tag
            {
                Name = $"中分類_{tid}",
                OwnerId = testUser,
                ParentTagId = level1Tag.Id,
                Node = HierarchyId.Parse("/999/1/"),
                Embedding = [0.1f, 0.2f, 0.3f],
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            db.Tags.AddRange(level1Tag, level2Tag);
            _ = await db.SaveChangesAsync();
        }

        var service = new TagHierarchyService(
            _dbFactory,
            _embeddingMock.Object,
            _httpFactoryMock.Object,
            _config,
            NullLogger<TagHierarchyService>.Instance);

        // Act (jsRuntime = null gives fallback to best candidate)
        SRNSMudApp.Data.Tag? result = await service.SuggestParentTagAsync($"新規タグ_{tid}", null);

        // Assert: Level >= 2 のタグ（中分類）が選ばれること
        Assert.NotNull(result);
        Assert.StartsWith("中分類_", result.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteImportAsync_CreatesAllEntitiesInTransaction()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var testUser = $"import_user_{tid}";

        await using (ApplicationDbContext db = await _dbFactory.CreateDbContextAsync())
        {
            await db.SeedUsersAsync(testUser);
        }

        var provider = new TaggingImportDataProvider(
            _dbFactory,
            Mock.Of<ITagHierarchyService>(),
            _embeddingMock.Object,
            TimeProvider.System,
            NullLogger<TaggingImportDataProvider>.Instance);

        var tagA = await provider.CreateTagAsync($"人格_{tid}", testUser, false, null);
        var tagB = await provider.CreateTagAsync($"自由_{tid}", testUser, false, null);

        var payload = new TaggingImportPayload
        {
            Item =
            [
                new ImportItemDto
                {
                    ItemId = $"item-{tid}",
                    SequenceOrder = 1,
                    Content = $"全ての[人格](/TagDetail/{tagA.Id})が[自由](/TagDetail/{tagB.Id})を持つ。"
                }
            ],
            TagRelations =
            [
                new ImportTagRelationDto
                {
                    RelationId = $"rel-001-{tid}",
                    SourceItemId = $"item-{tid}",
                    Tag = new ImportTagDto { Name = tagA.Name, TagKind = "UserCustomTag" }
                },
                new ImportTagRelationDto
                {
                    RelationId = $"rel-002-{tid}",
                    SourceItemId = $"item-{tid}",
                    Tag = new ImportTagDto { Name = tagB.Name, TagKind = "UserCustomTag" }
                }
            ],
            TagEdges =
            [
                new ImportTagEdgeDto
                {
                    EdgeId = $"edge-001-{tid}",
                    SourceTagId = $"rel-001-{tid}",
                    TargetTagId = $"rel-002-{tid}",
                    AppliedTags = [$"持つ_{tid}"]
                }
            ]
        };

        var relationMap = new Dictionary<string, int>
        {
            [$"rel-001-{tid}"] = tagA.Id,
            [$"rel-002-{tid}"] = tagB.Id
        };

        List<string> processedContents = [$"全ての[人格](/TagDetail/{tagA.Id})が[自由](/TagDetail/{tagB.Id})を持つ。"];

        // Act
        TaggingImportResult result = await provider.ExecuteImportAsync(
            testUser, payload, relationMap, processedContents);

        // Assert
        Assert.Equal(1, result.CreatedItemsCount);
        Assert.Equal(2, result.CreatedRelationsCount);
        Assert.Equal(1, result.CreatedEdgesCount);
        Assert.Equal(1, result.CreatedAttachmentsCount);

        // Verify in DB
        await using (ApplicationDbContext verifyDb = await _dbFactory.CreateDbContextAsync())
        {
            Assert.True(await verifyDb.Items.AnyAsync(i => i.OwnerId == testUser));
            Assert.True(await verifyDb.TagRelations.AnyAsync(tr => tr.OwnerId == testUser));
            Assert.True(await verifyDb.TagEdges.AnyAsync(te => te.OwnerId == testUser));
            Assert.True(await verifyDb.TagEdgeTagAttachments.AnyAsync(a => a.OwnerId == testUser));
        }
    }
}