namespace SRNSMudApp.Tests.Components.Pages;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.Pages;
using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

using Xunit;

public class RightAssetOverviewViewModelTests
{
    private readonly Mock<IRightAssetDataProvider> _dataProviderMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    private RightAssetOverviewViewModel CreateViewModel()
    {
        return new RightAssetOverviewViewModel(_dataProviderMock.Object, _dialogLauncherMock.Object);
    }

    [Fact]
    public void Constructor_WhenDataProviderIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RightAssetOverviewViewModel(null!, _dialogLauncherMock.Object));
    }

    [Fact]
    public void Constructor_WhenDialogLauncherIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RightAssetOverviewViewModel(_dataProviderMock.Object, null!));
    }

    [Fact]
    public async Task InitializeAsync_LoadsTopTags()
    {
        // Arrange
        var expectedTags = new List<TagRightAssetSummary>
        {
            new(1, "Tag1", "Desc1", 100, 5),
            new(2, "Tag2", null, 50, 2)
        };
        _dataProviderMock.Setup(d => d.GetTopTagsWithRightAssetsAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTags);

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.Equal(2, vm.TopTags.Count);
        Assert.Equal("Tag1", vm.TopTags[0].TagName);
        Assert.Equal(100, vm.TopTags[0].TotalAmount);
        _dataProviderMock.Verify(d => d.GetTopTagsWithRightAssetsAsync(15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadDataByTagIdAsync_SetsOverviewDataAndSelectedTag()
    {
        // Arrange
        var tag = new Tag { Id = 42, Name = "AssetTag", CachedWeight = 10, Content = "Content", OwnerId = "user-1" };
        var overviewData = new RightAssetOverviewData(
            Tag: tag,
            TotalHoldersCount: 3,
            TotalActiveAmount: 300,
            TotalActiveAssetsCount: 4,
            TotalBurnedAmount: 50,
            Holders: [],
            Assets: []);

        _dataProviderMock.Setup(d => d.GetRightAssetOverviewByTagIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overviewData);

        var vm = CreateViewModel();

        // Act
        await vm.LoadDataByTagIdAsync(42);

        // Assert
        Assert.False(vm.IsLoading);
        Assert.Equal(42, vm.LoadedTagId);
        Assert.NotNull(vm.OverviewData);
        Assert.Equal("AssetTag", vm.OverviewData.Tag.Name);
        Assert.Same(tag, vm.SelectedTag);
    }

    [Fact]
    public async Task LoadDataByTagIdAsync_WhenDataNotFound_SetsLoadedTagIdAndNullOverview()
    {
        // Arrange
        _dataProviderMock.Setup(d => d.GetRightAssetOverviewByTagIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RightAssetOverviewData?)null);

        var vm = CreateViewModel();

        // Act
        await vm.LoadDataByTagIdAsync(999);

        // Assert
        Assert.False(vm.IsLoading);
        Assert.Equal(999, vm.LoadedTagId);
        Assert.Null(vm.OverviewData);
        Assert.Null(vm.SelectedTag);
    }

    [Fact]
    public async Task ClearSelection_ResetsProperties()
    {
        // Arrange
        var tag = new Tag { Id = 1, Name = "Tag", OwnerId = "user-1" };
        var vm = CreateViewModel();
        _dataProviderMock.Setup(d => d.GetRightAssetOverviewByTagIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RightAssetOverviewData(tag, 1, 10, 1, 0, [], []));

        await vm.LoadDataByTagIdAsync(1);
        Assert.NotNull(vm.OverviewData);

        // Act
        vm.ClearSelection();

        // Assert
        Assert.Null(vm.SelectedTag);
        Assert.Null(vm.OverviewData);
        Assert.Null(vm.LoadedTagId);
    }

    [Theory]
    [InlineData(100, 200, 50.0)]
    [InlineData(0, 100, 0.0)]
    [InlineData(25, 100, 25.0)]
    [InlineData(50, 0, 0.0)]
    [InlineData(10, -5, 0.0)]
    public void CalculateShare_CalculatesPercentageSafely(int userAmount, int totalActiveAmount, double expected)
    {
        // Act
        var share = RightAssetOverviewViewModel.CalculateShare(userAmount, totalActiveAmount);

        // Assert
        Assert.Equal(expected, share);
    }

    [Fact]
    public async Task OpenPurchaseDialogAsync_WhenNoOverviewData_ReturnsFalseWithoutOpeningDialog()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var result = await vm.OpenPurchaseDialogAsync();

        // Assert
        Assert.False(result);
        _dialogLauncherMock.Verify(d => d.ShowAsync(
            typeof(PurchaseRightAssetDialog),
            It.IsAny<string>(),
            It.IsAny<DialogParameters>(),
            It.IsAny<DialogOptions>()), Times.Never);
    }

    [Fact]
    public async Task OpenPurchaseDialogAsync_WhenDialogSucceeds_ReloadsDataAndReturnsTrue()
    {
        // Arrange
        var tag = new Tag { Id = 10, Name = "BuyTag", OwnerId = "user-1" };
        var overviewData = new RightAssetOverviewData(tag, 1, 100, 1, 0, [], []);
        _dataProviderMock.Setup(d => d.GetRightAssetOverviewByTagIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overviewData);

        var dialogRefMock = new Mock<IDialogReference>();
        dialogRefMock.Setup(d => d.Result)
            .ReturnsAsync(DialogResult.Ok(true));

        _dialogLauncherMock.Setup(d => d.ShowAsync(
                typeof(PurchaseRightAssetDialog),
                "操作権限の購入 (JPYC)",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        var vm = CreateViewModel();
        await vm.LoadDataByTagIdAsync(10);

        // Act
        var result = await vm.OpenPurchaseDialogAsync();

        // Assert
        Assert.True(result);
        _dataProviderMock.Verify(d => d.GetRightAssetOverviewByTagIdAsync(10, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OpenPurchaseDialogAsync_WhenDialogCanceled_DoesNotReloadAndReturnsFalse()
    {
        // Arrange
        var tag = new Tag { Id = 10, Name = "BuyTag", OwnerId = "user-1" };
        var overviewData = new RightAssetOverviewData(tag, 1, 100, 1, 0, [], []);
        _dataProviderMock.Setup(d => d.GetRightAssetOverviewByTagIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overviewData);

        var dialogRefMock = new Mock<IDialogReference>();
        dialogRefMock.Setup(d => d.Result)
            .ReturnsAsync(DialogResult.Cancel());

        _dialogLauncherMock.Setup(d => d.ShowAsync(
                typeof(PurchaseRightAssetDialog),
                "操作権限の購入 (JPYC)",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        var vm = CreateViewModel();
        await vm.LoadDataByTagIdAsync(10);

        // Act
        var result = await vm.OpenPurchaseDialogAsync();

        // Assert
        Assert.False(result);
        _dataProviderMock.Verify(d => d.GetRightAssetOverviewByTagIdAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }
}