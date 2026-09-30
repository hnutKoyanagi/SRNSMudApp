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
    private readonly Mock<IItemTagService> _tagServiceMock = new();
    private readonly Mock<IItemSplitService> _splitServiceMock = new();
    private readonly Mock<IItemQuoteService> _quoteServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly ItemCardActionViewModel _sut;

    public ItemCardActionViewModelTests()
    {
        _sut = new ItemCardActionViewModel(
            _replyServiceMock.Object,
            _cardDataMock.Object,
            _contractServiceMock.Object,
            _tagServiceMock.Object,
            _splitServiceMock.Object,
            _quoteServiceMock.Object,
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

    [Fact]
    public async Task GetTaggingRequestsAsync_WhenItemIdValid_ReturnsList()
    {
        List<TaggingRequestEntity> requests = [new() { Id = 1, OwnerId = CurrentUserId }];
        _tagServiceMock.Setup(s => s.GetTaggingRequestsForItemAsync(10)).ReturnsAsync(requests);

        var result = await _sut.GetTaggingRequestsAsync(10);

        Assert.Equal(requests, result);
    }

    [Fact]
    public async Task GetTaggingRequestsAsync_WhenItemIdInvalid_ReturnsEmpty()
    {
        var result = await _sut.GetTaggingRequestsAsync(0);

        Assert.Empty(result);
        _tagServiceMock.Verify(s => s.GetTaggingRequestsForItemAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetPendingSplitRequestsAsync_WhenItemIdValid_ReturnsList()
    {
        List<ItemSplitRequest> requests = [new() { Id = 1, OriginalItemId = 10, RequesterUserId = CurrentUserId, SelectedText = "text", OwnerId = CurrentUserId }];
        _splitServiceMock.Setup(s => s.GetPendingSplitRequestsForOriginalItemAsync(10)).ReturnsAsync(requests);

        var result = await _sut.GetPendingSplitRequestsAsync(10);

        Assert.Equal(requests, result);
    }

    [Fact]
    public async Task GetPendingSplitRequestsAsync_WhenItemIdInvalid_ReturnsEmpty()
    {
        var result = await _sut.GetPendingSplitRequestsAsync(-1);

        Assert.Empty(result);
        _splitServiceMock.Verify(s => s.GetPendingSplitRequestsForOriginalItemAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetQuoteCountAsync_WhenItemIdValid_ReturnsCount()
    {
        _quoteServiceMock.Setup(s => s.GetQuoteCountAsync(10)).ReturnsAsync(5);

        int count = await _sut.GetQuoteCountAsync(10);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetQuoteCountAsync_WhenItemIdInvalid_ReturnsZero()
    {
        int count = await _sut.GetQuoteCountAsync(0);

        Assert.Equal(0, count);
        _quoteServiceMock.Verify(s => s.GetQuoteCountAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetItemRepliesAsync_WhenItemIdValid_ReturnsList()
    {
        List<SRNSMudApp.Data.Item> replies = [new() { Id = 20, OwnerId = CurrentUserId }];
        _replyServiceMock.Setup(s => s.GetItemRepliesAsync(10)).ReturnsAsync(replies);

        var result = await _sut.GetItemRepliesAsync(10);

        Assert.Equal(replies, result);
    }

    [Fact]
    public async Task GetItemRepliesAsync_WhenItemIdInvalid_ReturnsEmpty()
    {
        var result = await _sut.GetItemRepliesAsync(0);

        Assert.Empty(result);
        _replyServiceMock.Verify(s => s.GetItemRepliesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetItemReplyCountAsync_WhenServiceReturnsPositive_ReturnsServiceCount()
    {
        _replyServiceMock.Setup(s => s.GetItemReplyCountAsync(10)).ReturnsAsync(3);

        int count = await _sut.GetItemReplyCountAsync(10, fallbackCount: 1);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetItemReplyCountAsync_WhenServiceReturnsZero_ReturnsFallbackCount()
    {
        _replyServiceMock.Setup(s => s.GetItemReplyCountAsync(10)).ReturnsAsync(0);

        int count = await _sut.GetItemReplyCountAsync(10, fallbackCount: 4);

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task GetItemReplyCountAsync_WhenItemIdInvalid_ReturnsFallbackCount()
    {
        int count = await _sut.GetItemReplyCountAsync(0, fallbackCount: 2);

        Assert.Equal(2, count);
        _replyServiceMock.Verify(s => s.GetItemReplyCountAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetOptedOutUsersAsync_WhenRootItemIdValid_ReturnsSet()
    {
        HashSet<string> users = ["user-1", "user-2"];
        _replyServiceMock.Setup(s => s.GetOptedOutUsersAsync(10)).ReturnsAsync(users);

        var result = await _sut.GetOptedOutUsersAsync(10);

        Assert.Equal(users, result);
    }

    [Fact]
    public async Task GetOptedOutUsersAsync_WhenRootItemIdInvalid_ReturnsEmpty()
    {
        var result = await _sut.GetOptedOutUsersAsync(0);

        Assert.Empty(result);
        _replyServiceMock.Verify(s => s.GetOptedOutUsersAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ToggleConversationOptOutAsync_WhenUserEmptyOrRootIdInvalid_ReturnsFalse()
    {
        Assert.False(await _sut.ToggleConversationOptOutAsync(10, ""));
        Assert.False(await _sut.ToggleConversationOptOutAsync(0, CurrentUserId));
        _replyServiceMock.Verify(s => s.ToggleConversationOptOutAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleConversationOptOutAsync_WhenOptedOut_ShowsInfoSnackbar()
    {
        _replyServiceMock.Setup(s => s.ToggleConversationOptOutAsync(10, CurrentUserId)).ReturnsAsync(true);

        bool result = await _sut.ToggleConversationOptOutAsync(10, CurrentUserId);

        Assert.True(result);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(m => m.Contains("抜けました")), Severity.Info, null, null), Times.Once);
    }

    [Fact]
    public async Task ToggleConversationOptOutAsync_WhenOptedIn_ShowsSuccessSnackbar()
    {
        _replyServiceMock.Setup(s => s.ToggleConversationOptOutAsync(10, CurrentUserId)).ReturnsAsync(false);

        bool result = await _sut.ToggleConversationOptOutAsync(10, CurrentUserId);

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(m => m.Contains("戻りました")), Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_WhenUserEmpty_ReturnsFalseAndShowsError()
    {
        Assert.False(await _sut.DeleteItemAsync(10, "", "owner"));
        _cardDataMock.Verify(d => d.DeleteItemAsync(It.IsAny<int>()), Times.Never);
        _snackbarMock.Verify(s => s.Add(ErrorMessages.NotAuthorizedToDelete, Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_WhenNotOwner_ReturnsFalseAndShowsError()
    {
        Assert.False(await _sut.DeleteItemAsync(10, CurrentUserId, "other-user"));
        _cardDataMock.Verify(d => d.DeleteItemAsync(It.IsAny<int>()), Times.Never);
        _snackbarMock.Verify(s => s.Add(ErrorMessages.NotAuthorizedToDelete, Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_WhenOwner_CallsServiceAndShowsSuccess()
    {
        bool result = await _sut.DeleteItemAsync(10, CurrentUserId, CurrentUserId);

        Assert.True(result);
        _cardDataMock.Verify(d => d.DeleteItemAsync(10), Times.Once);
        _snackbarMock.Verify(s => s.Add("アイテムを削除しました。", Severity.Success, null, null), Times.Once);
    }
}