using Moq;

using SRNSMudApp.Components.PublicOffer;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Commands;

using ItemEntity = SRNSMudApp.Data.Item;

namespace SRNSMudApp.Tests.Components.PublicOffer;

/// <summary>
///     <see cref="TriggerPublicOfferViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的にオファー承諾（トリガー契約作成）、アセット読み込み、アイテム検索、バリデーションを検証する。
/// </summary>
public sealed class TriggerPublicOfferViewModelTests
{
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ICommandHandler<CreateTriggerContractCommand, Result<bool>>> _createTriggerHandlerMock = new();

    private readonly TriggerPublicOfferViewModel _sut;

    public TriggerPublicOfferViewModelTests()
    {
        _sut = new TriggerPublicOfferViewModel(_contractDataMock.Object, _createTriggerHandlerMock.Object);
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Null(_sut.Offer);
        Assert.Empty(_sut.CurrentUserId);
        Assert.Null(_sut.SelectedItem);
        Assert.Null(_sut.SelectedAsset);
        Assert.Empty(_sut.ValidAssets);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public void CanSubmit_WhenOfferOrItemNull_ReturnsFalse()
    {
        // Case 1: Both null
        Assert.False(_sut.CanSubmit);

        // Case 2: Only Offer set
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", RequiredAssetAmount = 0 };
        Assert.False(_sut.CanSubmit);

        // Case 3: Only Item set
        _sut.Offer = null;
        _sut.SelectedItem = new ItemEntity { Id = 10, Content = "TestItem", OwnerId = "owner-1" };
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public void CanSubmit_WhenZeroAssetRequiredAndItemSet_ReturnsTrue()
    {
        // Arrange
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", RequiredAssetAmount = 0 };
        _sut.SelectedItem = new ItemEntity { Id = 10, Content = "TestItem", OwnerId = "owner-1" };

        // Assert
        Assert.True(_sut.CanSubmit);
    }

    [Fact]
    public async Task CanSubmit_WhenAssetRequired_ValidatesSelectedAssetAndValidAssets()
    {
        // Arrange
        _sut.CurrentUserId = "user-1";
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", OfferedTagId = 100, RequiredAssetAmount = 50 };
        _sut.SelectedItem = new ItemEntity { Id = 10, Content = "TestItem", OwnerId = "user-1" };

        // Before asset selected: false
        Assert.False(_sut.CanSubmit);

        var asset = new RightAsset { Id = 5, OwnerId = "user-1", Amount = 100 };
        _contractDataMock.Setup(d => d.GetValidRightAssetsAsync("user-1", 100, 50))
            .ReturnsAsync([asset]);

        await _sut.LoadMyAssetsAsync();
        _sut.SelectedAsset = asset;

        // Both selected and valid assets exist: true
        Assert.True(_sut.CanSubmit);
    }

    [Fact]
    public async Task LoadMyAssetsAsync_WhenOfferOrUserIdMissing_EmptiesValidAssets()
    {
        // Arrange
        _sut.CurrentUserId = "";
        _sut.Offer = null;

        // Act
        await _sut.LoadMyAssetsAsync();

        // Assert
        Assert.Empty(_sut.ValidAssets);
        _contractDataMock.Verify(d => d.GetValidRightAssetsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task LoadMyAssetsAsync_WhenConfigured_PopulatesValidAssets()
    {
        // Arrange
        _sut.CurrentUserId = "user-123";
        _sut.Offer = new PublicTradeOffer { OwnerId = "owner-1", OfferedTagId = 10, RequiredAssetAmount = 20 };
        var sampleAssets = new List<RightAsset> { new() { Id = 1, OwnerId = "user-123", Amount = 30 } };

        _contractDataMock.Setup(d => d.GetValidRightAssetsAsync("user-123", 10, 20))
            .ReturnsAsync(sampleAssets);

        // Act
        await _sut.LoadMyAssetsAsync();

        // Assert
        Assert.Single(_sut.ValidAssets);
        Assert.Equal(1, _sut.ValidAssets[0].Id);
    }

    [Fact]
    public async Task SearchItemsAsync_CallsDataProvider()
    {
        // Arrange
        var items = new List<ItemEntity> { new() { Id = 1, Content = "Item1", OwnerId = "user-1" } };
        _contractDataMock.Setup(d => d.SearchItemsAsync("query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.SearchItemsAsync("query");

        // Assert
        Assert.Single(result);
        _contractDataMock.Verify(d => d.SearchItemsAsync("query", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenOfferNull_ReturnsFailure()
    {
        // Arrange
        _sut.Offer = null;
        _sut.SelectedItem = new ItemEntity { Id = 1, Content = "Item", OwnerId = "user-1" };

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("オファーが指定されていません", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenItemNull_ReturnsFailure()
    {
        // Arrange
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1" };
        _sut.SelectedItem = null;

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("アイテムを選択してください", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenAssetRequiredButMissing_ReturnsFailure()
    {
        // Arrange
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", RequiredAssetAmount = 10 };
        _sut.SelectedItem = new ItemEntity { Id = 1, Content = "Item", OwnerId = "user-1" };
        _sut.SelectedAsset = null;

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("要求量以上のアセットを選択してください", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenZeroAsset_DispatchesTriggerCommandSuccessfully()
    {
        // Arrange
        _sut.CurrentUserId = "requester-1";
        _sut.Offer = new PublicTradeOffer
        {
            Id = 99,
            OwnerId = "offer-owner",
            OfferedTagId = 42,
            RequiredAssetAmount = 0
        };
        _sut.SelectedItem = new ItemEntity { Id = 88, Content = "TargetItem", OwnerId = "requester-1" };

        CreateTriggerContractCommand? capturedCommand = null;
        _createTriggerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CreateTriggerContractCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTriggerContractCommand, CancellationToken>((cmd, _) =>
            {
                capturedCommand = cmd;
                cmd.TriggerContract.Id = 777; // simulate DB assignment
            })
            .ReturnsAsync(Result.Ok(true));

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Success<int>);
        if (result is Success<int> success)
        {
            Assert.Equal(777, success.Value);
        }

        Assert.NotNull(capturedCommand);
        var contract = capturedCommand.TriggerContract;
        Assert.Equal("Trigger", contract.ContractType);
        Assert.Equal("requester-1", contract.OwnerId);
        Assert.Equal("requester-1", contract.RequesterUserId);
        Assert.Equal("offer-owner", contract.TagOwnerUserId);
        Assert.Equal(88, contract.TargetItemId);
        Assert.Equal(42, contract.RequestedTagId);
        Assert.Null(contract.ConsumedRightAssetId);

        Assert.True(contract.Payload is PublicOfferPayload);
        if (contract.Payload is PublicOfferPayload payload)
        {
            Assert.Equal(99, payload.TargetPublicTradeOfferId);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenPaidAsset_SetsConsumedRightAssetId()
    {
        // Arrange
        _sut.CurrentUserId = "requester-1";
        _sut.Offer = new PublicTradeOffer
        {
            Id = 99,
            OwnerId = "offer-owner",
            OfferedTagId = 42,
            RequiredAssetAmount = 50
        };
        _sut.SelectedItem = new ItemEntity { Id = 88, Content = "Item", OwnerId = "requester-1" };
        _sut.SelectedAsset = new RightAsset { Id = 123, OwnerId = "requester-1", Amount = 100 };

        CreateTriggerContractCommand? capturedCommand = null;
        _createTriggerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CreateTriggerContractCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTriggerContractCommand, CancellationToken>((cmd, _) =>
            {
                capturedCommand = cmd;
                cmd.TriggerContract.Id = 888;
            })
            .ReturnsAsync(Result.Ok(true));

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Success<int>);
        Assert.NotNull(capturedCommand);
        Assert.Equal(123, capturedCommand.TriggerContract.ConsumedRightAssetId);
    }

    [Fact]
    public async Task SubmitAsync_WhenHandlerReturnsFailure_ReturnsFailure()
    {
        // Arrange
        _sut.CurrentUserId = "requester-1";
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", RequiredAssetAmount = 0 };
        _sut.SelectedItem = new ItemEntity { Id = 1, Content = "Item", OwnerId = "requester-1" };

        _createTriggerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CreateTriggerContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<bool>("Contract handler error"));

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("Contract handler error", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitAsync_WhenHandlerThrows_ReturnsFailure()
    {
        // Arrange
        _sut.CurrentUserId = "requester-1";
        _sut.Offer = new PublicTradeOffer { Id = 1, OwnerId = "owner-1", RequiredAssetAmount = 0 };
        _sut.SelectedItem = new ItemEntity { Id = 1, Content = "Item", OwnerId = "requester-1" };

        _createTriggerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CreateTriggerContractCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal error"));

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("Fatal error", failure.ErrorMessage);
        }
    }
}