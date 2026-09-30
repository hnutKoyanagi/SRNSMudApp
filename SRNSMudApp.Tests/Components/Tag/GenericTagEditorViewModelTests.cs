#region

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using Xunit;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class GenericTagEditorViewModelTests
{
    private readonly Mock<ITaggingService> _taggingServiceMock = new();

    private GenericTagEditorViewModel CreateSut()
    {
        return new GenericTagEditorViewModel(_taggingServiceMock.Object);
    }

    [Fact]
    public void Constructor_WhenTaggingServiceNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GenericTagEditorViewModel(null!));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-100, false)]
    [InlineData(1, true)]
    [InlineData(42, true)]
    public void CanAdd_EvaluatesCorrectly(int tagId, bool expected)
    {
        var sut = CreateSut();
        sut.NewTagId = tagId;

        Assert.Equal(expected, sut.CanAdd);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddTagAsync_WhenNewTagIdNonPositive_ReturnsFalse(int invalidTagId)
    {
        var sut = CreateSut();
        sut.NewTagId = invalidTagId;

        var result = await sut.AddTagAsync<TaggingRequestEntity>(targetEntityId: 10);

        Assert.False(result);
        _taggingServiceMock.Verify(s => s.AddTagAsync<TaggingRequestEntity>(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AddTagAsync_WhenValidTagId_CallsServiceResetsTagIdAndReturnsTrue()
    {
        var sut = CreateSut();
        sut.NewTagId = 42;

        _taggingServiceMock
            .Setup(s => s.AddTagAsync<TaggingRequestEntity>(10, 42))
            .Returns(Task.CompletedTask);

        var result = await sut.AddTagAsync<TaggingRequestEntity>(targetEntityId: 10);

        Assert.True(result);
        Assert.Equal(0, sut.NewTagId);
        Assert.False(sut.IsProcessing);
        _taggingServiceMock.Verify(s => s.AddTagAsync<TaggingRequestEntity>(10, 42), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RemoveTagAsync_WhenTagIdNonPositive_ReturnsFalse(int invalidTagId)
    {
        var sut = CreateSut();

        var result = await sut.RemoveTagAsync<TaggingRequestEntity>(targetEntityId: 10, invalidTagId);

        Assert.False(result);
        _taggingServiceMock.Verify(s => s.RemoveTagAsync<TaggingRequestEntity>(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RemoveTagAsync_WhenValidTagId_CallsServiceAndReturnsTrue()
    {
        var sut = CreateSut();

        _taggingServiceMock
            .Setup(s => s.RemoveTagAsync<TaggingRequestEntity>(10, 55))
            .Returns(Task.CompletedTask);

        var result = await sut.RemoveTagAsync<TaggingRequestEntity>(targetEntityId: 10, tagId: 55);

        Assert.True(result);
        Assert.False(sut.IsProcessing);
        _taggingServiceMock.Verify(s => s.RemoveTagAsync<TaggingRequestEntity>(10, 55), Times.Once);
    }
}