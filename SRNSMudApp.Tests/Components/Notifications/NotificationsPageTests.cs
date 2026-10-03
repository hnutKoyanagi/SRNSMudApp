using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Pages;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Components.Notifications;

public sealed class NotificationsPageTests : IAsyncLifetime
{
    private const string OwnerUserId = "notif-owner-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<INotificationService> _notifServiceMock = new();
    private readonly Mock<INotificationsDataProvider> _notifDataMock = new();
    private readonly Mock<ITaggingRequestActions> _actionsMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Mock<IRightAssetDataProvider> _rightAssetDataMock = new();
    private readonly Mock<IHomeDataProvider> _homeDataMock = new();

    public NotificationsPageTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _notifServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _notifDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _actionsMock.Object);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);
        _ctx.Services.AddScoped(_ => _contractServiceMock.Object);
        _ctx.Services.AddScoped(_ => _rightAssetDataMock.Object);
        _ctx.Services.AddScoped(_ => _homeDataMock.Object);

        _notifDataMock.Setup(d => d.GetAssociatedItemsAsync(It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync([]);

        _ctx.Services.AddAuthorizationCore();

        var authState = CreateAuthState(OwnerUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ctx.Services.AddScoped(_ => authMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void ApproveAndReject_Requests_CallsActionsAndInvokesCallback()
    {
        var targetTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "NotifTag", OwnerId = OwnerUserId };
        var targetItem = new SRNSMudApp.Data.Item { Id = 10, Content = "Target Item", OwnerId = OwnerUserId };

        var note1 = new NotificationDto
        {
            SourceId = 100,
            ActorName = "notif_requester",
            Message = "タグ追加リクエスト",
            CreatedAt = DateTimeOffset.UtcNow,
            IsRead = false,
            Kind = new TagRequestNotification(100, TaggingRequestType.Add, 10, "NotifTag", 1, 1, TradeStatus.Proposed),
            TargetUrl = new RelativeUrl("/item-detail/10"),
            AssociatedItem = targetItem
        };

        var note2 = new NotificationDto
        {
            SourceId = 101,
            ActorName = "notif_requester",
            Message = "タグ追加リクエスト",
            CreatedAt = DateTimeOffset.UtcNow,
            IsRead = false,
            Kind = new TagRequestNotification(101, TaggingRequestType.Add, 10, "NotifTag", 1, 1, TradeStatus.Proposed),
            TargetUrl = new RelativeUrl("/item-detail/10"),
            AssociatedItem = targetItem
        };

        _ = _notifServiceMock.Setup(s => s.GetUserNotificationsAsync(OwnerUserId))
            .ReturnsAsync([note1, note2]);

        _ = _notifDataMock.Setup(d => d.GetAssociatedItemsAsync(It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync([targetItem]);

        _ = _actionsMock.Setup(a => a.ApproveAsync(note1.SourceId, OwnerUserId))
            .ReturnsAsync(true);

        var dialogReferenceMock = new Mock<IDialogReference>();
        _ = dialogReferenceMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok("Rejecting reason"));
        _ = _dialogLauncherMock.Setup(l => l.ShowAsync(
            typeof(RejectRequestDialog),
            "リクエストを却下",
            It.IsAny<DialogParameters?>(),
            It.IsAny<DialogOptions?>()))
            .ReturnsAsync(dialogReferenceMock.Object);

        _ = _contractServiceMock.Setup(s => s.CancelContractAsync(note2.SourceId, OwnerUserId))
            .ReturnsAsync(new Success<string>("却下完了"));

        RenderFragment page = builder =>
        {
            builder.OpenComponent<NotificationsPage>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthHost> host =
            _ctx.Render<AuthHost>(parameters => parameters.Add(p => p.ChildContent, page));

        host.WaitForState(() => host.Markup.Contains("タグ追加リクエスト"));

        Assert.Contains("タグ追加リクエスト", host.Markup);
        Assert.Equal(2, host.FindAll("button[title='リクエストを承認する']").Count);

        // 初期化時に未読通知の一括既読化が呼び出されることを検証
        _notifServiceMock.Verify(s => s.MarkAllAsReadAsync(OwnerUserId), Times.Once);

        // Act 1: 1件目を承認
        host.FindAll("button[title='リクエストを承認する']")[0].Click();

        _actionsMock.Verify(a => a.ApproveAsync(note1.SourceId, OwnerUserId), Times.Once);

        // Act 2: 2件目を却下
        host.FindAll("button[title='リクエストを却下する']")[^1].Click();

        _contractServiceMock.Verify(s => s.CancelContractAsync(note2.SourceId, OwnerUserId), Times.Once);
    }

    [Fact]
    public void TagPermissionRequest_DisplaysApproveAndRejectButtons()
    {
        var permNote = new NotificationDto
        {
            SourceId = 200,
            ActorName = "requester_bob",
            Message = "タグ操作権限リクエスト",
            CreatedAt = DateTimeOffset.UtcNow,
            IsRead = false,
            Kind = new TagPermissionRequestNotification(
                ItemId: 200,
                RequestedTagId: 10,
                RequestedTagName: "TargetTag",
                RequestedAmount: 5,
                OfferedRightAssetId: null,
                OfferedTagName: null,
                OfferedAmount: 0,
                RequesterName: "requester_bob",
                Message: "子タグを作成したいです",
                Status: TradeStatus.Proposed),
            TargetUrl = new RelativeUrl("/TagDetail/10"),
            AssociatedItemId = 200
        };

        _ = _notifServiceMock.Setup(s => s.GetUserNotificationsAsync(OwnerUserId))
            .ReturnsAsync([permNote]);

        _ = _rightAssetDataMock.Setup(d => d.ApprovePermissionRequestAsync(200, OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<bool>(true));

        RenderFragment page = builder =>
        {
            builder.OpenComponent<NotificationsPage>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthHost> host =
            _ctx.Render<AuthHost>(parameters => parameters.Add(p => p.ChildContent, page));

        host.WaitForState(() => host.Markup.Contains("TargetTag"));

        Assert.Contains("TargetTag", host.Markup);
        Assert.Contains("要求数量: 5", host.Markup);
        Assert.Contains("無償リクエスト", host.Markup);
        Assert.Contains("子タグを作成したいです", host.Markup);

        // タグ名リンクが /TagDetail/10 に遷移可能であること
        var tagLink = host.Find("[data-testid='permission-request-tag-link']");
        Assert.NotNull(tagLink);
        Assert.Equal("/TagDetail/10", tagLink.GetAttribute("href"));

        // 承認ボタンと却下ボタンが表示されていること
        var approveButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Trim().Contains("承認", StringComparison.Ordinal));
        var rejectButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Trim().Contains("却下", StringComparison.Ordinal));
        Assert.NotNull(approveButton);
        Assert.NotNull(rejectButton);

        // 承認ボタンクリック
        approveButton.Click();

        _rightAssetDataMock.Verify(d => d.ApprovePermissionRequestAsync(200, OwnerUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TagPermissionRequest_WhenRequestedTagIdIsZero_DoesNotResolveFromAllTagsAndRendersPlainText()
    {
        var targetTag = new SRNSMudApp.Data.Tag { Id = 42, Name = "ResolvedTag", OwnerId = OwnerUserId };
        _homeDataMock.Setup(h => h.GetTagsAndRelationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(([targetTag], []));

        var permNote = new NotificationDto
        {
            SourceId = 201,
            ActorName = "requester_bob",
            Message = "タグ操作権限リクエスト",
            CreatedAt = DateTimeOffset.UtcNow,
            IsRead = false,
            Kind = new TagPermissionRequestNotification(
                ItemId: 201,
                RequestedTagId: 0,
                RequestedTagName: "ResolvedTag",
                RequestedAmount: 3,
                OfferedRightAssetId: null,
                OfferedTagName: null,
                OfferedAmount: 0,
                RequesterName: "requester_bob",
                Message: null,
                Status: TradeStatus.Proposed),
            TargetUrl = new RelativeUrl("/notifications"),
            AssociatedItemId = 0
        };

        _ = _notifServiceMock.Setup(s => s.GetUserNotificationsAsync(OwnerUserId))
            .ReturnsAsync([permNote]);

        RenderFragment page = builder =>
        {
            builder.OpenComponent<NotificationsPage>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthHost> host =
            _ctx.Render<AuthHost>(parameters => parameters.Add(p => p.ChildContent, page));

        host.WaitForState(() => host.Markup.Contains("ResolvedTag"));

        // AllTags からのすり替えが行われず、プレーンテキストとして表示されること
        var tagText = host.Find("[data-testid='permission-request-tag-text']");
        Assert.NotNull(tagText);
        Assert.Equal("ResolvedTag", tagText.TextContent.Trim());
        Assert.Empty(host.FindAll("[data-testid='permission-request-tag-link']"));
    }

    private static AuthenticationState CreateAuthState(string userId)
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId)];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private sealed class AuthHost : ComponentBase
    {
        [Parameter] public RenderFragment ChildContent { get; set; } = _ => { };

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<MudSnackbarProvider>(0);
                b.CloseComponent();
                b.OpenComponent<MudDialogProvider>(1);
                b.CloseComponent();
                b.AddContent(2, ChildContent);
            }));
            builder.CloseComponent();
        }
    }
}