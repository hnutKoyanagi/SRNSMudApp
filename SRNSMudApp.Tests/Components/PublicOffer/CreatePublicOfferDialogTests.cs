using System.Security.Claims;

using AngleSharp.Dom;

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
using SRNSMudApp.Services.Commands;

namespace SRNSMudApp.Tests.Components.PublicOffer;

public sealed class CreatePublicOfferDialogTests : IAsyncLifetime
{
    private const string AliceUserId = "alice-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ICommandHandler<CreatePublicOfferCommand, Result<bool>>> _createPublicOfferHandlerMock = new();

    private readonly Mock<ISnackbar> _snackbarMock = new();

    public CreatePublicOfferDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _createPublicOfferHandlerMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();

        var authState = CreateAuthState(AliceUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ctx.Services.AddScoped(_ => authMock.Object);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Publish_WithOwnedTag_ExecutesCreatePublicOfferCommand()
    {
        var tag = new SRNSMudApp.Data.Tag { Id = 10, Name = "AliceTag", Content = "Alice's Tag", OwnerId = AliceUserId };

        _ = _contractDataMock.Setup(d => d.SearchMyTagsAsync(AliceUserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([tag]);
        _ = _createPublicOfferHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreatePublicOfferCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();

        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        IDialogReference dialog = await dialogService.ShowAsync<CreatePublicOfferDialog>("公開オファーの作成");

        host.WaitForState(() => host.Markup.Contains("提供するタグ"));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Tag>> autocomplete =
            host.FindComponents<MudAutocomplete<SRNSMudApp.Data.Tag>>()[0];
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged!.InvokeAsync(tag));

        IRenderedComponent<MudForm> form = host.FindComponents<MudForm>()[0];
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement publishButton =
            host.FindAll("button").First(b => b.TextContent.Contains("公開する"));
        publishButton.Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result!.Canceled);
        Assert.Equal(true, result.Data);
        _createPublicOfferHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CreatePublicOfferCommand>(c => c.Offer.OfferedTagId == tag.Id && c.Offer.OwnerId == AliceUserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _snackbarMock.Verify(s => s.Add("公開オファーを作成しました。", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WithRequiredAssetAmount_PassesAmountToHandler()
    {
        var tag = new SRNSMudApp.Data.Tag { Id = 22, Name = "PremiumTag", OwnerId = AliceUserId };

        _ = _contractDataMock.Setup(d => d.SearchMyTagsAsync(AliceUserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([tag]);
        _ = _createPublicOfferHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreatePublicOfferCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        IDialogReference dialog = await dialogService.ShowAsync<CreatePublicOfferDialog>("公開オファーの作成");

        host.WaitForState(() => host.Markup.Contains("提供するタグ"));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Tag>> autocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Tag>>();
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged!.InvokeAsync(tag));

        IRenderedComponent<MudNumericField<int>> numericField =
            host.FindComponent<MudNumericField<int>>();
        await host.InvokeAsync(() => numericField.Instance.ValueChanged.InvokeAsync(5));

        IRenderedComponent<MudForm> form = host.FindComponent<MudForm>();
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement publishButton =
            host.FindAll("button").First(b => b.TextContent.Contains("公開する"));
        await host.InvokeAsync(() => publishButton.Click());

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result!.Canceled);
        _createPublicOfferHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CreatePublicOfferCommand>(c =>
                c.Offer.OfferedTagId == 22 &&
                c.Offer.RequiredAssetAmount == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenHandlerFails_ShowsErrorSnackbarAndDoesNotClose()
    {
        var tag = new SRNSMudApp.Data.Tag { Id = 33, Name = "FailTag", OwnerId = AliceUserId };

        _ = _contractDataMock.Setup(d => d.SearchMyTagsAsync(AliceUserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([tag]);
        _ = _createPublicOfferHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreatePublicOfferCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<bool>("オファー作成エラー"));

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        _ = await dialogService.ShowAsync<CreatePublicOfferDialog>("公開オファーの作成");

        host.WaitForState(() => host.Markup.Contains("提供するタグ"));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Tag>> autocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Tag>>();
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged!.InvokeAsync(tag));

        IRenderedComponent<MudForm> form = host.FindComponent<MudForm>();
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement publishButton =
            host.FindAll("button").First(b => b.TextContent.Contains("公開する"));
        await host.InvokeAsync(() => publishButton.Click());

        _snackbarMock.Verify(s => s.Add("オファー作成エラー", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
        Assert.Contains("提供するタグ", host.Markup);
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
                b.OpenComponent<MudDialogProvider>(0);
                b.CloseComponent();
                b.AddContent(1, ChildContent);
            }));
            builder.CloseComponent();
        }
    }
}