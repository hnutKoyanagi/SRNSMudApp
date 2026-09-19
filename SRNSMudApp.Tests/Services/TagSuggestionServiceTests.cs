using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     TagSuggestionService の単体テスト。
///     ベクトル類似度計算、閾値フィルタリング、除外対象タグの動作を検証する。
/// </summary>
public class TagSuggestionServiceTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;
    private readonly Mock<ITagEmbeddingService> _mockEmbeddingService = new();

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SuggestTagsAsync_WhenContentIsEmpty_ReturnsEmptyList()
    {
        // Arrange
        var dbFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var service = new TagSuggestionService(dbFactoryMock.Object, _mockEmbeddingService.Object);

        // Act
        var result = await service.SuggestTagsAsync(string.Empty);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SuggestTagsAsync_CalculatesScoresAndFiltersByThreshold()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"sug_{tid}";

        await using var db = new ApplicationDbContext(_sharedDb.Options);
        await db.SeedUsersAsync(userId);

        // 3次元のテスト用ベクトル
        float[] queryVector = [1.0f, 0.0f, 0.0f];
        float[] highSimVector = [0.9f, 0.1f, 0.0f]; // 類似度高 (> 0.9)
        float[] midSimVector = [0.5f, 0.5f, 0.0f];  // 類似度中 (~0.7)
        float[] lowSimVector = [0.0f, 1.0f, 0.0f];  // 類似度低 (0.0)

        var highTag = new Tag
        {
            Name = $"HighTag_{tid}",
            OwnerId = userId,
            Embedding = highSimVector
        };
        var midTag = new Tag
        {
            Name = $"MidTag_{tid}",
            OwnerId = userId,
            Embedding = midSimVector
        };
        var lowTag = new Tag
        {
            Name = $"LowTag_{tid}",
            OwnerId = userId,
            Embedding = lowSimVector
        };
        var reactionTag = new Tag
        {
            Name = "good", // 投票タグ
            OwnerId = userId,
            Embedding = highSimVector
        };

        db.Tags.AddRange(highTag, midTag, lowTag, reactionTag);
        await db.SaveChangesAsync();

        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync("テストコンテンツ"))
            .ReturnsAsync(new ReadOnlyMemory<float>(queryVector));

        var dbFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_sharedDb.Options));

        var service = new TagSuggestionService(dbFactoryMock.Object, _mockEmbeddingService.Object);

        // Act (閾値 0.40 で取得)
        var results = await service.SuggestTagsAsync("テストコンテンツ", 0.40f);

        // Assert
        // highTag と midTag が含まれ、lowTag (0.0) と reactionTag (good) は除外されること
        Assert.Contains(results, r => r.TagId == highTag.Id);
        Assert.Contains(results, r => r.TagId == midTag.Id);
        Assert.DoesNotContain(results, r => r.TagId == lowTag.Id);
        Assert.DoesNotContain(results, r => r.TagName == "good");

        // スコア降順にソートされていること
        var highResult = results.First(r => r.TagId == highTag.Id);
        var midResult = results.First(r => r.TagId == midTag.Id);
        Assert.True(highResult.Score > midResult.Score);
    }
}