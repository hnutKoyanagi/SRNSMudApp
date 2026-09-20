#region

using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     InternalLinkConversionService の単体テスト。
///     タグ名検出、類似度スコアリング、URL スキップ、置換ロジック、
///     オーバーラップ解決の各動作を検証する。
/// </summary>
public class InternalLinkConversionServiceTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private InternalLinkConversionService CreateService(ITagSearchQueryService? queryService = null)
    {
        if (queryService != null)
        {
            return new InternalLinkConversionService(queryService);
        }

        var dbFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbFactoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_sharedDb.Options));

        var embeddingMock = new Mock<ITagEmbeddingService>();
        var tagSearchQueryService = new TagSearchQueryService(
            dbFactoryMock.Object,
            embeddingMock.Object);

        return new InternalLinkConversionService(tagSearchQueryService);
    }

    private async Task<Tag> CreateTagAsync(string name, string userId)
    {
        await using var db = new ApplicationDbContext(_sharedDb.Options);
        await db.SeedUsersAsync(userId);

        var tag = new Tag { Name = name, OwnerId = userId };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_WhenContentIsEmpty_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.DetectLinkCandidatesAsync(string.Empty);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_WhenContentIsNull_ReturnsEmpty()
    {
        var service = CreateService();

        var result = await service.DetectLinkCandidatesAsync(null!);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_ExactMatch_ReturnsAutoReplaceCandidate()
    {
        // Arrange: DBにタグ名を登録
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"lnk_{tid}";
        var tag = await CreateTagAsync($"プログラミング{tid}", userId);
        var content = $"今日は{tag.Name}を学んだ";

        var service = CreateService();

        // Act
        var result = await service.DetectLinkCandidatesAsync(content, 0.85f);

        // Assert: 完全一致は自動置換対象
        Assert.Contains(result.AutoReplaceCandidates, c =>
            c.TagId == tag.Id &&
            c.Similarity >= 0.95f &&
            c.IsAutoReplace);
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_SkipsExistingInternalLinks()
    {
        // Arrange: テキスト内に既存の内部リンク (/TagDetail/999) がある場合
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"lnk_{tid}";
        var tag = await CreateTagAsync($"Blazor{tid}", userId);
        var content = $"/TagDetail/999 について {tag.Name} のメモ";

        var service = CreateService();

        // Act
        var result = await service.DetectLinkCandidatesAsync(content, 0.85f);

        // Assert: 既存リンク部分はスキップされ、タグ名部分のみ候補になる
        Assert.Contains(result.AutoReplaceCandidates, c => c.TagId == tag.Id);
        Assert.DoesNotContain(result.AutoReplaceCandidates, c => c.OriginalText.Contains("/TagDetail/"));
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_SkipsShortTagNames()
    {
        // Arrange: 2文字未満のタグ名はスキップされる
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"lnk_{tid}";
        await CreateTagAsync("A", userId); // 1文字
        var content = "A is important";

        var service = CreateService();

        // Act
        var result = await service.DetectLinkCandidatesAsync(content, 0.85f);

        // Assert: 1文字の短いタグ名は候補にならない
        Assert.DoesNotContain(result.AutoReplaceCandidates.Concat(result.ManualCandidates), c => c.TagName == "A");
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_SkipsVoteAndReactionTags()
    {
        // Arrange: 投票・リアクションタグは候補から除外
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"lnk_{tid}";
        await CreateTagAsync("good", userId); // リアクションタグ
        var content = "This is really good";

        var service = CreateService();

        // Act
        var result = await service.DetectLinkCandidatesAsync(content, 0.50f);

        // Assert: "good" は除外される
        Assert.DoesNotContain(result.AutoReplaceCandidates, c => c.TagName == "good");
        Assert.DoesNotContain(result.ManualCandidates, c => c.TagName == "good");
    }

    [Fact]
    public void ApplyReplacements_ReplacesTextAtCorrectPositions()
    {
        // Arrange
        var service = CreateService();
        var content = "今日はプログラミングを学んだ";

        var candidates = new[]
        {
            new SRNSMudApp.Models.LinkConversionCandidate(
                OriginalText: "プログラミング",
                TagId: 42,
                TagName: "プログラミング",
                Similarity: 1.0f,
                StartIndex: 3, // "今日は" の後
                Length: 7,
                IsAutoReplace: true)
        };

        // Act
        var result = service.ApplyReplacements(content, candidates);

        // Assert
        Assert.Equal("今日は/TagDetail/42を学んだ", result);
    }

    [Fact]
    public void ApplyReplacements_MultipleReplacements_HandlesPositionsCorrectly()
    {
        // Arrange
        var service = CreateService();
        var content = "AAAとBBBの話";

        var candidates = new[]
        {
            new SRNSMudApp.Models.LinkConversionCandidate("AAA", 1, "AAA", 1.0f, 0, 3, true),
            new SRNSMudApp.Models.LinkConversionCandidate("BBB", 2, "BBB", 1.0f, 4, 3, true)
        };

        // Act
        var result = service.ApplyReplacements(content, candidates);

        // Assert: 後ろから置換するので位置ずれしない
        Assert.Equal("/TagDetail/1と/TagDetail/2の話", result);
    }

    [Fact]
    public void ApplyReplacements_EmptyCandidates_ReturnsOriginal()
    {
        var service = CreateService();
        var content = "変更なし";

        var result = service.ApplyReplacements(content, []);

        Assert.Equal("変更なし", result);
    }

    [Fact]
    public async Task DetectLinkCandidatesAsync_WithMockedTagSearchQueryService_CallsProviderAndDetectsCandidate()
    {
        // Arrange
        var mockQueryService = new Mock<ITagSearchQueryService>();
        mockQueryService
            .Setup(s => s.GetCandidateTagsForLinkConversionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Tag { Id = 99, Name = "アーキテクチャ", OwnerId = "test-user" }
            ]);

        var service = CreateService(mockQueryService.Object);

        // Act
        var result = await service.DetectLinkCandidatesAsync("このアーキテクチャは素晴らしい");

        // Assert
        mockQueryService.Verify(s => s.GetCandidateTagsForLinkConversionAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(result.AutoReplaceCandidates);
        Assert.Equal("アーキテクチャ", result.AutoReplaceCandidates[0].TagName);
        Assert.Equal(99, result.AutoReplaceCandidates[0].TagId);
    }

    [Fact]
    public async Task TagSearchQueryService_GetCandidateTagsForLinkConversionAsync_CachesResultsInMemory()
    {
        // Arrange
        var userId = $"cache-user-{Guid.NewGuid():N}";
        await CreateTagAsync("キャッシュ対象タグ", userId);

        var dbFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbFactoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_sharedDb.Options));

        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var queryService = new TagSearchQueryService(
            dbFactoryMock.Object,
            new Mock<ITagEmbeddingService>().Object,
            memoryCache);

        // Act 1: 1回目の呼び出し（DBから取得しキャッシュ）
        var tags1 = await queryService.GetCandidateTagsForLinkConversionAsync();

        // Act 2: 2回目の呼び出し（キャッシュから返却）
        var tags2 = await queryService.GetCandidateTagsForLinkConversionAsync();

        // Assert
        Assert.NotEmpty(tags1);
        Assert.Same(tags1, tags2); // キャッシュされた同一インスタンスが返る
        dbFactoryMock.Verify(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}