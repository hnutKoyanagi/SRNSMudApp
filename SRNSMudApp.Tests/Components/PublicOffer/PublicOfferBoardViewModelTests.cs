#region

using Moq;

using MudBlazor;

using SRNSMudApp.Components.PublicOffer;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.PublicOffer;

/// <summary>
///     <see cref="PublicOfferBoardViewModel" /> の単体テスト。
///     公開オファー一覧の読み込み、取り下げ、オファー応諾後の契約承認・エラー時キャンセル処理を検証する。
/// </summary>
public sealed class PublicOfferBoardViewModelTests
{
    private readonly Mock<IPublicOfferDataProvider> _mockContractData = new();
    private readonly Mock<ITaggingContractService> _mockContractService = new();

    private PublicOfferBoardViewModel CreateViewModel()
    {
        return new PublicOfferBoardViewModel(_mockContractData.Object, _mockContractService.Object);
    }

    [Fact]
    public void Constructor_WhenContractDataIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new PublicOfferBoardViewModel(null!, _mockContractService.Object));
    }

    [Fact]
    public void Constructor_WhenContractServiceIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new PublicOfferBoardViewModel(_mockContractData.Object, null!));
    }

    [Fact]
    public void InitialState_PropertiesDefaultCorrectly()
    {
        var vm = CreateViewModel();

        Assert.Empty(vm.Offers);
        Assert.Equal(string.Empty, vm.CurrentUserId);
        Assert.True(vm.IsLoading);
    }

    [Fact]
    public async Task LoadDataAsync_LoadsOffersFromContractData()
    {
        var vm = CreateViewModel();
        var offers = new List<PublicTradeOffer>
        {
            new() { Id = 1, OwnerId = "user-1", OfferedTagId = 10, OfferedTag = new SRNSMudApp.Data.Tag { Name = "Tag1", OwnerId = "user-1" } },
            new() { Id = 2, OwnerId = "user-2", OfferedTagId = 20, OfferedTag = new SRNSMudApp.Data.Tag { Name = "Tag2", OwnerId = "user-2" } }
        };
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync(offers);

        await vm.LoadDataAsync();

        Assert.Equal(2, vm.Offers.Count);
        Assert.False(vm.IsLoading);
        _mockContractData.Verify(d => d.GetActivePublicOffersAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadDataAsync_WhenContractDataReturnsNull_SetsEmptyOffers()
    {
        var vm = CreateViewModel();
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync((List<PublicTradeOffer>)null!);

        await vm.LoadDataAsync();

        Assert.NotNull(vm.Offers);
        Assert.Empty(vm.Offers);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task DeactivateOfferAsync_WhenOfferIsNull_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        var result = await vm.DeactivateOfferAsync(null!);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("対象のオファーが指定されていません。", failure.ErrorMessage);
        }
        _mockContractData.Verify(d => d.DeactivatePublicOfferAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateOfferAsync_WhenCurrentUserIdIsNullOrWhiteSpace_ReturnsFailure(string userId)
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = userId;
        var offer = new PublicTradeOffer { Id = 1, OwnerId = "owner" };

        var result = await vm.DeactivateOfferAsync(offer);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("ユーザー情報が取得できませんでした。", failure.ErrorMessage);
        }
        _mockContractData.Verify(d => d.DeactivatePublicOfferAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateOfferAsync_WhenDeactivateFails_ReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var offer = new PublicTradeOffer { Id = 5, OwnerId = "user-1" };

        _mockContractData.Setup(d => d.DeactivatePublicOfferAsync(5, "user-1")).ReturnsAsync(false);

        var result = await vm.DeactivateOfferAsync(offer);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("オファーの取り下げに失敗しました。", failure.ErrorMessage);
        }
    }

    [Fact]
    public async Task DeactivateOfferAsync_WhenDeactivateSucceeds_ReloadsAndReturnsSuccess()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var offer = new PublicTradeOffer { Id = 5, OwnerId = "user-1" };

        _mockContractData.Setup(d => d.DeactivatePublicOfferAsync(5, "user-1")).ReturnsAsync(true);
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync(new List<PublicTradeOffer>());

        var result = await vm.DeactivateOfferAsync(offer);

        Assert.True(result is Success<bool>);
        _mockContractData.Verify(d => d.DeactivatePublicOfferAsync(5, "user-1"), Times.Once);
        _mockContractData.Verify(d => d.GetActivePublicOffersAsync(), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AcceptTriggeredContractAsync_WhenCurrentUserIdIsNullOrWhiteSpace_ReturnsFailure(string userId)
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = userId;

        var result = await vm.AcceptTriggeredContractAsync(100);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Equal("ユーザー情報が取得できませんでした。", failure.ErrorMessage);
        }
        _mockContractService.Verify(s => s.AcceptContractAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task AcceptTriggeredContractAsync_WhenAcceptSucceeds_ReloadsAndReturnsSuccess()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        _mockContractService.Setup(s => s.AcceptContractAsync(100, "user-1", null))
            .ReturnsAsync(new Success<string>("ok"));
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync(new List<PublicTradeOffer>());

        var result = await vm.AcceptTriggeredContractAsync(100);

        Assert.True(result is Success<bool>);
        _mockContractService.Verify(s => s.AcceptContractAsync(100, "user-1", null), Times.Once);
        _mockContractData.Verify(d => d.GetActivePublicOffersAsync(), Times.Once);
    }

    [Fact]
    public async Task AcceptTriggeredContractAsync_WhenAcceptThrows_CallsCancelContractAsyncAndReturnsFailure()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        _mockContractService.Setup(s => s.AcceptContractAsync(100, "user-1", null))
            .ThrowsAsync(new InvalidOperationException("Contract expired"));
        _mockContractService.Setup(s => s.CancelContractAsync(100, "user-1"))
            .ReturnsAsync(new Success<string>("ok"));
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync(new List<PublicTradeOffer>());

        var result = await vm.AcceptTriggeredContractAsync(100);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("Contract expired", failure.ErrorMessage);
        }
        _mockContractService.Verify(s => s.CancelContractAsync(100, "user-1"), Times.Once);
        _mockContractData.Verify(d => d.GetActivePublicOffersAsync(), Times.Once);
    }

    [Fact]
    public async Task AcceptTriggeredContractAsync_WhenAcceptAndCancelBothThrow_ReturnsFailureWithoutCrashing()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        _mockContractService.Setup(s => s.AcceptContractAsync(100, "user-1", null))
            .ThrowsAsync(new InvalidOperationException("Contract failed"));
        _mockContractService.Setup(s => s.CancelContractAsync(100, "user-1"))
            .ThrowsAsync(new Exception("Cancel failed too"));
        _mockContractData.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync(new List<PublicTradeOffer>());

        var result = await vm.AcceptTriggeredContractAsync(100);

        Assert.True(result is Failure);
        if (result is Failure failure)
        {
            Assert.Contains("Contract failed", failure.ErrorMessage);
        }
    }

    [Fact]
    public void IsOwner_WhenOfferIsNull_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        Assert.False(vm.IsOwner(null));
    }

    [Fact]
    public void IsOwner_WhenCurrentUserIdIsEmpty_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "";
        var offer = new PublicTradeOffer { OwnerId = "user-1" };

        Assert.False(vm.IsOwner(offer));
    }

    [Fact]
    public void IsOwner_WhenOwnerIdMatches_ReturnsTrue()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var offer = new PublicTradeOffer { OwnerId = "user-1" };

        Assert.True(vm.IsOwner(offer));
    }

    [Fact]
    public void IsOwner_WhenOwnerIdDoesNotMatch_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";
        var offer = new PublicTradeOffer { OwnerId = "user-2" };

        Assert.False(vm.IsOwner(offer));
    }

    [Fact]
    public void CreateDialogOptions_ReturnsExpectedOptions()
    {
        var options = PublicOfferBoardViewModel.CreateDialogOptions();

        Assert.True(options.CloseOnEscapeKey);
        Assert.Equal(MaxWidth.Small, options.MaxWidth);
        Assert.True(options.FullWidth);
    }

    [Fact]
    public void TriggerDialogParameters_WhenOfferIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => PublicOfferBoardViewModel.TriggerDialogParameters(null!));
    }

    [Fact]
    public void TriggerDialogParameters_ContainsOffer()
    {
        var offer = new PublicTradeOffer { Id = 10, OwnerId = "user-1" };

        var parameters = PublicOfferBoardViewModel.TriggerDialogParameters(offer);

        Assert.Same(offer, parameters[nameof(TriggerPublicOfferDialog.Offer)]);
    }

    [Fact]
    public void TriggerDialogOptions_ReturnsExpectedOptions()
    {
        var options = PublicOfferBoardViewModel.TriggerDialogOptions();

        Assert.True(options.CloseOnEscapeKey);
        Assert.Equal(MaxWidth.Small, options.MaxWidth);
        Assert.True(options.FullWidth);
    }
}