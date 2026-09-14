using System.Net;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     ItemListExportService の単体テスト。
///     タグ展開 (親タグ・関連タグ) とリンクプレビュー組み立てを bUnit なしで検証する。
/// </summary>
public class ItemListExportServiceTests
{
    [Fact]
    public async Task BuildExportAsync_WithUnknownTag_SkipsRelation()
    {
        var exportData = new ItemListExportData(
            AllTags: [],
            ItemTagRelations: [new TagRelation { ItemId = 1, TagId = 99, OwnerId = "u1" }],
            TagToTagRelations: []);
        var service = new ItemListExportService(CreatePreviewService());

        IReadOnlyList<ExportItemDto> result = await service.BuildExportAsync(
            exportData, [new SRNSMudApp.Data.Item { Id = 1, Content = "content", OwnerId = "u1" }]);

        ExportItemDto dto = Assert.Single(result);
        Assert.Empty(dto.Tags);
    }

    [Fact]
    public async Task BuildExportAsync_WithParentAndRelatedTags_ExpandsHierarchy()
    {
        var parent = new SRNSMudApp.Data.Tag { Id = 1, Name = "Parent", OwnerId = "u1" };
        var child = new SRNSMudApp.Data.Tag { Id = 2, Name = "Child", ParentTagId = 1, OwnerId = "u1" };
        var related = new SRNSMudApp.Data.Tag { Id = 3, Name = "Related", OwnerId = "u1" };
        var exportData = new ItemListExportData(
            AllTags: new Dictionary<int, SRNSMudApp.Data.Tag> { [1] = parent, [2] = child, [3] = related },
            ItemTagRelations: [new TagRelation { ItemId = 1, TagId = 2, OwnerId = "u1" }],
            TagToTagRelations: [new TagRelationToTag { TagId = 3, TargetTagId = 2, OwnerId = "u1" }]);
        var service = new ItemListExportService(CreatePreviewService());

        IReadOnlyList<ExportItemDto> result = await service.BuildExportAsync(
            exportData, [new SRNSMudApp.Data.Item { Id = 1, Content = "content", OwnerId = "u1" }]);

        ExportItemDto dto = Assert.Single(result);
        ExportTagDto tag = Assert.Single(dto.Tags);
        Assert.Equal("Child", tag.Name);
        Assert.Equal(["Parent"], tag.ParentTags.Select(t => t.Name));
        Assert.Equal(["Related"], tag.RelatedTags.Select(t => t.Name));
    }

    [Fact]
    public async Task BuildExportAsync_WithSuccessfulPreviews_IncludesMaxThree()
    {
        const string content = "https://a.com https://b.com https://c.com https://d.com";
        var exportData = new ItemListExportData([], [], []);
        var service = new ItemListExportService(CreatePreviewService());

        IReadOnlyList<ExportItemDto> result =
            await service.BuildExportAsync(exportData, [new SRNSMudApp.Data.Item { Id = 1, Content = content, OwnerId = "u1" }]);

        ExportItemDto dto = Assert.Single(result);
        Assert.Equal(3, dto.LinkPreviews.Count);
        Assert.All(dto.LinkPreviews, lp => Assert.False(string.IsNullOrWhiteSpace(lp.Title)));
    }

    [Fact]
    public async Task BuildExportAsync_WithFailingFetch_ExcludesPreview()
    {
        var exportData = new ItemListExportData([], [], []);
        var mockPreview = new Mock<ILinkPreviewService>();
        mockPreview.Setup(s => s.GetPreviewAsync(It.IsAny<string>()))
            .ReturnsAsync((string url) => new LinkPreviewData { Url = url, IsSuccess = false });
        var service = new ItemListExportService(mockPreview.Object);

        IReadOnlyList<ExportItemDto> result = await service.BuildExportAsync(
            exportData, [new SRNSMudApp.Data.Item { Id = 1, Content = "see https://fail.com", OwnerId = "u1" }]);

        ExportItemDto dto = Assert.Single(result);
        Assert.Empty(dto.LinkPreviews);
    }

    [Fact]
    public async Task BuildExportAsync_WithItemAndTagOwners_PopulatesOwnerNames()
    {
        var parentUser = new ApplicationUser { Id = "u1", UserName = "parent_author" };
        var childUser = new ApplicationUser { Id = "u2", UserName = "child_author" };
        var relatedUser = new ApplicationUser { Id = "u3", UserName = "related_author" };
        var itemUser = new ApplicationUser { Id = "u4", UserName = "item_author" };

        var parent = new SRNSMudApp.Data.Tag { Id = 1, Name = "Parent", OwnerId = "u1", Owner = parentUser };
        var child = new SRNSMudApp.Data.Tag { Id = 2, Name = "Child", ParentTagId = 1, OwnerId = "u2", Owner = childUser };
        var related = new SRNSMudApp.Data.Tag { Id = 3, Name = "Related", OwnerId = "u3", Owner = relatedUser };
        var exportData = new ItemListExportData(
            AllTags: new Dictionary<int, SRNSMudApp.Data.Tag> { [1] = parent, [2] = child, [3] = related },
            ItemTagRelations: [new TagRelation { ItemId = 1, TagId = 2, OwnerId = "u2" }],
            TagToTagRelations: [new TagRelationToTag { TagId = 3, TargetTagId = 2, OwnerId = "u3" }]);
        var service = new ItemListExportService(CreatePreviewService());

        var item = new SRNSMudApp.Data.Item
        {
            Id = 1,
            Content = "content",
            OwnerId = "u4",
            Owner = itemUser
        };

        IReadOnlyList<ExportItemDto> result = await service.BuildExportAsync(exportData, [item]);

        ExportItemDto dto = Assert.Single(result);
        Assert.Equal("item_author", dto.Owner.Name);

        ExportTagDto tag = Assert.Single(dto.Tags);
        Assert.Equal("child_author", tag.Owner.Name);

        ExportTagSimpleDto parentDto = Assert.Single(tag.ParentTags);
        Assert.Equal("parent_author", parentDto.Owner.Name);

        ExportTagSimpleDto relatedDto = Assert.Single(tag.RelatedTags);
        Assert.Equal("related_author", relatedDto.Owner.Name);
    }

    [Fact]
    public async Task BuildExportAsync_WhenOwnerIsNull_DefaultsToEmptyString()
    {
        var tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "TagWithoutOwner", OwnerId = "u1", Owner = null! };
        var exportData = new ItemListExportData(
            AllTags: new Dictionary<int, SRNSMudApp.Data.Tag> { [1] = tag },
            ItemTagRelations: [new TagRelation { ItemId = 1, TagId = 1, OwnerId = "u1" }],
            TagToTagRelations: []);
        var service = new ItemListExportService(CreatePreviewService());

        var item = new SRNSMudApp.Data.Item { Id = 1, Content = "content", OwnerId = "u1", Owner = null! };

        IReadOnlyList<ExportItemDto> result = await service.BuildExportAsync(exportData, [item]);

        ExportItemDto dto = Assert.Single(result);
        Assert.Equal(string.Empty, dto.Owner.Name);
        ExportTagDto tagDto = Assert.Single(dto.Tags);
        Assert.Equal(string.Empty, tagDto.Owner.Name);
    }

    [Fact]
    public void Serialize_ProducesIndentedJsonWithUnicode()
    {
        var items = new List<ExportItemDto>
        {
            new()
            {
                Content = "日本語コンテンツ",
                Owner = new ExportOwnerDto { Name = "所有者ユーザー" },
                Tags = [new ExportTagDto
                {
                    Name = "タグ",
                    Owner = new ExportOwnerDto { Name = "タグ作者" },
                    ParentTags = [new ExportTagSimpleDto { Name = "親", Owner = new ExportOwnerDto { Name = "親タグ作者" } }]
                }]
            }
        };

        var json = ItemListExportService.Serialize(items);

        Assert.Contains("\n", json);                       // インデント付き
        Assert.Contains("日本語コンテンツ", json);           // Unicode がそのまま出力される
        Assert.Contains("\"ParentTags\"", json);
        Assert.Contains("\"Owner\"", json);
        Assert.Contains("\"Name\": \"所有者ユーザー\"", json);
        Assert.Contains("\"Name\": \"タグ作者\"", json);
    }

    /// <summary>テスト用の ILinkPreviewService モックを生成する。</summary>
    private static ILinkPreviewService CreatePreviewService()
    {
        var mock = new Mock<ILinkPreviewService>();
        mock.Setup(s => s.GetPreviewAsync(It.IsAny<string>()))
            .ReturnsAsync((string url) => new LinkPreviewData
            {
                Url = url,
                Title = "Example Title",
                IsSuccess = true
            });
        return mock.Object;
    }
}