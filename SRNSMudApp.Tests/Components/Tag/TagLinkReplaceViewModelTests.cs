using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     <see cref="TagLinkReplaceViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的に内部リンク置き換え、バリデーション、決定生成を検証する。
/// </summary>
public sealed class TagLinkReplaceViewModelTests
{
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();
    private readonly TagLinkReplaceViewModel _sut;

    public TagLinkReplaceViewModelTests()
    {
        _sut = new TagLinkReplaceViewModel(_dataProviderMock.Object);
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Empty(_sut.OriginalMatchText);
        Assert.Empty(_sut.TagName);
        Assert.Null(_sut.FirstCandidateTag);
        Assert.Equal(TagLinkReplaceAction.ReplaceWithFirstCandidate, _sut.Action);
        Assert.Null(_sut.SelectedCustomTag);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public void SetCandidate_WhenCandidateProvided_SetsCandidateAndFirstCandidateAction()
    {
        // Arrange
        var candidate = new TagEntity { Id = 10, Name = "CandidateTag", OwnerId = "user-1" };

        // Act
        _sut.SetCandidate(candidate);

        // Assert
        Assert.Same(candidate, _sut.FirstCandidateTag);
        Assert.Equal(TagLinkReplaceAction.ReplaceWithFirstCandidate, _sut.Action);
        Assert.True(_sut.CanSubmit);
    }

    [Fact]
    public void SetCandidate_WhenCandidateNull_SetsDoNotReplaceAction()
    {
        // Act
        _sut.SetCandidate(null);

        // Assert
        Assert.Null(_sut.FirstCandidateTag);
        Assert.Equal(TagLinkReplaceAction.DoNotReplace, _sut.Action);
        Assert.True(_sut.CanSubmit);
    }

    [Theory]
    [InlineData(TagLinkReplaceAction.DoNotReplace, false, false, true)]
    [InlineData(TagLinkReplaceAction.ReplaceWithFirstCandidate, true, false, true)]
    [InlineData(TagLinkReplaceAction.ReplaceWithFirstCandidate, false, false, false)]
    [InlineData(TagLinkReplaceAction.ReplaceWithSelectedCandidate, false, true, true)]
    [InlineData(TagLinkReplaceAction.ReplaceWithSelectedCandidate, false, false, false)]
    public void CanSubmit_ValidatesConditions(
        TagLinkReplaceAction action,
        bool hasFirstCandidate,
        bool hasSelectedCustom,
        bool expected)
    {
        // Arrange
        _sut.Action = action;
        _sut.FirstCandidateTag = hasFirstCandidate ? new TagEntity { Id = 1, Name = "C1", OwnerId = "u1" } : null;
        _sut.SelectedCustomTag = hasSelectedCustom ? new TagEntity { Id = 2, Name = "C2", OwnerId = "u1" } : null;

        // Assert
        Assert.Equal(expected, _sut.CanSubmit);
    }

    [Fact]
    public async Task SearchTagsAsync_CallsDataProvider()
    {
        // Arrange
        var results = new List<TagEntity> { new() { Id = 5, Name = "Tag5", OwnerId = "u1" } };
        _dataProviderMock.Setup(d => d.SearchTagsAsync("query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        var tags = await _sut.SearchTagsAsync("query");

        // Assert
        Assert.Single(tags);
        Assert.Equal(5, tags.First().Id);
        _dataProviderMock.Verify(d => d.SearchTagsAsync("query", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateDecision_WhenReplaceWithFirstCandidate_ReturnsFirstCandidateId()
    {
        // Arrange
        _sut.Action = TagLinkReplaceAction.ReplaceWithFirstCandidate;
        _sut.FirstCandidateTag = new TagEntity { Id = 42, Name = "Tag42", OwnerId = "u1" };

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagLinkReplaceAction.ReplaceWithFirstCandidate, decision.Action);
        Assert.Equal(42, decision.SelectedTagId);
    }

    [Fact]
    public void CreateDecision_WhenReplaceWithSelectedCandidate_ReturnsSelectedTagId()
    {
        // Arrange
        _sut.Action = TagLinkReplaceAction.ReplaceWithSelectedCandidate;
        _sut.SelectedCustomTag = new TagEntity { Id = 88, Name = "Tag88", OwnerId = "u1" };

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagLinkReplaceAction.ReplaceWithSelectedCandidate, decision.Action);
        Assert.Equal(88, decision.SelectedTagId);
    }

    [Fact]
    public void CreateDecision_WhenDoNotReplace_ReturnsNullTagId()
    {
        // Arrange
        _sut.Action = TagLinkReplaceAction.DoNotReplace;

        // Act
        var decision = _sut.CreateDecision();

        // Assert
        Assert.Equal(TagLinkReplaceAction.DoNotReplace, decision.Action);
        Assert.Null(decision.SelectedTagId);
    }

    [Fact]
    public void CreateSkipDecision_ReturnsDoNotReplaceDecision()
    {
        // Act
        var decision = _sut.CreateSkipDecision();

        // Assert
        Assert.Equal(TagLinkReplaceAction.DoNotReplace, decision.Action);
        Assert.Null(decision.SelectedTagId);
    }
}