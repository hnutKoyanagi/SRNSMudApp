using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     <see cref="PurchaseRightAssetViewModel"/> の単体テスト。
///     DOM 描画や bUnit を伴わずに高速かつ決定論的に初期化、ネットワーク選択、ウォレット取得、シミュレーション、購入ロジックを検証する。
/// </summary>
public sealed class PurchaseRightAssetViewModelTests
{
    private readonly Mock<IRightAssetPurchaseService> _purchaseServiceMock = new();
    private readonly PurchaseRightAssetViewModel _sut;

    public PurchaseRightAssetViewModelTests()
    {
        _sut = new PurchaseRightAssetViewModel(_purchaseServiceMock.Object);
    }

    private static IReadOnlyList<JpycNetworkInfo> CreateSampleNetworks()
    {
        return
        [
            new JpycNetworkInfo("polygon-amoy", "Polygon Amoy Testnet", 80002, "0xAmoyContract", "https://faucet.amoy", "https://amoy.polygonscan.com", true, true),
            new JpycNetworkInfo("ethereum-sepolia", "Sepolia Testnet", 11155111, "0xSepoliaContract", "https://faucet.sepolia", "https://sepolia.etherscan.io", false, true)
        ];
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        // Assert
        Assert.Null(_sut.RequestedTag);
        Assert.Empty(_sut.CurrentUserId);
        Assert.Equal(1, _sut.Amount);
        Assert.Equal(100, _sut.UnitPriceJpyc);
        Assert.Equal(100, _sut.TotalJpyc);
        Assert.Equal("polygon-amoy", _sut.SelectedNetworkName);
        Assert.Empty(_sut.TransactionHash);
        Assert.Empty(_sut.DepositAddress);
        Assert.True(_sut.IsLoadingWallet);
        Assert.False(_sut.IsSubmitting);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public async Task InitializeAsync_SetsRecommendedNetworkAndLoadsWallet()
    {
        // Arrange
        var tag = new TagEntity { Id = 10, Name = "TargetTag", OwnerId = "user-1" };
        var networks = CreateSampleNetworks();
        _purchaseServiceMock.Setup(s => s.GetSupportedNetworks()).Returns(networks);

        var wallet = new UserDepositWalletDto("user-100", "polygon-amoy", "0xMyDepositAddress", DateTime.UtcNow);
        _purchaseServiceMock.Setup(s => s.GetOrCreateUserDepositWalletAsync("user-100", "polygon-amoy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        // Act
        await _sut.InitializeAsync(tag, "user-100", 5, 200);

        // Assert
        Assert.Same(tag, _sut.RequestedTag);
        Assert.Equal("user-100", _sut.CurrentUserId);
        Assert.Equal(5, _sut.Amount);
        Assert.Equal(200, _sut.UnitPriceJpyc);
        Assert.Equal(1000, _sut.TotalJpyc);
        Assert.Equal("polygon-amoy", _sut.SelectedNetworkName);
        Assert.Equal("0xMyDepositAddress", _sut.DepositAddress);
        Assert.False(_sut.IsLoadingWallet);
        Assert.NotNull(_sut.CurrentNetwork);
        Assert.Equal("Polygon Amoy Testnet", _sut.CurrentNetwork.DisplayName);
    }

    [Fact]
    public async Task SelectNetworkAsync_UpdatesSelectedNetworkAndReloadsWallet()
    {
        // Arrange
        var tag = new TagEntity { Id = 10, Name = "TargetTag", OwnerId = "user-1" };
        _purchaseServiceMock.Setup(s => s.GetSupportedNetworks()).Returns(CreateSampleNetworks());
        _purchaseServiceMock.Setup(s => s.GetOrCreateUserDepositWalletAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDepositWalletDto("user-1", "net", "0xDefaultAddress", DateTime.UtcNow));

        await _sut.InitializeAsync(tag, "user-1");

        var newWallet = new UserDepositWalletDto("user-1", "ethereum-sepolia", "0xSepoliaAddress", DateTime.UtcNow);
        _purchaseServiceMock.Setup(s => s.GetOrCreateUserDepositWalletAsync("user-1", "ethereum-sepolia", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newWallet);

        // Act
        await _sut.SelectNetworkAsync("ethereum-sepolia");

        // Assert
        Assert.Equal("ethereum-sepolia", _sut.SelectedNetworkName);
        Assert.Equal("0xSepoliaAddress", _sut.DepositAddress);
    }

    [Fact]
    public async Task SimulatePaymentAsync_WhenUserIdEmpty_ReturnsFailure()
    {
        // Arrange
        _sut.CurrentUserId = "";

        // Act
        var result = await _sut.SimulatePaymentAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("ログインが必要です", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SimulatePaymentAsync_WhenValid_CallsServiceAndSetsTxHash()
    {
        // Arrange
        _sut.CurrentUserId = "user-1";
        _sut.Amount = 2;
        _sut.UnitPriceJpyc = 150;
        _sut.SelectedNetworkName = "polygon-amoy";

        const string simulatedTx = "0xSimulatedTxHash123";
        _purchaseServiceMock.Setup(s => s.SimulateDepositAsync("user-1", "polygon-amoy", 300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(simulatedTx);

        // Act
        var result = await _sut.SimulatePaymentAsync();

        // Assert
        Assert.True(result is Success<string>);
        if (result is Success<string> success)
        {
            Assert.Equal(simulatedTx, success.Value);
        }

        Assert.Equal(simulatedTx, _sut.TransactionHash);
    }

    [Fact]
    public async Task SubmitPurchaseAsync_WhenTagNull_ReturnsFailure()
    {
        // Arrange
        _sut.RequestedTag = null;
        _sut.CurrentUserId = "user-1";
        _sut.TransactionHash = "0x123";

        // Act
        var result = await _sut.SubmitPurchaseAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("対象のタグが指定されていません", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitPurchaseAsync_WhenTxHashEmpty_ReturnsFailure()
    {
        // Arrange
        _sut.RequestedTag = new TagEntity { Id = 1, Name = "T", OwnerId = "u1" };
        _sut.CurrentUserId = "user-1";
        _sut.TransactionHash = "   ";

        // Act
        var result = await _sut.SubmitPurchaseAsync();

        // Assert
        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("トランザクションハッシュを入力してください", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task SubmitPurchaseAsync_WhenValid_CallsPurchaseServiceSuccessfully()
    {
        // Arrange
        var tag = new TagEntity { Id = 42, Name = "T42", OwnerId = "owner" };
        _purchaseServiceMock.Setup(s => s.GetSupportedNetworks()).Returns(CreateSampleNetworks());
        _purchaseServiceMock.Setup(s => s.GetOrCreateUserDepositWalletAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDepositWalletDto("user-1", "polygon-amoy", "0xAddr", DateTime.UtcNow));

        await _sut.InitializeAsync(tag, "user-1", 10, 50);
        _sut.TransactionHash = "0xConfirmedTx";

        var createdAsset = new RightAsset { Id = 77, OwnerId = "user-1", Amount = 10, TargetTagId = 42 };
        _purchaseServiceMock.Setup(s => s.PurchaseRightAssetWithJpycAsync(
                "user-1",
                It.Is<JpycPurchaseRequestDto>(r =>
                    r.RequestedTagId == 42 &&
                    r.Amount == 10 &&
                    r.UnitPriceJpyc == 50 &&
                    r.TransactionHash == "0xConfirmedTx"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RightAsset>(createdAsset));

        // Act
        var result = await _sut.SubmitPurchaseAsync();

        // Assert
        Assert.True(result is Success<RightAsset>);
        if (result is Success<RightAsset> success)
        {
            Assert.Equal(77, success.Value.Id);
            Assert.Equal(10, success.Value.Amount);
        }

        Assert.False(_sut.IsSubmitting);
    }

    [Theory]
    [InlineData(false, 1, 100, "0x123", "u1", true, true)]
    [InlineData(true, 1, 100, "0x123", "u1", true, false)] // Loading wallet
    [InlineData(false, 0, 100, "0x123", "u1", true, false)] // Amount < 1
    [InlineData(false, 1, 0, "0x123", "u1", true, false)] // UnitPrice < 1
    [InlineData(false, 1, 100, "", "u1", true, false)] // TxHash empty
    [InlineData(false, 1, 100, "0x123", "", true, false)] // UserId empty
    [InlineData(false, 1, 100, "0x123", "u1", false, false)] // Tag null
    public async Task CanSubmit_ValidatesAllConditions(
        bool isLoadingWallet,
        int amount,
        int unitPrice,
        string txHash,
        string userId,
        bool hasTag,
        bool expected)
    {
        // Arrange
        _sut.RequestedTag = hasTag ? new TagEntity { Id = 1, Name = "T", OwnerId = "u" } : null;
        _sut.CurrentUserId = userId;
        _sut.Amount = amount;
        _sut.UnitPriceJpyc = unitPrice;
        _sut.TransactionHash = txHash;

        if (!isLoadingWallet)
        {
            _purchaseServiceMock.Setup(s => s.GetOrCreateUserDepositWalletAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserDepositWalletDto("u", "net", "0xAddr", DateTime.UtcNow));
            await _sut.LoadDepositWalletAsync();
        }

        // Assert
        Assert.Equal(expected, _sut.CanSubmit);
    }
}