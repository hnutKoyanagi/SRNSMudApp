#region

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class TagListViewModelTests
{
    private readonly Mock<ITagSearchQueryService> _tagSearchQueryServiceMock = new();

    private TagListViewModel CreateSut()
    {
        return new TagListViewModel(_tagSearchQueryServiceMock.Object);
    }

    [Fact]
    public void Constructor_WhenQueryServiceIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TagListViewModel(null!));
    }

    [Fact]
    public void InitialState_PropertiesHaveExpectedDefaults()
    {
        var sut = CreateSut();

        Assert.Null(sut.Tags);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadDataAsync_WhenCalled_SetsTagsAndManagesLoadingState()
    {
        var sut = CreateSut();
        var expectedTags = new List<TagEntity>
        {
            new() { Id = 1, Name = "TagA", CachedWeight = 10, OwnerId = "user1" },
            new() { Id = 2, Name = "TagB", CachedWeight = 20, OwnerId = "user2" }
        };

        _tagSearchQueryServiceMock
            .Setup(s => s.GetTagsWithDetailsAsync())
            .ReturnsAsync(expectedTags);

        await sut.LoadDataAsync();

        Assert.NotNull(sut.Tags);
        Assert.Equal(2, sut.Tags.Count);
        Assert.Same(expectedTags, sut.Tags);
        Assert.False(sut.IsLoading);
        _tagSearchQueryServiceMock.Verify(s => s.GetTagsWithDetailsAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadDataAsync_WhenExceptionOccurs_ResetsIsLoadingAndRethrows()
    {
        var sut = CreateSut();

        _tagSearchQueryServiceMock
            .Setup(s => s.GetTagsWithDetailsAsync())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadDataAsync());

        Assert.False(sut.IsLoading);
        Assert.Null(sut.Tags);
    }
}