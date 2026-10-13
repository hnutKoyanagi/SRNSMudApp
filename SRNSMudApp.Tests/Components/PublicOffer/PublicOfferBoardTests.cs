using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.PublicOffer;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Components.PublicOffer;

public sealed class PublicOfferBoardTests : IAsyncLifetime
{
    private const string AliceUserId = "alice-id";
    private const string CharlieUserId = "charlie-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IPublicOfferDataProvider> _contractDataMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public PublicOfferBoardTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);
        _ctx.Services.AddAuthorizationCore();

        var authState = CreateAuthState(CharlieUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ctx.Services.AddScoped(_ => authMock.Object);
        _ctx.Services.AddScoped(_ => _contractServiceMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Board_ShowsOnlyActiveOffers_WithFreeBadge()
    {
        var alice = new ApplicationUser { Id = AliceUserId, UserName = "Alice" };
        var aliceTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "AliceOfferTag", OwnerId = AliceUserId, Owner = alice };
        var activeOffer = new PublicTradeOffer
        {
            Id = 10,
            OwnerId = AliceUserId,
            Owner = alice,
            OfferedTagId = 1,
            OfferedTag = aliceTag,
            RequiredAssetAmount = 0,
            IsActive = true
        };

        _ = _contractDataMock.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync([activeOffer]);

        RenderFragment board = builder =>
        {
            builder.OpenComponent<PublicOfferBoard>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthDialogHost> host =
            _ctx.Render<AuthDialogHost>(parameters => parameters.Add(p => p.ChildContent, board));

        host.WaitForState(() => host.Markup.Contains(aliceTag.Name));

        Assert.Contains(aliceTag.Name, host.Markup);
        Assert.Contains("提供者: Alice", host.Markup);
        Assert.Contains("無償で提供", host.Markup);
        Assert.Contains("オファーに応じる", host.Markup);
    }

    [Fact]
    public void TriggerViaCard_CallsContractServiceAccept()
    {
        var alice = new ApplicationUser { Id = AliceUserId, UserName = "Alice" };
        var aliceTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "AliceOfferTag", OwnerId = AliceUserId, Owner = alice };
        var activeOffer = new PublicTradeOffer
        {
            Id = 10,
            OwnerId = AliceUserId,
            Owner = alice,
            OfferedTagId = 1,
            OfferedTag = aliceTag,
            RequiredAssetAmount = 0,
            IsActive = true
        };

        _ = _contractDataMock.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync([activeOffer]);

        var dialogReferenceMock = new Mock<IDialogReference>();
        _ = dialogReferenceMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok(999));

        _ = _dialogLauncherMock.Setup(l => l.ShowAsync(
            typeof(TriggerPublicOfferDialog),
            "オファーに応じる",
            It.IsAny<DialogParameters?>(),
            It.IsAny<DialogOptions?>()))
            .ReturnsAsync(dialogReferenceMock.Object);

        _ = _contractServiceMock.Setup(s => s.AcceptContractAsync(999, CharlieUserId))
            .ReturnsAsync(new Success<string>("Success"));

        RenderFragment board = builder =>
        {
            builder.OpenComponent<PublicOfferBoard>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthDialogHost> host =
            _ctx.Render<AuthDialogHost>(parameters => parameters.Add(p => p.ChildContent, board));

        host.WaitForState(() => host.Markup.Contains(aliceTag.Name));

        host.FindAll("button").First(b => b.TextContent.Contains("オファーに応じる")).Click();

        host.WaitForState(() => host.Markup.Contains("公開オファーを利用してタグを獲得しました！"),
            TimeSpan.FromSeconds(5));

        _contractServiceMock.Verify(s => s.AcceptContractAsync(999, CharlieUserId), Times.Once);
    }

    [Fact]
    public void Board_WhenNoOffers_ShowsEmptyAlert()
    {
        _ = _contractDataMock.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync([]);

        RenderFragment board = builder =>
        {
            builder.OpenComponent<PublicOfferBoard>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthDialogHost> host =
            _ctx.Render<AuthDialogHost>(parameters => parameters.Add(p => p.ChildContent, board));

        host.WaitForState(() => host.Markup.Contains("現在、アクティブな公開オファーはありません。"));
        Assert.Contains("現在、アクティブな公開オファーはありません。", host.Markup);
    }

    [Fact]
    public void ClickCreateOfferButton_OpensCreateDialog_AndReloadsDataOnSuccess()
    {
        _ = _contractDataMock.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync([]);

        var dialogReferenceMock = new Mock<IDialogReference>();
        _ = dialogReferenceMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok(true));

        _ = _dialogLauncherMock.Setup(l => l.ShowAsync(
            typeof(CreatePublicOfferDialog),
            "公開オファーの作成",
            It.IsAny<DialogParameters?>(),
            It.IsAny<DialogOptions?>()))
            .ReturnsAsync(dialogReferenceMock.Object);

        RenderFragment board = builder =>
        {
            builder.OpenComponent<PublicOfferBoard>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthDialogHost> host =
            _ctx.Render<AuthDialogHost>(parameters => parameters.Add(p => p.ChildContent, board));

        host.WaitForState(() => host.Markup.Contains("オファーを作成する"));
        host.FindAll("button").First(b => b.TextContent.Contains("オファーを作成する")).Click();

        _dialogLauncherMock.Verify(l => l.ShowAsync(
            typeof(CreatePublicOfferDialog),
            "公開オファーの作成",
            It.IsAny<DialogParameters?>(),
            It.IsAny<DialogOptions?>()), Times.Once);
        _contractDataMock.Verify(d => d.GetActivePublicOffersAsync(), Times.Exactly(2));
    }

    [Fact]
    public void Board_WhenViewerIsOwner_ShowsDeactivateButton_AndCallsDeactivateOnClick()
    {
        var charlie = new ApplicationUser { Id = CharlieUserId, UserName = "Charlie" };
        var charlieTag = new SRNSMudApp.Data.Tag { Id = 2, Name = "CharlieOfferTag", OwnerId = CharlieUserId, Owner = charlie };
        var ownOffer = new PublicTradeOffer
        {
            Id = 20,
            OwnerId = CharlieUserId,
            Owner = charlie,
            OfferedTagId = 2,
            OfferedTag = charlieTag,
            RequiredAssetAmount = 50,
            IsActive = true
        };

        _ = _contractDataMock.Setup(d => d.GetActivePublicOffersAsync()).ReturnsAsync([ownOffer]);
        _ = _contractDataMock.Setup(d => d.DeactivatePublicOfferAsync(20, CharlieUserId)).ReturnsAsync(true);

        RenderFragment board = builder =>
        {
            builder.OpenComponent<PublicOfferBoard>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthDialogHost> host =
            _ctx.Render<AuthDialogHost>(parameters => parameters.Add(p => p.ChildContent, board));

        host.WaitForState(() => host.Markup.Contains(charlieTag.Name));
        Assert.Contains("要求アセット量: 50", host.Markup);
        Assert.Contains("取り下げる", host.Markup);

        host.FindAll("button").First(b => b.TextContent.Contains("取り下げる")).Click();

        host.WaitForState(() => host.Markup.Contains("オファーを取り下げました。"), TimeSpan.FromSeconds(5));
        _contractDataMock.Verify(d => d.DeactivatePublicOfferAsync(20, CharlieUserId), Times.Once);
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

    private sealed class AuthDialogHost : ComponentBase
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