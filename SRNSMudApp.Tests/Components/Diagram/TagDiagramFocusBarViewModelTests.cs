// Components/Diagram/TagDiagramFocusBarViewModelTests.cs
#region

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Diagram;

/// <summary>
///     <see cref="TagDiagramFocusBarViewModel" /> の単体テスト。
/// </summary>
public class TagDiagramFocusBarViewModelTests
{
    [Fact]
    public void GetExistingEdgeBetween_WhenForwardEdgeExists_ReturnsEdge()
    {
        var expectedEdge = new TagEdge { OwnerId = "test-owner", SourceTagId = 1, TargetTagId = 2 };
        var edges = new List<TagEdge>
        {
            new() { OwnerId = "test-owner", SourceTagId = 3, TargetTagId = 4 },
            expectedEdge
        };

        var actual = TagDiagramFocusBarViewModel.GetExistingEdgeBetween(edges, 1, 2);

        Assert.NotNull(actual);
        Assert.Same(expectedEdge, actual);
    }

    [Fact]
    public void GetExistingEdgeBetween_WhenReverseEdgeExists_ReturnsEdge()
    {
        var expectedEdge = new TagEdge { OwnerId = "test-owner", SourceTagId = 2, TargetTagId = 1 };
        var edges = new List<TagEdge>
        {
            new() { OwnerId = "test-owner", SourceTagId = 3, TargetTagId = 4 },
            expectedEdge
        };

        var actual = TagDiagramFocusBarViewModel.GetExistingEdgeBetween(edges, 1, 2);

        Assert.NotNull(actual);
        Assert.Same(expectedEdge, actual);
    }

    [Fact]
    public void GetExistingEdgeBetween_WhenNoMatchingEdge_ReturnsNull()
    {
        var edges = new List<TagEdge>
        {
            new() { OwnerId = "test-owner", SourceTagId = 1, TargetTagId = 3 },
            new() { OwnerId = "test-owner", SourceTagId = 4, TargetTagId = 2 }
        };

        var actual = TagDiagramFocusBarViewModel.GetExistingEdgeBetween(edges, 1, 2);

        Assert.Null(actual);
    }

    [Fact]
    public void GetExistingEdgeBetween_WhenEdgesNull_ReturnsNull()
    {
        var actual = TagDiagramFocusBarViewModel.GetExistingEdgeBetween(null, 1, 2);
        Assert.Null(actual);
    }

    [Fact]
    public void GetChildTagsCount_CountsMatchingChildTags()
    {
        var tags = new List<TagEntity>
        {
            new() { Id = 1, OwnerId = "test-owner", Name = "Tag 1", ParentTagId = null },
            new() { Id = 2, OwnerId = "test-owner", Name = "Tag 2", ParentTagId = 1 },
            new() { Id = 3, OwnerId = "test-owner", Name = "Tag 3", ParentTagId = 1 },
            new() { Id = 4, OwnerId = "test-owner", Name = "Tag 4", ParentTagId = 2 },
            new() { Id = 5, OwnerId = "test-owner", Name = "Tag 5", ParentTagId = 1 }
        };

        var count = TagDiagramFocusBarViewModel.GetChildTagsCount(tags, 1);

        Assert.Equal(3, count);
    }

    [Fact]
    public void GetChildTagsCount_WhenNoChildren_ReturnsZero()
    {
        var tags = new List<TagEntity>
        {
            new() { Id = 1, OwnerId = "test-owner", Name = "Tag 1", ParentTagId = null },
            new() { Id = 2, OwnerId = "test-owner", Name = "Tag 2", ParentTagId = 1 }
        };

        var count = TagDiagramFocusBarViewModel.GetChildTagsCount(tags, 99);

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetChildTagsCount_WhenAllTagsNull_ReturnsZero()
    {
        var count = TagDiagramFocusBarViewModel.GetChildTagsCount(null, 1);
        Assert.Equal(0, count);
    }
}