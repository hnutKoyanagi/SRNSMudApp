// Components/Contract/ContractManagementViewModelTests.cs
#region

using MudBlazor;

using SRNSMudApp.Components.Contract;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

#endregion

namespace SRNSMudApp.Tests.Components.Contract;

/// <summary>
///     <see cref="ContractManagementViewModel" /> の単体テスト。
/// </summary>
public class ContractManagementViewModelTests
{
    [Theory]
    [InlineData(TradeStatus.Proposed, "提案中")]
    [InlineData(TradeStatus.Executed, "承認済み")]
    [InlineData(TradeStatus.Rejected, "拒否")]
    [InlineData(TradeStatus.Canceled, "キャンセル")]
    [InlineData((TradeStatus)999, "その他")]
    public void GetStatusDisplayText_MapsTradeStatusToJapaneseText(TradeStatus status, string expectedText)
    {
        var actual = ContractManagementViewModel.GetStatusDisplayText(status);
        Assert.Equal(expectedText, actual);
    }

    [Theory]
    [InlineData(TradeStatus.Proposed, Color.Info)]
    [InlineData(TradeStatus.Executed, Color.Success)]
    [InlineData(TradeStatus.Rejected, Color.Error)]
    [InlineData(TradeStatus.Canceled, Color.Default)]
    [InlineData((TradeStatus)999, Color.Default)]
    public void GetStatusColor_MapsTradeStatusToMudColor(TradeStatus status, Color expectedColor)
    {
        var actual = ContractManagementViewModel.GetStatusColor(status);
        Assert.Equal(expectedColor, actual);
    }

    [Fact]
    public void CanCancelContract_WhenProposed_ReturnsTrue()
    {
        var contract = new TaggingRequestEntity { OwnerId = "test-owner" };
        Assert.Equal(TradeStatus.Proposed, contract.Status);

        var actual = ContractManagementViewModel.CanCancelContract(contract);

        Assert.True(actual);
    }

    [Fact]
    public void CanCancelContract_WhenExecuted_ReturnsFalse()
    {
        var contract = new TaggingRequestEntity { OwnerId = "test-owner" };
        _ = contract.Execute();

        var actual = ContractManagementViewModel.CanCancelContract(contract);

        Assert.False(actual);
    }

    [Fact]
    public void CanCancelContract_WhenCanceled_ReturnsFalse()
    {
        var contract = new TaggingRequestEntity { OwnerId = "test-owner" };
        _ = contract.Cancel();

        var actual = ContractManagementViewModel.CanCancelContract(contract);

        Assert.False(actual);
    }

    [Fact]
    public void CanCancelContract_WhenRejected_ReturnsFalse()
    {
        var contract = new TaggingRequestEntity { OwnerId = "test-owner" };
        _ = contract.Reject(new RejectionInfo(new RejectionReason("test reason")));

        var actual = ContractManagementViewModel.CanCancelContract(contract);

        Assert.False(actual);
    }

    [Fact]
    public void CanCancelContract_WhenContractIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => ContractManagementViewModel.CanCancelContract(null!));
    }
}