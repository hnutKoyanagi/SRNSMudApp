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
    public void SetUser_SetsPropertiesCorrectly()
    {
        var sut = CreateSut();

        sut.SetUser("user-1", true);

        Assert.Equal("user-1", sut.CurrentUserId);
        Assert.True(sut.IsAdmin);
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
            .Setup(s => s.GetTagsWithDetailsAsync(null, false))
            .ReturnsAsync(expectedTags);

        await sut.LoadDataAsync();

        Assert.NotNull(sut.Tags);
        Assert.Equal(2, sut.Tags.Count);
        Assert.Same(expectedTags, sut.Tags);
        Assert.False(sut.IsLoading);
        _tagSearchQueryServiceMock.Verify(s => s.GetTagsWithDetailsAsync(null, false), Times.Once);
    }

    [Fact]
    public async Task LoadDataAsync_WhenUserIsSet_PassesCurrentUserIdAndIsAdminToQueryService()
    {
        var sut = CreateSut();
        var privateTag = new TagEntity
        {
            Id = 10,
            Name = "SecretProject",
            IsPrivate = true,
            OwnerId = "user-1",
            Content = "Confidential details"
        };
        var publicTag = new TagEntity
        {
            Id = 11,
            Name = "PublicTopic",
            IsPrivate = false,
            OwnerId = "user-2"
        };
        var expectedTags = new List<TagEntity> { privateTag, publicTag };

        _tagSearchQueryServiceMock
            .Setup(s => s.GetTagsWithDetailsAsync("user-1", false))
            .ReturnsAsync(expectedTags);

        sut.SetUser("user-1", false);
        await sut.LoadDataAsync();

        Assert.NotNull(sut.Tags);
        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Name == "SecretProject" && t.IsPrivate);
        _tagSearchQueryServiceMock.Verify(s => s.GetTagsWithDetailsAsync("user-1", false), Times.Once);

        // TagTable の検索フィルターでプライベートタグがヒットすることを検証
        Assert.True(TagTableViewModel.FilterFunc(privateTag, "Secret"));
        Assert.True(TagTableViewModel.FilterFunc(privateTag, "Confidential"));

        // サジェスト候補にもプライベートタグ名が含まれることを検証
        var suggestions = TagTableViewModel.GetTagSearchSuggestions(sut.Tags, "Secret");
        Assert.Contains("SecretProject", suggestions);
    }

    [Fact]
    public async Task LoadDataAsync_WithParameters_SetsUserAndPassesToQueryService()
    {
        var sut = CreateSut();
        var expectedTags = new List<TagEntity>
        {
            new() { Id = 1, Name = "AdminTag", IsPrivate = true, OwnerId = "user-9" }
        };

        _tagSearchQueryServiceMock
            .Setup(s => s.GetTagsWithDetailsAsync("admin-1", true))
            .ReturnsAsync(expectedTags);

        await sut.LoadDataAsync("admin-1", true);

        Assert.Equal("admin-1", sut.CurrentUserId);
        Assert.True(sut.IsAdmin);
        Assert.NotNull(sut.Tags);
        Assert.Single(sut.Tags);
        _tagSearchQueryServiceMock.Verify(s => s.GetTagsWithDetailsAsync("admin-1", true), Times.Once);
    }

    [Fact]
    public async Task LoadDataAsync_WhenExceptionOccurs_ResetsIsLoadingAndRethrows()
    {
        var sut = CreateSut();

        _tagSearchQueryServiceMock
            .Setup(s => s.GetTagsWithDetailsAsync(It.IsAny<string?>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadDataAsync());

        Assert.False(sut.IsLoading);
        Assert.Null(sut.Tags);
    }
}