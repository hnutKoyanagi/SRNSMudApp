#region

using System.Text;

using Moq;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     <see cref="ItemImportViewModel" /> の単体テスト。
///     CSVストリームのパース、ファイルサイズバリデーション、ユーザー認証検証、インポート処理を検証する。
/// </summary>
public sealed class ItemImportViewModelTests
{
    private readonly Mock<IAdminDataProvider> _mockAdminData = new();

    private ItemImportViewModel CreateViewModel()
    {
        return new ItemImportViewModel(_mockAdminData.Object);
    }

    [Fact]
    public void Constructor_WhenAdminDataProviderIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ItemImportViewModel(null!));
    }

    [Fact]
    public void InitialState_PropertiesDefaultCorrectly()
    {
        var vm = CreateViewModel();

        Assert.Equal(string.Empty, vm.CurrentUserId);
        Assert.False(vm.IsUploading);
    }

    [Fact]
    public async Task ParseCsvStreamAsync_WhenStreamIsNull_ThrowsArgumentNullException()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => ItemImportViewModel.ParseCsvStreamAsync(null!));
    }

    [Fact]
    public async Task ParseCsvStreamAsync_ParsesValidCsvLinesAndTrims()
    {
        var csvContent = "item1, tagA, tagB\nitem2, tagC";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var lines = await ItemImportViewModel.ParseCsvStreamAsync(stream);

        Assert.Equal(2, lines.Count);
        Assert.Equal(["item1", "tagA", "tagB"], lines[0]);
        Assert.Equal(["item2", "tagC"], lines[1]);
    }

    [Fact]
    public async Task ParseCsvStreamAsync_SkipsEmptyLinesAndWhitespace()
    {
        var csvContent = "item1, tagA\n\n   \nitem2, tagB\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var lines = await ItemImportViewModel.ParseCsvStreamAsync(stream);

        Assert.Equal(2, lines.Count);
        Assert.Equal("item1", lines[0][0]);
        Assert.Equal("item2", lines[1][0]);
    }

    [Fact]
    public async Task ParseCsvStreamAsync_SkipsLinesWithEmptyFirstColumn()
    {
        var csvContent = ", tagA, tagB\n   , tagC\nvalidItem, tagD";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var lines = await ItemImportViewModel.ParseCsvStreamAsync(stream);

        Assert.Single(lines);
        Assert.Equal("validItem", lines[0][0]);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenStreamIsNull_ThrowsArgumentNullException()
    {
        var vm = CreateViewModel();
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => vm.ImportCsvAsync(null!, 100));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImportCsvAsync_WhenCurrentUserIdEmpty_ReturnsFailure(string userId)
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = userId;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("item1, tag1"));

        var result = await vm.ImportCsvAsync(stream, 100);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("ログインが必要です。", failure.ErrorMessage);
        }
        _mockAdminData.Verify(d => d.ImportItemsWithTagsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string[]>>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenFileSizeExceeds5MB_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("item1, tag1"));
        long tooLargeSize = ItemImportViewModel.MaxFileSizeBytes + 1;

        var result = await vm.ImportCsvAsync(stream, tooLargeSize);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("5MB以下", failure.ErrorMessage);
        }
        _mockAdminData.Verify(d => d.ImportItemsWithTagsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string[]>>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenNoValidLines_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\n  \n,tag1,tag2"));

        var result = await vm.ImportCsvAsync(stream, 100);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("処理するデータがありませんでした。", failure.ErrorMessage);
        }
        _mockAdminData.Verify(d => d.ImportItemsWithTagsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string[]>>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenValidCsv_CallsProviderAndReturnsSuccess()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var csvContent = "item1, tagA\nitem2, tagB";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        _mockAdminData.Setup(d => d.ImportItemsWithTagsAsync("user-1", It.Is<IReadOnlyList<string[]>>(l => l.Count == 2)))
            .ReturnsAsync(2);

        var result = await vm.ImportCsvAsync(stream, csvContent.Length);

        Assert.True(result is Success<int>);
        if (result is Success<int> success)
        {
            Assert.Equal(2, success.Value);
        }
        Assert.False(vm.IsUploading);
        _mockAdminData.Verify(d => d.ImportItemsWithTagsAsync("user-1", It.IsAny<IReadOnlyList<string[]>>()), Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenProviderThrows_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var csvContent = "item1, tagA";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        _mockAdminData.Setup(d => d.ImportItemsWithTagsAsync("user-1", It.IsAny<IReadOnlyList<string[]>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await vm.ImportCsvAsync(stream, csvContent.Length);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("DB error", failure.ErrorMessage);
        }
        Assert.False(vm.IsUploading);
    }
}