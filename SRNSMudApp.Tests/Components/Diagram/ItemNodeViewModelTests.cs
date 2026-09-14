using Moq;

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

using ItemEntity = SRNSMudApp.Data.Item;
using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Diagram;

/// <summary>
///     ItemNodeViewModel の純粋ロジック（文字列整形、切り詰め、ファクトリ生成、プレビュー解決）を検証する単体テスト。
///     bUnit を起動せず高速に実行可能。
/// </summary>
public class ItemNodeViewModelTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("<b>Hello</b>", "Hello")]
    [InlineData("<div><span>Hello</span> <i>World</i></div>", "Hello World")]
    public void StripHtmlTags_RemovesTagsAndTrims(string? input, string expected)
    {
        var result = ItemNodeViewModel.StripHtmlTags(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTruncatedSegments_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(ItemNodeViewModel.GetTruncatedSegments(null));
        Assert.Empty(ItemNodeViewModel.GetTruncatedSegments(""));
        Assert.Empty(ItemNodeViewModel.GetTruncatedSegments("   "));
    }

    [Fact]
    public void GetTruncatedSegments_ShortText_ReturnsSingleSegmentWithoutEllipsis()
    {
        var text = "Short text under 80 characters.";
        var segments = ItemNodeViewModel.GetTruncatedSegments(text);

        Assert.Single(segments);
        Assert.False(segments[0].IsUrl);
        Assert.Equal(text, segments[0].Text);
    }

    [Fact]
    public void GetTruncatedSegments_LongText_TruncatesAtMaxLengthWithEllipsis()
    {
        var longText = new string('A', 100);
        var segments = ItemNodeViewModel.GetTruncatedSegments(longText, maxLength: 80);

        Assert.Single(segments);
        Assert.False(segments[0].IsUrl);
        Assert.Equal(new string('A', 80) + "...", segments[0].Text);
    }

    [Fact]
    public void GetTruncatedSegments_WithInternalUrl_PreservesUrlSegment()
    {
        var text = "Link to item /ItemDetail/42 and more details.";
        var segments = ItemNodeViewModel.GetTruncatedSegments(text, maxLength: 80);

        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsUrl);
        Assert.Equal("Link to item ", segments[0].Text);

        Assert.True(segments[1].IsUrl);
        Assert.Equal("/ItemDetail/42", segments[1].Text);

        Assert.False(segments[2].IsUrl);
        Assert.Equal(" and more details.", segments[2].Text);
    }

    [Fact]
    public void GetTruncatedSegments_WhenUrlExceedsMaxLength_AppendsEllipsisAndStops()
    {
        var text = "Prefix text " + new string('X', 65) + " /ItemDetail/999 and suffix text that should be cut off.";
        var segments = ItemNodeViewModel.GetTruncatedSegments(text, maxLength: 80);

        // URL 自体は保持され、後続があるため末尾に "..." が付与されること
        Assert.Contains(segments, s => s.IsUrl && s.Text == "/ItemDetail/999");
        Assert.Equal("...", segments.Last().Text);
        Assert.DoesNotContain("and suffix text", string.Join("", segments.Select(s => s.Text)));
    }

    [Fact]
    public void CreateLinkPreviewData_ValidItem_GeneratesExpectedPreviewData()
    {
        // Arrange
        var user = new ApplicationUser { UserName = "testuser" };
        var tag = new TagEntity { Id = 1, Name = "C#", Owner = user, OwnerId = "testuser", IsSystem = false };
        var systemTag = new TagEntity { Id = 2, Name = "good", OwnerId = "testuser", IsSystem = true };
        var item = new ItemEntity
        {
            Id = 55,
            OwnerId = "testuser",
            Content = "<p>Sample item content with tags.</p>",
            TagRelations =
            [
                new TagRelation { Tag = tag, TagId = 1, OwnerId = "testuser", Weight = 5 },
                new TagRelation { Tag = systemTag, TagId = 2, OwnerId = "testuser", Weight = 1 }
            ]
        };

        // Act
        var preview = ItemNodeViewModel.CreateLinkPreviewData(item, "/ItemDetail/55");

        // Assert
        Assert.NotNull(preview);
        Assert.True(preview.IsSuccess);
        Assert.Equal("/ItemDetail/55", preview.Url);
        Assert.Equal("Item #55", preview.Title);
        Assert.Equal("Sample item content with tags.", preview.Description);
        Assert.Single(preview.Tags); // システムタグは除外されること
        Assert.Equal("C#", preview.Tags[0].Name);
        Assert.Equal("testuser", preview.Tags[0].OwnerName);
        Assert.Equal(5, preview.Tags[0].Weight);
    }

    [Fact]
    public void CreateLinkPreviewData_LongContent_TruncatesTo200CharactersWithEllipsis()
    {
        // Arrange
        var longContent = new string('Z', 250);
        var item = new ItemEntity { Id = 10, OwnerId = "testuser", Content = longContent };

        // Act
        var preview = ItemNodeViewModel.CreateLinkPreviewData(item, "/ItemDetail/10");

        // Assert
        Assert.Equal(new string('Z', 200) + "...", preview.Description);
    }

    [Fact]
    public async Task ResolvePreviewAsync_WithCustomLoader_UsesCustomLoaderPriority()
    {
        // Arrange
        var customPreview = new LinkPreviewData { Url = "/ItemDetail/1", Title = "Custom", IsSuccess = true };
        var mockService = new Mock<ILinkPreviewService>();

        // Act
        var result = await ItemNodeViewModel.ResolvePreviewAsync(
            "/ItemDetail/1",
            contextItems: [],
            previewService: mockService.Object,
            customLoader: _ => Task.FromResult<LinkPreviewData?>(customPreview));

        // Assert
        Assert.Same(customPreview, result);
        mockService.Verify(s => s.GetPreviewAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolvePreviewAsync_ItemInContextItems_ResolvesInstantlyWithoutServiceCall()
    {
        // Arrange
        var item = new ItemEntity { Id = 42, OwnerId = "testuser", Content = "Context item content" };
        var mockService = new Mock<ILinkPreviewService>();

        // Act
        var result = await ItemNodeViewModel.ResolvePreviewAsync(
            "/ItemDetail/42",
            contextItems: [item],
            previewService: mockService.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Item #42", result.Title);
        Assert.Equal("Context item content", result.Description);
        mockService.Verify(s => s.GetPreviewAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolvePreviewAsync_ItemNotInContextItems_CallsPreviewService()
    {
        // Arrange
        var servicePreview = new LinkPreviewData { Url = "/TagDetail/10", Title = "Tag:Rust", IsSuccess = true };
        var mockService = new Mock<ILinkPreviewService>();
        mockService.Setup(s => s.GetPreviewAsync("/TagDetail/10")).ReturnsAsync(servicePreview);

        // Act
        var result = await ItemNodeViewModel.ResolvePreviewAsync(
            "/TagDetail/10",
            contextItems: [],
            previewService: mockService.Object);

        // Assert
        Assert.Same(servicePreview, result);
        mockService.Verify(s => s.GetPreviewAsync("/TagDetail/10"), Times.Once);
    }

    [Fact]
    public async Task ResolvePreviewAsync_NullService_ReturnsNullSafely()
    {
        var result = await ItemNodeViewModel.ResolvePreviewAsync(
            "/TagDetail/99",
            contextItems: [],
            previewService: null);

        Assert.Null(result);
    }

    [Fact]
    public void CreateLinkPreviewData_NullContextItem_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("contextItem", () =>
            ItemNodeViewModel.CreateLinkPreviewData(null!, "/ItemDetail/1"));
    }

    [Fact]
    public void CreateLinkPreviewData_NullUrl_ThrowsArgumentNullException()
    {
        var item = new ItemEntity { Id = 1, OwnerId = "testuser", Content = "Test" };
        Assert.Throws<ArgumentNullException>("url", () =>
            ItemNodeViewModel.CreateLinkPreviewData(item, null!));
    }

    [Fact]
    public async Task ResolvePreviewAsync_NullUrl_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("url", () =>
            ItemNodeViewModel.ResolvePreviewAsync(null!, []));
    }

    [Fact]
    public async Task ResolvePreviewAsync_NullContextItems_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("contextItems", () =>
            ItemNodeViewModel.ResolvePreviewAsync("/ItemDetail/1", null!));
    }
}