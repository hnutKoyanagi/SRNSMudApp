#region

using Moq;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

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

    [Fact]
    public void Constructor_WhenLockServiceIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TagManagementViewModel(null!));
    }

    [Fact]
    public async Task LoadDataAsync_WhenCalled_SetsPropertiesFromService()
    {
        var mockService = new Mock<ITagLockService>();
        mockService.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        mockService.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SampleTags);

        var sut = new TagManagementViewModel(mockService.Object);
        Assert.True(sut.IsLoading);

        await sut.LoadDataAsync();

        Assert.False(sut.IsLoading);
        Assert.Equal(3, sut.CurrentConfiguredLevel);
        Assert.Equal(3, sut.InputLevel);
        Assert.Equal(SampleTags.Length, sut.Tags.Count);
        Assert.Equal(SampleTags.Length, sut.FilteredTags.Count());
    }

    [Fact]
    public async Task SaveHierarchyLockSettingAsync_WhenSuccessful_CallsServiceAndReloadsData()
    {
        var mockService = new Mock<ITagLockService>();
        mockService.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        mockService.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SampleTags);
        mockService.Setup(s => s.SetLockedHierarchyLevelAsync(4, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = new TagManagementViewModel(mockService.Object);
        sut.InputLevel = 4;

        var result = await sut.SaveHierarchyLockSettingAsync();

        Assert.True(result is Success<int> s && s.Value == 4);
        Assert.False(sut.IsSavingSetting);
        mockService.Verify(s => s.SetLockedHierarchyLevelAsync(4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveHierarchyLockSettingAsync_WhenExceptionOccurs_ReturnsFailure()
    {
        var mockService = new Mock<ITagLockService>();
        mockService.Setup(s => s.SetLockedHierarchyLevelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Lock error"));

        var sut = new TagManagementViewModel(mockService.Object);
        sut.InputLevel = 4;

        var result = await sut.SaveHierarchyLockSettingAsync();

        Assert.True(result is Failure f && f.ErrorMessage.Contains("Lock error"));
        Assert.False(sut.IsSavingSetting);
    }

    [Fact]
    public async Task ToggleTagLockAsync_WhenSuccessful_CallsServiceAndReturnsSuccess()
    {
        var mockService = new Mock<ITagLockService>();
        mockService.Setup(s => s.ToggleTagLockAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockService.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        mockService.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new TagManagementViewModel(mockService.Object);

        var result = await sut.ToggleTagLockAsync(10);

        Assert.True(result is Success<bool> s && s.Value);
        mockService.Verify(s => s.ToggleTagLockAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleTagLockAsync_WhenExceptionOccurs_ReturnsFailure()
    {
        var mockService = new Mock<ITagLockService>();
        mockService.Setup(s => s.ToggleTagLockAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Toggle error"));

        var sut = new TagManagementViewModel(mockService.Object);

        var result = await sut.ToggleTagLockAsync(10);

        Assert.True(result is Failure f && f.ErrorMessage.Contains("Toggle error"));
    }
}