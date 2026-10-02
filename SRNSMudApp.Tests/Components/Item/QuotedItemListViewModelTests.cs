using Moq;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using ItemEntity = SRNSMudApp.Data.Item;

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     <see cref="QuotedItemListViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的にデータ取得、計算プロパティ、エラーハンドリングを検証する。
/// </summary>
public sealed class QuotedItemListViewModelTests
{
    private readonly Mock<IItemQuoteService> _itemQuoteServiceMock = new();
    private readonly QuotedItemListViewModel _sut;

    public QuotedItemListViewModelTests()
    {
        _sut = new QuotedItemListViewModel(_itemQuoteServiceMock.Object);
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Null(_sut.TargetItem);
        Assert.Null(_sut.SourceItem);
        Assert.Empty(_sut.QuotedItems);
        Assert.True(_sut.IsLoading);
        Assert.False(_sut.HasSourceItem);
        Assert.False(_sut.HasQuotedItems);
        Assert.False(_sut.ShowNoQuotesMessage);
    }

    [Fact]
    public async Task LoadAsync_WhenTargetItemNull_ResetsAndEndsLoading()
    {
        // Arrange
        _sut.TargetItem = null;

        // Act
        await _sut.LoadAsync();

        // Assert
        Assert.False(_sut.IsLoading);
        Assert.Null(_sut.SourceItem);
        Assert.Empty(_sut.QuotedItems);
        Assert.True(_sut.ShowNoQuotesMessage);
        _itemQuoteServiceMock.Verify(s => s.GetSourceItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemQuoteServiceMock.Verify(s => s.GetQuotedByItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_WhenSourceAndQuotesExist_PopulatesProperties()
    {
        // Arrange
        const int targetId = 10;
        _sut.TargetItem = new ItemEntity { Id = targetId, OwnerId = "user-1", Content = "Target" };

        var sourceItem = new ItemEntity { Id = 5, OwnerId = "user-1", Content = "Source" };
        var quotedList = new List<ItemEntity>
        {
            new() { Id = 11, OwnerId = "user-2", Content = "Quote 1" },
            new() { Id = 12, OwnerId = "user-3", Content = "Quote 2" }
        };

        _itemQuoteServiceMock.Setup(s => s.GetSourceItemAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceItem);
        _itemQuoteServiceMock.Setup(s => s.GetQuotedByItemsAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotedList);

        // Act
        await _sut.LoadAsync();

        // Assert
        Assert.False(_sut.IsLoading);
        Assert.True(_sut.HasSourceItem);
        Assert.True(_sut.HasQuotedItems);
        Assert.False(_sut.ShowNoQuotesMessage);
        Assert.NotNull(_sut.SourceItem);
        Assert.Equal(5, _sut.SourceItem.Id);
        Assert.Equal(2, _sut.QuotedItems.Count);
    }

    [Fact]
    public async Task LoadAsync_WhenNoSourceAndNoQuotes_SetsShowNoQuotesMessage()
    {
        // Arrange
        const int targetId = 20;
        _sut.TargetItem = new ItemEntity { Id = targetId, OwnerId = "user-1", Content = "Target" };

        _itemQuoteServiceMock.Setup(s => s.GetSourceItemAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemEntity?)null);
        _itemQuoteServiceMock.Setup(s => s.GetQuotedByItemsAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.LoadAsync();

        // Assert
        Assert.False(_sut.IsLoading);
        Assert.False(_sut.HasSourceItem);
        Assert.False(_sut.HasQuotedItems);
        Assert.True(_sut.ShowNoQuotesMessage);
    }

    [Fact]
    public async Task LoadAsync_WhenServiceThrows_CatchesExceptionAndEndsLoading()
    {
        // Arrange
        const int targetId = 30;
        _sut.TargetItem = new ItemEntity { Id = targetId, OwnerId = "user-1", Content = "Target" };

        _itemQuoteServiceMock.Setup(s => s.GetSourceItemAsync(targetId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        await _sut.LoadAsync();

        // Assert
        Assert.False(_sut.IsLoading);
        Assert.Null(_sut.SourceItem);
        Assert.Empty(_sut.QuotedItems);
    }
}