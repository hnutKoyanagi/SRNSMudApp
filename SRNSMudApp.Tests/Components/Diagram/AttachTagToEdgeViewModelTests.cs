#region

using Moq;

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Diagram;

public class AttachTagToEdgeViewModelTests
{
    private readonly Mock<ITagDiagramDataProvider> _diagramDataProviderMock = new();
    private readonly AttachTagToEdgeViewModel _sut;

    public AttachTagToEdgeViewModelTests()
    {
        _sut = new AttachTagToEdgeViewModel(_diagramDataProviderMock.Object);
    }

    [Fact]
    public void Constructor_NullDataProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AttachTagToEdgeViewModel(null!));
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        Assert.Null(_sut.Edge);
        Assert.Empty(_sut.CurrentUserId);
        Assert.Empty(_sut.AvailableTags);
        Assert.Null(_sut.SelectedTag);
        Assert.Null(_sut.SelectedAsset);
        Assert.Empty(_sut.AvailableAssets);
        Assert.Equal(1, _sut.Weight);
        Assert.False(_sut.IsLoadingAssets);
        Assert.False(_sut.IsOwnerTag);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public void Initialize_ConfiguresProperties()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 10, TargetTagId = 20, OwnerId = "u" };
        var tags = new List<TagEntity>
        {
            new() { Id = 1, Name = "Tag1", OwnerId = "u" }
        };

        _sut.Initialize(edge, "user-1", tags);

        Assert.Same(edge, _sut.Edge);
        Assert.Equal("user-1", _sut.CurrentUserId);
        Assert.Same(tags, _sut.AvailableTags);
        Assert.Null(_sut.SelectedTag);
        Assert.Null(_sut.SelectedAsset);
        Assert.Equal(1, _sut.Weight);
    }

    [Fact]
    public void SearchTags_FiltersByName()
    {
        var tags = new List<TagEntity>
        {
            new() { Id = 1, Name = "CSharp", OwnerId = "u" },
            new() { Id = 2, Name = "Rust", OwnerId = "u" },
            new() { Id = 3, Name = "CPlusPlus", OwnerId = "u" }
        };
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "u", tags);

        var result = _sut.SearchTags("c").ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "CSharp");
        Assert.Contains(result, t => t.Name == "CPlusPlus");
    }

    [Fact]
    public void SearchTags_WhenEmpty_ReturnsAllAvailableTagsUpTo20()
    {
        var tags = Enumerable.Range(1, 25).Select(i => new TagEntity { Id = i, Name = $"Tag{i}", OwnerId = "u" }).ToList();
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "u", tags);

        var result = _sut.SearchTags("").ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public async Task SelectTagAsync_NullTag_ClearsSelectedAssetAndAssets()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "user-1", []);

        await _sut.SelectTagAsync(null);

        Assert.Null(_sut.SelectedTag);
        Assert.Null(_sut.SelectedAsset);
        Assert.Empty(_sut.AvailableAssets);
    }

    [Fact]
    public async Task SelectTagAsync_LoadsAssetsAndSelectsFirst()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "user-1", []);
        var tag = new TagEntity { Id = 5, Name = "SelectedTag", OwnerId = "other" };
        var asset1 = new RightAsset { Id = 10, OwnerId = "user-1", Amount = 3, TargetTagId = 5 };
        var asset2 = new RightAsset { Id = 11, OwnerId = "user-1", Amount = 1, TargetTagId = 5 };

        _diagramDataProviderMock.Setup(p => p.GetAvailableRightAssetsAsync("user-1", 5))
            .ReturnsAsync([asset1, asset2]);

        await _sut.SelectTagAsync(tag);

        Assert.Same(tag, _sut.SelectedTag);
        Assert.Equal(2, _sut.AvailableAssets.Count);
        Assert.Same(asset1, _sut.SelectedAsset);
        Assert.Equal(1, _sut.Weight);
        Assert.False(_sut.IsLoadingAssets);
    }

    [Fact]
    public void IsOwnerTag_ReturnsTrue_WhenUserIdMatchesTagOwner()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "user-1", []);
        _sut.SelectedTag = new TagEntity { Id = 1, Name = "T", OwnerId = "user-1" };

        Assert.True(_sut.IsOwnerTag);
    }

    [Fact]
    public void IsOwnerTag_ReturnsFalse_WhenUserIdDiffers()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = "u" };
        _sut.Initialize(edge, "user-1", []);
        _sut.SelectedTag = new TagEntity { Id = 1, Name = "T", OwnerId = "other-user" };

        Assert.False(_sut.IsOwnerTag);
    }

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(3, 3, true)]
    [InlineData(0, 3, false)] // Weight < 1
    [InlineData(4, 3, false)] // Weight > Amount
    public void CanSubmit_ValidatesWeightRange(int weight, int assetAmount, bool expected)
    {
        _sut.SelectedTag = new TagEntity { Id = 1, Name = "T", OwnerId = "u" };
        _sut.SelectedAsset = new RightAsset { Id = 2, Amount = assetAmount, OwnerId = "u", TargetTagId = 1 };
        _sut.Weight = weight;

        Assert.Equal(expected, _sut.CanSubmit);
    }

    [Fact]
    public void Submit_WhenSelectedTagNull_ReturnsFailure()
    {
        _sut.SelectedAsset = new RightAsset { Id = 1, Amount = 5, OwnerId = "u", TargetTagId = 1 };
        _sut.Weight = 1;

        var result = _sut.Submit();

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("タグを選択してください", failure.ErrorMessage);
        }
    }

    [Fact]
    public void Submit_WhenSelectedAssetNull_ReturnsFailure()
    {
        _sut.SelectedTag = new TagEntity { Id = 1, Name = "T", OwnerId = "u" };
        _sut.Weight = 1;

        var result = _sut.Submit();

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("消費する RightAsset を選択してください", failure.ErrorMessage);
        }
    }

    [Fact]
    public void Submit_WhenValid_ReturnsSuccessWithValues()
    {
        _sut.SelectedTag = new TagEntity { Id = 15, Name = "T", OwnerId = "u" };
        _sut.SelectedAsset = new RightAsset { Id = 77, Amount = 10, OwnerId = "u", TargetTagId = 15 };
        _sut.Weight = 4;

        var result = _sut.Submit();

        Assert.True(result is Success<(int TagId, int RightAssetId, int Weight)>);
        if (result is Success<(int TagId, int RightAssetId, int Weight)> success)
        {
            Assert.Equal(15, success.Value.TagId);
            Assert.Equal(77, success.Value.RightAssetId);
            Assert.Equal(4, success.Value.Weight);
        }
    }
}