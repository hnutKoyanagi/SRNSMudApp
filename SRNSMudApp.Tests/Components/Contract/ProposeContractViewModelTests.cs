using Moq;

using SRNSMudApp.Components.Contract;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Contract;

/// <summary>
///     ProposeContractViewModel の単体テスト。
///     bUnit を使わずに提案入力値の検証および契約サービス連携を検証する。
/// </summary>
public sealed class ProposeContractViewModelTests
{
    private const string RequesterId = "user-requester";
    private const string TagOwnerId = "tag-owner";
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly ProposeContractViewModel _sut;

    public ProposeContractViewModelTests()
    {
        _sut = new ProposeContractViewModel(_contractServiceMock.Object);
    }

    [Theory]
    [InlineData(0, 10, 1, false, "対象アイテムが無効です。")]
    [InlineData(10, 0, 1, false, "対象タグが無効です。")]
    [InlineData(10, 10, 0, false, "変更する Weight (差分) を 0 以外で指定してください。")]
    [InlineData(10, 10, 5, true, null)]
    [InlineData(10, 10, -2, true, null)]
    public void ValidateProposal_ValidatesCorrectly(
        int itemId, int tagId, int weight, bool expectedValid, string? expectedError)
    {
        (bool isValid, string? error) = ProposeContractViewModel.ValidateProposal(itemId, tagId, weight, RequesterId);

        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task ProposeContractAsync_WhenValid_CallsContractService()
    {
        var request = new TaggingRequestEntity { Id = 100, OwnerId = RequesterId, RequesterUserId = RequesterId };
        _contractServiceMock
            .Setup(c => c.ProposeGratisContractAsync(
                RequesterId,
                TagOwnerId,
                10,
                20,
                TaggingRequestType.Add,
                2,
                "Test proposal"))
            .ReturnsAsync(new Success<TaggingRequestEntity>(request));

        (bool success, string? error) = await _sut.ProposeContractAsync(
            itemId: 10,
            tagId: 20,
            tagOwnerUserId: TagOwnerId,
            proposedWeight: 2,
            requesterId: RequesterId,
            comment: "Test proposal");

        Assert.True(success);
        Assert.Null(error);
        _contractServiceMock.Verify(c => c.ProposeGratisContractAsync(
            RequesterId,
            TagOwnerId,
            10,
            20,
            TaggingRequestType.Add,
            2,
            "Test proposal"), Times.Once);
    }

    [Theory]
    [InlineData(true, 5, TaggingRequestType.Remove)]
    [InlineData(true, -3, TaggingRequestType.Remove)]
    [InlineData(false, 3, TaggingRequestType.Add)]
    [InlineData(false, -2, TaggingRequestType.DecreaseWeight)]
    public void ResolveRequestType_ResolvesCorrectly(bool isRemoval, int weight, TaggingRequestType expectedType)
    {
        var result = ProposeContractViewModel.ResolveRequestType(isRemoval, weight);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData(1, 2, 3, true, null)]
    [InlineData(0, 2, 3, false, "相互タグ付けに必要な項目を入力してください。")]
    [InlineData(1, 0, 3, false, "相互タグ付けに必要な項目を入力してください。")]
    [InlineData(1, 2, null, false, "相互タグ付けに必要な項目を入力してください。")]
    [InlineData(1, 2, 0, false, "相互タグ付けに必要な項目を入力してください。")]
    public void ValidateMutualProposal_ValidatesCorrectly(int item, int tag, int? asset, bool expectedValid, string? expectedError)
    {
        (bool isValid, string? error) = ProposeContractViewModel.ValidateMutualProposal(item, tag, asset);
        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task ProposeMutualContractAsync_CallsContractService()
    {
        var request = new TaggingRequestEntity { Id = 101, OwnerId = RequesterId, RequesterUserId = RequesterId };
        _contractServiceMock
            .Setup(c => c.ProposeMutualContractAsync(
                RequesterId,
                TagOwnerId,
                10,
                20,
                30,
                40,
                50,
                TaggingRequestType.Add,
                1))
            .ReturnsAsync(new Success<TaggingRequestEntity>(request));

        (bool success, string? error) = await _sut.ProposeMutualContractAsync(
            RequesterId,
            TagOwnerId,
            10,
            20,
            30,
            40,
            50,
            TaggingRequestType.Add,
            1);

        Assert.True(success);
        Assert.Null(error);
        _contractServiceMock.Verify(c => c.ProposeMutualContractAsync(
            RequesterId,
            TagOwnerId,
            10,
            20,
            30,
            40,
            50,
            TaggingRequestType.Add,
            1), Times.Once);
    }

    [Fact]
    public void Constructor_WhenContractServiceIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProposeContractViewModel(null!));
    }
}