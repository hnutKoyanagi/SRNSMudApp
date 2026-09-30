using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     TaggingRequestActionViewModel の単体テスト。
///     bUnit を用いずに承認・却下・キャンセルの各フローを検証する。
/// </summary>
public sealed class TaggingRequestActionViewModelTests
{
    private const string CurrentUserId = "user-requester";
    private readonly Mock<ITaggingRequestActions> _actionsMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly TaggingRequestActionViewModel _sut;

    public TaggingRequestActionViewModelTests()
    {
        _sut = new TaggingRequestActionViewModel(_actionsMock.Object, _contractServiceMock.Object);
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenValid_CallsTaggingRequestActions()
    {
        const int requestId = 10;
        _actionsMock.Setup(a => a.ApproveAsync(requestId, CurrentUserId)).ReturnsAsync(true);

        bool result = await _sut.ApproveRequestAsync(requestId, CurrentUserId);

        Assert.True(result);
        _actionsMock.Verify(a => a.ApproveAsync(requestId, CurrentUserId), Times.Once);
    }

    [Fact]
    public async Task RejectRequestAsync_WhenValid_CallsRejectViaDialog()
    {
        const int requestId = 11;
        _actionsMock.Setup(a => a.RejectViaDialogAsync(requestId, CurrentUserId)).ReturnsAsync(true);

        bool result = await _sut.RejectRequestAsync(requestId, CurrentUserId);

        Assert.True(result);
        _actionsMock.Verify(a => a.RejectViaDialogAsync(requestId, CurrentUserId), Times.Once);
    }

    [Fact]
    public async Task CancelRequestAsync_WhenOwner_CallsContractService()
    {
        var request = new TaggingRequestEntity { Id = 20, RequesterUserId = CurrentUserId, OwnerId = CurrentUserId };
        _contractServiceMock
            .Setup(c => c.CancelContractAsync(20, CurrentUserId))
            .ReturnsAsync(new Success<string>("Cancelled"));

        bool result = await _sut.CancelRequestAsync(request, CurrentUserId);

        Assert.True(result);
        _contractServiceMock.Verify(t => t.CancelContractAsync(20, CurrentUserId), Times.Once);
    }

    [Fact]
    public async Task CancelRequestAsync_WhenNotOwner_DoesNotCallService()
    {
        var request = new TaggingRequestEntity { Id = 21, RequesterUserId = "different-user", OwnerId = "different-user" };

        bool result = await _sut.CancelRequestAsync(request, CurrentUserId);

        Assert.False(result);
        _contractServiceMock.Verify(t => t.CancelContractAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }
}