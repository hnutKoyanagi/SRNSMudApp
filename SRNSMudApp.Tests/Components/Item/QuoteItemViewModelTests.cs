using Moq;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using ItemEntity = SRNSMudApp.Data.Item;

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     <see cref="QuoteItemViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的に引用投稿、バリデーション、エラーハンドリングを検証する。
/// </summary>
public sealed class QuoteItemViewModelTests
{
    private readonly Mock<IItemQuoteService> _itemQuoteServiceMock = new();
    private readonly QuoteItemViewModel _sut;

    public QuoteItemViewModelTests()
    {
        _sut = new QuoteItemViewModel(_itemQuoteServiceMock.Object);
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Null(_sut.QuotedItem);
        Assert.Empty(_sut.CurrentUserId);
        Assert.Empty(_sut.Content);
        Assert.False(_sut.IsSubmitting);
        Assert.False(_sut.CanSubmit);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("素晴らしい考察ですね", true)]
    public void CanSubmit_ValidatesContent(string content, bool expected)
    {
        // Arrange
        _sut.Content = content;

        // Assert
        Assert.Equal(expected, _sut.CanSubmit);
    }

    [Fact]
    public async Task SubmitAsync_WhenContentEmpty_ReturnsFailure()
    {
        // Arrange
        _sut.Content = "   ";
        _sut.CurrentUserId = "user-1";
        _sut.QuotedItem = new ItemEntity { Id = 1, OwnerId = "owner-1", Content = "Original" };

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("本文を入力してください", failure.ErrorMessage);
        }

        _itemQuoteServiceMock.Verify(
            s => s.CreateQuoteItemAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenUserIdEmpty_ReturnsFailure()
    {
        // Arrange
        _sut.Content = "引用コメント";
        _sut.CurrentUserId = "";
        _sut.QuotedItem = new ItemEntity { Id = 1, OwnerId = "owner-1", Content = "Original" };

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("ログインが必要です", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenQuotedItemNull_ReturnsFailure()
    {
        // Arrange
        _sut.Content = "引用コメント";
        _sut.CurrentUserId = "user-1";
        _sut.QuotedItem = null;

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("指定されていません", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenValid_CallsServiceAndReturnsSuccess()
    {
        // Arrange
        const int quotedId = 42;
        const string comment = "この引用について";
        const string userId = "user-100";
        var quotedItem = new ItemEntity { Id = quotedId, OwnerId = "owner-1", Content = "Original item" };
        var createdItem = new ItemEntity { Id = 101, OwnerId = userId, Content = comment };

        _sut.QuotedItem = quotedItem;
        _sut.CurrentUserId = userId;
        _sut.Content = comment;

        _itemQuoteServiceMock.Setup(s => s.CreateQuoteItemAsync(
                quotedId, comment, userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdItem);

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Success<ItemEntity>);
        if (result is Success<ItemEntity> success)
        {
            Assert.Equal(101, success.Value.Id);
            Assert.Equal(comment, success.Value.Content);
        }

        Assert.False(_sut.IsSubmitting);
        _itemQuoteServiceMock.Verify(s => s.CreateQuoteItemAsync(
            quotedId, comment, userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenServiceReturnsNull_ReturnsFailure()
    {
        // Arrange
        _sut.QuotedItem = new ItemEntity { Id = 99, OwnerId = "owner-1", Content = "Deleted" };
        _sut.CurrentUserId = "user-1";
        _sut.Content = "Quote";

        _itemQuoteServiceMock.Setup(s => s.CreateQuoteItemAsync(
                99, "Quote", "user-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemEntity?)null);

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("見つかりませんでした", failure.ErrorMessage);
        }

        Assert.False(_sut.IsSubmitting);
    }

    [Fact]
    public async Task SubmitAsync_WhenServiceThrows_ReturnsFailureAndResetsSubmitting()
    {
        // Arrange
        _sut.QuotedItem = new ItemEntity { Id = 99, OwnerId = "owner-1", Content = "Test" };
        _sut.CurrentUserId = "user-1";
        _sut.Content = "Quote";

        _itemQuoteServiceMock.Setup(s => s.CreateQuoteItemAsync(
                99, "Quote", "user-1", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("DB error", failure.ErrorMessage);
        }

        Assert.False(_sut.IsSubmitting);
    }
}