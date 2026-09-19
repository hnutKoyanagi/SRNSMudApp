using SRNSMudApp.Components.Admin;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="TagManagementViewModel"/> の単体テスト。
///     検索フィルタの動作（空文字・タグ名・親タグ名・作成者名の一致）を検証する。
/// </summary>
public class TagManagementViewModelTests
{
    private static readonly TagLockItemDto[] SampleTags =
    [
        new(1, "AlphaTag", 1, null, null, "Alice", false, false, false, false),
        new(2, "BetaTag", 2, 1, "AlphaTag", "Bob", true, false, false, true),
        new(3, "GammaTag", 2, 1, "AlphaTag", "Charlie", false, true, false, true),
        new(4, "DeltaTag", 3, 2, "BetaTag", "David", false, false, true, true)
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterTags_WhenSearchStringIsNullOrWhiteSpace_ReturnsAllTags(string? search)
    {
        // Act
        var result = TagManagementViewModel.FilterTags(SampleTags, search).ToList();

        // Assert
        Assert.Equal(SampleTags.Length, result.Count);
    }

    [Fact]
    public void FilterTags_WhenSearchMatchesTagName_ReturnsMatchingTags()
    {
        // Act
        var result = TagManagementViewModel.FilterTags(SampleTags, "Delta").ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("DeltaTag", result[0].Name);
    }

    [Fact]
    public void FilterTags_WhenSearchMatchesParentTagName_ReturnsMatchingTags()
    {
        // Act: ParentTagName が "BetaTag" のタグ
        var result = TagManagementViewModel.FilterTags(SampleTags, "BetaTag").ToList();

        // Assert: Name が BetaTag のものと ParentTagName が BetaTag のもの
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "BetaTag");
        Assert.Contains(result, t => t.Name == "DeltaTag");
    }

    [Fact]
    public void FilterTags_WhenSearchMatchesOwnerUserName_ReturnsMatchingTags()
    {
        // Act
        var result = TagManagementViewModel.FilterTags(SampleTags, "Charlie").ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("GammaTag", result[0].Name);
    }

    [Fact]
    public void FilterTags_WhenSearchDoesNotMatch_ReturnsEmpty()
    {
        // Act
        var result = TagManagementViewModel.FilterTags(SampleTags, "NonExistentKeyword").ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FilterTags_WhenTagsIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TagManagementViewModel.FilterTags(null!, "test"));
    }
}