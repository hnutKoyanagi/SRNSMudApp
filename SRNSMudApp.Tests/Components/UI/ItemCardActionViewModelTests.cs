using Moq;

using MudBlazor;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Resources;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
///     ItemCardActionViewModel の単体テスト。
///     bUnit を用いずにプライベートリプライ解決や管理者非公開化判定、契約リクエスト操作を検証する。
/// </summary>
public sealed class ItemCardActionViewModelTests
{
    private const string CurrentUserId = "user-100";
    private readonly Mock<IItemReplyService> _replyServiceMock = new();
    private readonly Mock<IItemCardDataProvider> _cardDataMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly ItemCardActionViewModel _sut;

    public ItemCardActionViewModelTests()
    {
        _sut = new ItemCardActionViewModel(
            _replyServiceMock.Object,
            _cardDataMock.Object,
            _contractServiceMock.Object,
            _snackbarMock.Object);
    }

    [Fact]
    public async Task SubmitReplyAsync_WhenParentIsPrivate_InheritsTargetUserGroupId()
    {
        var parentItem = new SRNSMudApp.Data.Item
        {
            Id = 10,
            IsPrivate = true,
            TargetUserGroupId = 42,
            OwnerId = CurrentUserId
        };

        var createdReply = new SRNSMudApp.Data.Item { Id = 200, ParentItemId = 10, OwnerId = CurrentUserId };
        _replyServiceMock.Setup(r => r.AddItemReplyAsync(
            10,
            "Private reply content",
            CurrentUserId,
            It.IsAny<IEnumerable<string>>(),
            true,
            42))
            .ReturnsAsync(createdReply);

        SRNSMudApp.Data.Item? result = await _sut.SubmitReplyAsync(
            parentItem,
            "Private reply content",
            CurrentUserId,
            isPrivateRequested: true);

        Assert.NotNull(result);
        Assert.Equal(200, result.Id);
        _replyServiceMock.Verify(r => r.AddItemReplyAsync(
            10,
            "Private reply content",
            CurrentUserId,
            It.IsAny<IEnumerable<string>>(),
            true,
            42), Times.Once);
    }

    [Fact]
    public async Task SubmitReplyAsync_WhenTargetUserIdsProvided_PassesToService()
    {
        var parentItem = new SRNSMudApp.Data.Item
        {
            Id = 10,
            IsPrivate = false,
            OwnerId = CurrentUserId
        };

        var targetIds = new[] { "target-1", "target-2" };
        var createdReply = new SRNSMudApp.Data.Item { Id = 201, ParentItemId = 10, OwnerId = CurrentUserId };

        _replyServiceMock.Setup(r => r.AddItemReplyAsync(
            10,
            "Reply with targets",
            CurrentUserId,
            targetIds,
            false,
            null))
            .ReturnsAsync(createdReply);

        SRNSMudApp.Data.Item? result = await _sut.SubmitReplyAsync(
            parentItem,
            "Reply with targets",
            CurrentUserId,
            isPrivateRequested: false,
            targetUserIds: targetIds);

        Assert.NotNull(result);
        Assert.Equal(201, result.Id);
    }

    [Theory]
    [InlineData(null, "content", "user-1")]
    [InlineData("item", "", "user-1")]
    [InlineData("item", "   ", "user-1")]
    [InlineData("item", "content", "")]
    [InlineData("item", "content", null)]
    public async Task SubmitReplyAsync_WhenArgumentsInvalid_ReturnsNull(string? itemPresent, string replyContent, string? userId)
    {
        var parentItem = itemPresent != null ? new SRNSMudApp.Data.Item { Id = 1, OwnerId = "user-1" } : null;

        SRNSMudApp.Data.Item? result = await _sut.SubmitReplyAsync(
            parentItem!,
            replyContent,
            userId!,
            isPrivateRequested: false);

        Assert.Null(result);
        _replyServiceMock.Verify(r => r.AddItemReplyAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<bool>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task SetAdminHiddenAsync_WhenAdminAndValid_CallsDataProvider()
    {
        _cardDataMock.Setup(d => d.SetAdminHiddenAsync(10, true, "reason", CurrentUserId)).ReturnsAsync(true);

        bool result = await _sut.SetAdminHiddenAsync(10, true, "reason", CurrentUserId, isAdmin: true);

        Assert.True(result);
        _cardDataMock.Verify(d => d.SetAdminHiddenAsync(10, true, "reason", CurrentUserId), Times.Once);
    }

    [Theory]
    [InlineData(0, "user-1", true)]
    [InlineData(-1, "user-1", true)]
    [InlineData(10, "", true)]
    [InlineData(10, null, true)]
    [InlineData(10, "user-1", false)]
    public async Task SetAdminHiddenAsync_WhenArgumentsInvalidOrNotAdmin_ReturnsFalse(int itemId, string? adminUserId, bool isAdmin)
    {
        bool result = await _sut.SetAdminHiddenAsync(itemId, true, null, adminUserId!, isAdmin);

        Assert.False(result);
        _cardDataMock.Verify(d => d.SetAdminHiddenAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItemByAdminAsync_WhenAdminAndValid_CallsDataProvider()
    {
        _cardDataMock.Setup(d => d.DeleteItemByAdminAsync(10, CurrentUserId)).ReturnsAsync(true);

        bool result = await _sut.DeleteItemByAdminAsync(10, CurrentUserId, isAdmin: true);

        Assert.True(result);
        _cardDataMock.Verify(d => d.DeleteItemByAdminAsync(10, CurrentUserId), Times.Once);
    }

    [Theory]
    [InlineData(0, "user-1", true)]
    [InlineData(-1, "user-1", true)]
    [InlineData(10, "", true)]
    [InlineData(10, null, true)]
    [InlineData(10, "user-1", false)]
    public async Task DeleteItemByAdminAsync_WhenArgumentsInvalidOrNotAdmin_ReturnsFalse(int itemId, string? adminUserId, bool isAdmin)
    {
        bool result = await _sut.DeleteItemByAdminAsync(itemId, adminUserId!, isAdmin);

        Assert.False(result);
        _cardDataMock.Verify(d => d.DeleteItemByAdminAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateItemContentAsync_WhenValid_UpdatesContentAndReturnsTrue()
    {
        _cardDataMock.Setup(d => d.UpdateItemContentAsync(100, "Updated content")).ReturnsAsync(true);

        (bool success, string? error) = await _sut.UpdateItemContentAsync(100, "Updated content");

        Assert.True(success);
        Assert.Null(error);
        _cardDataMock.Verify(d => d.UpdateItemContentAsync(100, "Updated content"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateItemContentAsync_WhenContentEmpty_ReturnsError(string? invalidContent)
    {
        (bool success, string? error) = await _sut.UpdateItemContentAsync(100, invalidContent!);

        Assert.False(success);
        Assert.Equal("内容は空にできません。", error);
        _cardDataMock.Verify(d => d.UpdateItemContentAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateItemContentAsync_WhenContentTooLong_ReturnsError()
    {
        var longContent = new string('a', 1001);
        (bool success, string? error) = await _sut.UpdateItemContentAsync(100, longContent);

        Assert.False(success);
        Assert.Equal("内容は1000文字以内で入力してください。", error);
        _cardDataMock.Verify(d => d.UpdateItemContentAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateItemContentAsync_WhenItemIdInvalid_ReturnsError()
    {
        (bool success, string? error) = await _sut.UpdateItemContentAsync(0, "Valid content");

        Assert.False(success);
        Assert.Equal("対象のアイテムが無効です。", error);
        _cardDataMock.Verify(d => d.UpdateItemContentAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CancelTaggingRequestAsync_WhenValid_CancelsAndShowsSnackbar()
    {
        var request = new TaggingRequestEntity
        {
            Id = 50,
            ContractType = "Gratis",
            OwnerId = CurrentUserId,
            RequesterUserId = CurrentUserId
        };

        _contractServiceMock
            .Setup(c => c.CancelContractAsync(50, CurrentUserId))
            .ReturnsAsync(new Success<string>("契約をキャンセルしました。"));

        bool result = await _sut.CancelTaggingRequestAsync(request, CurrentUserId);

        Assert.True(result);
        Assert.Equal(TradeStatus.Canceled, request.Status);
        _contractServiceMock.Verify(c => c.CancelContractAsync(50, CurrentUserId), Times.Once);
        _snackbarMock.Verify(s => s.Add(ErrorMessages.ContractCancelSuccess, Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task CancelTaggingRequestAsync_WhenRequestNullOrUserEmpty_ReturnsFalse()
    {
        Assert.False(await _sut.CancelTaggingRequestAsync(null, CurrentUserId));
        Assert.False(await _sut.CancelTaggingRequestAsync(new TaggingRequestEntity { Id = 1, OwnerId = CurrentUserId }, ""));
        _contractServiceMock.Verify(c => c.CancelContractAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CancelTaggingRequestAsync_WhenServiceFails_ShowsErrorSnackbar()
    {
        var request = new TaggingRequestEntity
        {
            Id = 50,
            ContractType = "Gratis",
            OwnerId = CurrentUserId,
            RequesterUserId = CurrentUserId
        };

        _contractServiceMock
            .Setup(c => c.CancelContractAsync(50, CurrentUserId))
            .ReturnsAsync(new Failure("既に承認されています。"));

        bool result = await _sut.CancelTaggingRequestAsync(request, CurrentUserId);

        Assert.False(result);
        Assert.NotEqual(TradeStatus.Canceled, request.Status);
        _snackbarMock.Verify(s => s.Add("エラー: 既に承認されています。", Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task ApproveTaggingRequestAsync_WhenValid_ExecutesAndShowsSnackbar()
    {
        var request = new TaggingRequestEntity
        {
            Id = 60,
            ContractType = "Gratis",
            OwnerId = CurrentUserId,
            RequesterUserId = CurrentUserId
        };

        _contractServiceMock
            .Setup(c => c.AcceptContractAsync(60, CurrentUserId))
            .ReturnsAsync(new Success<string>("契約を承認しました。"));

        bool result = await _sut.ApproveTaggingRequestAsync(request, CurrentUserId);

        Assert.True(result);
        Assert.Equal(TradeStatus.Executed, request.Status);
        _contractServiceMock.Verify(c => c.AcceptContractAsync(60, CurrentUserId), Times.Once);
        _snackbarMock.Verify(s => s.Add(ErrorMessages.ContractApproveSuccess, Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task ApproveTaggingRequestAsync_WhenRequestNullOrUserEmpty_ReturnsFalse()
    {
        Assert.False(await _sut.ApproveTaggingRequestAsync(null, CurrentUserId));
        Assert.False(await _sut.ApproveTaggingRequestAsync(new TaggingRequestEntity { Id = 1, OwnerId = CurrentUserId }, ""));
        _contractServiceMock.Verify(c => c.AcceptContractAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApproveTaggingRequestAsync_WhenServiceFails_ShowsErrorSnackbar()
    {
        var request = new TaggingRequestEntity
        {
            Id = 60,
            ContractType = "Gratis",
            OwnerId = CurrentUserId,
            RequesterUserId = CurrentUserId
        };

        _contractServiceMock
            .Setup(c => c.AcceptContractAsync(60, CurrentUserId))
            .ReturnsAsync(new Failure("契約の有効期限が切れています。"));

        bool result = await _sut.ApproveTaggingRequestAsync(request, CurrentUserId);

        Assert.False(result);
        Assert.NotEqual(TradeStatus.Executed, request.Status);
        _snackbarMock.Verify(s => s.Add("エラー: 契約の有効期限が切れています。", Severity.Error, null, null), Times.Once);
    }
}