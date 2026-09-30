using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     <see cref="TagResolutionViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的に候補検索・初期化・バリデーション・決定モデル生成を検証する。
/// </summary>
public sealed class TagResolutionViewModelTests
{
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();
    private readonly TagResolutionViewModel _sut;

    public TagResolutionViewModelTests()
    {
        _sut = new TagResolutionViewModel(_dataProviderMock.Object);
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Empty(_sut.TagName);
        Assert.Equal("UserCustomTag", _sut.TagKind);
        Assert.Null(_sut.SuggestedParentTag);
        Assert.Equal(TagResolutionAction.UseExisting, _sut.Action);
        Assert.True(_sut.HasCandidate);
        Assert.True(_sut.UseSuggestedParent);
        Assert.Null(_sut.SelectedExistingTag);
        Assert.Null(_sut.SelectedCustomParentTag);
        Assert.Empty(_sut.InitialSuggestions);
        Assert.Empty(_sut.ParentSuggestions);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public async Task InitializeAsync_WhenSuggestionsFound_PopulatesCandidatesAndDefaults()
    {
        // Arrange
        const string tagName = "AI";
        var existingTags = new List<TagEntity>
        {
            new() { Id = 1, Name = "AI", OwnerId = "user-1" },
            new() { Id = 2, Name = "AI Tool", OwnerId = "user-1" }
        };
        var parentTags = new List<TagEntity>
        {
            new() { Id = 10, Name = "Tech", OwnerId = "user-1" }
        };
        var defaultParents = new List<TagEntity>
        {
            new() { Id = 10, Name = "Tech", OwnerId = "user-1" },
            new() { Id = 20, Name = "Science", OwnerId = "user-1" }
        };

        _dataProviderMock.Setup(d => d.SearchTagsAsync(tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTags);
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTags);
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultParents);

        var suggestedParent = new TagEntity { Id = 99, Name = "SuggestedParent", OwnerId = "user-1" };

        // Act
        await _sut.InitializeAsync(tagName, suggestedParent);

        // Assert
        Assert.Equal(tagName, _sut.TagName);
        Assert.True(_sut.HasCandidate);
        Assert.Equal(2, _sut.InitialSuggestions.Count);
        Assert.NotNull(_sut.SelectedExistingTag);
        Assert.Equal(1, _sut.SelectedExistingTag.Id);

        // Parent candidates should contain 10 (Tech) and 20 (Science) without duplicates
        Assert.Equal(2, _sut.ParentSuggestions.Count);
        Assert.NotNull(_sut.SelectedCustomParentTag);
        Assert.Equal(10, _sut.SelectedCustomParentTag.Id);

        Assert.True(_sut.UseSuggestedParent);
        Assert.Same(suggestedParent, _sut.SuggestedParentTag);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoSuggestionsFound_SetsHasCandidateFalse()
    {
        // Arrange
        const string tagName = "UniqueTag";
        _dataProviderMock.Setup(d => d.SearchTagsAsync(tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.InitializeAsync(tagName, null);

        // Assert
        Assert.False(_sut.HasCandidate);
        Assert.Empty(_sut.InitialSuggestions);
        Assert.Null(_sut.SelectedExistingTag);
        Assert.False(_sut.UseSuggestedParent);
    }

    [Fact]
    public void SetUseSuggestedParent_WhenSwitchedToFalse_SetsDefaultCustomParent()
    {
        // Arrange
        _sut.UseSuggestedParent = true;
        _sut.SelectedCustomParentTag = null;

        var parent1 = new TagEntity { Id = 15, Name = "Parent1", OwnerId = "user-1" };
        var parentList = new List<TagEntity> { parent1 };
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentList);

        // Populate parent suggestions via InitializeAsync
        _dataProviderMock.Setup(d => d.SearchTagsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync("Test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentList);

        // Act
        _sut.SetUseSuggestedParent(false);

        // Assert
        Assert.False(_sut.UseSuggestedParent);
    }

    [Theory]
    [InlineData(TagResolutionAction.Skip, false, false, false, true)]
    [InlineData(TagResolutionAction.UseExisting, true, false, false, true)]
    [InlineData(TagResolutionAction.UseExisting, false, false, false, false)]
    [InlineData(TagResolutionAction.CreateNew, false, true, true, true)]
    [InlineData(TagResolutionAction.CreateNew, false, true, false, false)]
    [InlineData(TagResolutionAction.CreateNew, false, false, false, false)]
    public void CanSubmit_ValidatesCorrectly(
        TagResolutionAction action,
        bool hasSelectedExisting,
        bool useSuggestedParent,
        bool hasSuggestedOrCustomParent,
        bool expected)
    {
        // Arrange
        _sut.Action = action;
        _sut.SelectedExistingTag = hasSelectedExisting ? new TagEntity { Id = 1, Name = "Existing", OwnerId = "u1" } : null;
        _sut.UseSuggestedParent = useSuggestedParent;

        if (useSuggestedParent)
        {
            _sut.SuggestedParentTag = hasSuggestedOrCustomParent ? new TagEntity { Id = 10, Name = "P", OwnerId = "u1" } : null;
        }
        else
        {
            _sut.SelectedCustomParentTag = hasSuggestedOrCustomParent ? new TagEntity { Id = 20, Name = "CP", OwnerId = "u1" } : null;
        }

        // Assert
        Assert.Equal(expected, _sut.CanSubmit);
    }

    [Fact]
    public async Task SearchExistingTagsAsync_WhenQueryEmptyAndInitialSuggestionsExist_ReturnsInitialWithoutCallingService()
    {
        // Arrange
        var initial = new List<TagEntity> { new() { Id = 1, Name = "TagA", OwnerId = "u1" } };
        _dataProviderMock.Setup(d => d.SearchTagsAsync("Test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);

        await _sut.InitializeAsync("Test", null);

        // Act
        var result = await _sut.SearchExistingTagsAsync("");

        // Assert
        Assert.Single(result);
        // Only 1 call from InitializeAsync
        _dataProviderMock.Verify(d => d.SearchTagsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchExistingTagsAsync_WhenQueryProvided_CallsDataProvider()
    {
        // Arrange
        var searchResult = new List<TagEntity> { new() { Id = 99, Name = "SearchResult", OwnerId = "u1" } };
        _dataProviderMock.Setup(d => d.SearchTagsAsync("query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        // Act
        var result = await _sut.SearchExistingTagsAsync("query");

        // Assert
        Assert.Single(result);
        Assert.Equal(99, result.First().Id);
    }

    [Fact]
    public async Task SearchParentCandidateTagsAsync_WhenQueryEmpty_ReturnsCachedParentSuggestions()
    {
        // Arrange
        var parents = new List<TagEntity> { new() { Id = 5, Name = "ParentA", OwnerId = "u1" } };
        _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync("Test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parents);

        await _sut.InitializeAsync("Test", null);

        // Act
        var result = await _sut.SearchParentCandidateTagsAsync("");

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void CreateDecision_WhenUseExisting_ReturnsDecisionWithExistingTagId()
    {
        // Arrange
        _sut.TagName = "MyTag";
        _sut.Action = TagResolutionAction.UseExisting;
        _sut.SelectedExistingTag = new TagEntity { Id = 42, Name = "Existing", OwnerId = "u1" };

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagResolutionAction.UseExisting, decision.Action);
        Assert.Equal(42, decision.SelectedExistingTagId);
        Assert.Null(decision.SelectedParentTagId);
        Assert.Equal("MyTag", decision.NewTagName);
    }

    [Fact]
    public void CreateDecision_WhenCreateNewWithSuggestedParent_ReturnsDecisionWithParentId()
    {
        // Arrange
        _sut.TagName = "NewTag";
        _sut.Action = TagResolutionAction.CreateNew;
        _sut.UseSuggestedParent = true;
        _sut.SuggestedParentTag = new TagEntity { Id = 100, Name = "Suggested", OwnerId = "u1" };

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagResolutionAction.CreateNew, decision.Action);
        Assert.Null(decision.SelectedExistingTagId);
        Assert.Equal(100, decision.SelectedParentTagId);
        Assert.Equal("NewTag", decision.NewTagName);
    }

    [Fact]
    public void CreateDecision_WhenCreateNewWithCustomParent_ReturnsDecisionWithCustomParentId()
    {
        // Arrange
        _sut.TagName = "NewTag";
        _sut.Action = TagResolutionAction.CreateNew;
        _sut.UseSuggestedParent = false;
        _sut.SelectedCustomParentTag = new TagEntity { Id = 200, Name = "Custom", OwnerId = "u1" };

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagResolutionAction.CreateNew, decision.Action);
        Assert.Null(decision.SelectedExistingTagId);
        Assert.Equal(200, decision.SelectedParentTagId);
        Assert.Equal("NewTag", decision.NewTagName);
    }

    [Fact]
    public void CreateSkipDecision_ReturnsDecisionWithSkipAction()
    {
        // Arrange
        _sut.TagName = "SkippedTag";

        // Act
        var decision = _sut.CreateSkipDecision();

        // Assert
        Assert.Equal(TagResolutionAction.Skip, decision.Action);
        Assert.Null(decision.SelectedExistingTagId);
        Assert.Null(decision.SelectedParentTagId);
        Assert.Equal("SkippedTag", decision.NewTagName);
    }
}