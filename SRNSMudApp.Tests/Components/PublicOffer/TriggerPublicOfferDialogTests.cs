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

public sealed class TriggerPublicOfferDialogTests : IAsyncLifetime
{
    private const string AliceUserId = "alice-id";
    private const string CharlieUserId = "charlie-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ICommandHandler<CreateTriggerContractCommand, Result<bool>>> _createTriggerContractHandlerMock = new();

    private readonly Mock<ISnackbar> _snackbarMock = new();

    public TriggerPublicOfferDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _createTriggerContractHandlerMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();

        var authState = CreateAuthState(CharlieUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ctx.Services.AddScoped(_ => authMock.Object);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Trigger_FreeOffer_ExecutesCreateTriggerContractCommand()
    {
        var aliceTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "AlicePublicTag", OwnerId = AliceUserId };
        var charlieItem = new SRNSMudApp.Data.Item { Id = 10, Content = "Charlie's own item", OwnerId = CharlieUserId };

        var offer = new PublicTradeOffer
        {
            Id = 50,
            OwnerId = AliceUserId,
            OfferedTagId = aliceTag.Id,
            OfferedTag = aliceTag,
            RequiredAssetAmount = 0,
            IsActive = true
        };

        _ = _contractDataMock.Setup(d => d.SearchItemsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([charlieItem]);
        _ = _createTriggerContractHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreateTriggerContractCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateTriggerContractCommand, CancellationToken>((c, _) => c.TriggerContract.Id = 123)
            .ReturnsAsync(Result.Ok());

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();

        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(TriggerPublicOfferDialog.Offer), offer } };
        IDialogReference dialog = await dialogService.ShowAsync<TriggerPublicOfferDialog>("オファーに応じる", parameters);

        host.WaitForState(() => host.Markup.Contains("タグを付与する対象のアイテム"));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Item>> autocomplete =
            host.FindComponents<MudAutocomplete<SRNSMudApp.Data.Item>>()[0];
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged!.InvokeAsync(charlieItem));

        IRenderedComponent<MudForm> form = host.FindComponents<MudForm>()[0];
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement triggerButton =
            host.FindAll("button").First(b => b.TextContent.Contains("実行する"));
        triggerButton.Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result!.Canceled);
        var contractId = Assert.IsType<int>(result.Data);
        Assert.Equal(123, contractId);
        _createTriggerContractHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CreateTriggerContractCommand>(c =>
                c.TriggerContract.ContractType == "Trigger" &&
                c.TriggerContract.RequesterUserId == CharlieUserId &&
                c.TriggerContract.TagOwnerUserId == AliceUserId &&
                c.TriggerContract.TargetItemId == charlieItem.Id &&
                c.TriggerContract.RequestedTagId == aliceTag.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaidOffer_WithoutValidAssets_DisplaysWarningAndDisablesSubmit()
    {
        var aliceTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "AlicePaidTag", OwnerId = AliceUserId };
        var offer = new PublicTradeOffer
        {
            Id = 51,
            OwnerId = AliceUserId,
            OfferedTagId = aliceTag.Id,
            OfferedTag = aliceTag,
            RequiredAssetAmount = 3,
            IsActive = true
        };

        _ = _contractDataMock
            .Setup(d => d.GetValidRightAssetsAsync(CharlieUserId, aliceTag.Id, 3))
            .ReturnsAsync([]);

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(TriggerPublicOfferDialog.Offer), offer } };
        _ = await dialogService.ShowAsync<TriggerPublicOfferDialog>("オファーに応じる", parameters);

        host.WaitForState(() => host.Markup.Contains("条件を満たすアセットを所有していません。"));

        Assert.Contains("条件を満たすアセットを所有していません。", host.Markup);
        IElement triggerButton = host.FindAll("button").First(b => b.TextContent.Contains("実行する"));
        Assert.True(triggerButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task PaidOffer_WithSelectedAsset_PassesConsumedRightAssetId()
    {
        var aliceTag = new SRNSMudApp.Data.Tag { Id = 1, Name = "AlicePaidTag", OwnerId = AliceUserId };
        var charlieItem = new SRNSMudApp.Data.Item { Id = 10, Content = "Charlie's item", OwnerId = CharlieUserId };
        var validAsset = new RightAsset
        {
            Id = 88,
            OwnerId = CharlieUserId,
            TargetTagId = aliceTag.Id,
            TargetTag = aliceTag,
            Amount = 5
        };

        var offer = new PublicTradeOffer
        {
            Id = 52,
            OwnerId = AliceUserId,
            OfferedTagId = aliceTag.Id,
            OfferedTag = aliceTag,
            RequiredAssetAmount = 2,
            IsActive = true
        };

        _ = _contractDataMock
            .Setup(d => d.GetValidRightAssetsAsync(CharlieUserId, aliceTag.Id, 2))
            .ReturnsAsync([validAsset]);
        _ = _contractDataMock
            .Setup(d => d.SearchItemsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([charlieItem]);
        _ = _createTriggerContractHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CreateTriggerContractCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTriggerContractCommand, CancellationToken>((c, _) => c.TriggerContract.Id = 456)
            .ReturnsAsync(Result.Ok());

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(TriggerPublicOfferDialog.Offer), offer } };
        IDialogReference dialog = await dialogService.ShowAsync<TriggerPublicOfferDialog>("オファーに応じる", parameters);

        host.WaitForState(() => host.Markup.Contains("消費するアセット（必須）"));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Item>> autocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Item>>();
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged!.InvokeAsync(charlieItem));

        IRenderedComponent<MudSelect<RightAsset>> select =
            host.FindComponent<MudSelect<RightAsset>>();
        await host.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(validAsset));

        IRenderedComponent<MudForm> form = host.FindComponent<MudForm>();
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement triggerButton = host.FindAll("button").First(b => b.TextContent.Contains("実行する"));
        await host.InvokeAsync(() => triggerButton.Click());

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result!.Canceled);
        _createTriggerContractHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CreateTriggerContractCommand>(c =>
                c.TriggerContract.ConsumedRightAssetId == 88 &&
                c.TriggerContract.TargetItemId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
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