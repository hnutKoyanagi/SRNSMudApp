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

using SRNSMudApp.Components.Bounty;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Commands;

using ItemEntity = SRNSMudApp.Data.Item;
using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Bounty;

/// <summary>
///     <see cref="BountyCreateDialog"/> の UI インタラクションテスト。
///     TargetItem 指定時・未指定時の描画、タグ・報酬アセット選択、送信時の CreateBountyCommand 実行とキャンセルを検証する。
/// </summary>
public sealed class BountyCreateDialogTests : IAsyncDisposable
{
    private const string RequesterUserId = "requester-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ICommandHandler<CreateBountyCommand, Result<bool>>> _createBountyHandlerMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public BountyCreateDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _createBountyHandlerMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();

        var authState = CreateAuthState(RequesterUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ = _ctx.Services.AddScoped(_ => authMock.Object);
        _ = _ctx.Render<MudPopoverProvider>();

        _ = _contractDataMock.Setup(d => d.GetAvailableRightAssetsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task WhenTargetItemProvided_RendersReadOnlyItemField()
    {
        var item = new ItemEntity { Id = 10, Content = "バウンティ対象アイテム", OwnerId = RequesterUserId };

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<BountyCreateDialog>
        {
            { x => x.TargetItem, item }
        };

        _ = await dialogService.ShowAsync<BountyCreateDialog>("バウンティの作成", parameters);

        host.WaitForState(() => host.Markup.Contains("バウンティ対象アイテム"));
        Assert.Contains("バウンティ対象アイテム", host.Markup);
        Assert.Empty(host.FindComponents<MudAutocomplete<ItemEntity>>());
    }

    [Fact]
    public async Task Submit_WithItemAndTag_ExecutesCreateBountyCommand_AndClosesDialog()
    {
        var item = new ItemEntity { Id = 10, Content = "バウンティ対象アイテム", OwnerId = RequesterUserId };
        var tag = new TagEntity { Id = 25, Name = "WantedTag", OwnerId = "tag-owner-id" };
        var rewardAsset = new RightAsset
        {
            Id = 77,
            OwnerId = RequesterUserId,
            TargetTagId = 30,
            TargetTag = new TagEntity { Id = 30, Name = "RewardTag", OwnerId = RequesterUserId },
            Amount = 1
        };

        _ = _contractDataMock.Setup(d => d.GetAvailableRightAssetsAsync(RequesterUserId))
            .ReturnsAsync([rewardAsset]);
        _ = _contractDataMock.Setup(d => d.SearchTagsByNameAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([tag]);
        _ = _createBountyHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreateBountyCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<bool>(true));

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<BountyCreateDialog>
        {
            { x => x.TargetItem, item }
        };

        IDialogReference dialog = await dialogService.ShowAsync<BountyCreateDialog>("バウンティの作成", parameters);

        host.WaitForState(() => host.Markup.Contains("希望するタグ"));

        IRenderedComponent<MudAutocomplete<TagEntity>> tagAutocomplete =
            host.FindComponents<MudAutocomplete<TagEntity>>()[0];
        await host.InvokeAsync(() => tagAutocomplete.Instance.ValueChanged.InvokeAsync(tag));

        IRenderedComponent<MudSelect<RightAsset>> rewardSelect =
            host.FindComponents<MudSelect<RightAsset>>()[0];
        await host.InvokeAsync(() => rewardSelect.Instance.ValueChanged.InvokeAsync(rewardAsset));

        IRenderedComponent<MudForm> form = host.FindComponents<MudForm>()[0];
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Contains("依頼を作成"));
        submitButton.Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal(true, result.Data);

        ContractPayload expectedPayload = new BountyPayload(rewardAsset.Id);
        _createBountyHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CreateBountyCommand>(c =>
                c.Bounty.TargetItemId == item.Id &&
                c.Bounty.RequestedTagId == tag.Id &&
                c.Bounty.RequesterUserId == RequesterUserId &&
                c.Bounty.Payload.Equals(expectedPayload)),
            It.IsAny<CancellationToken>()), Times.Once);

        _snackbarMock.Verify(s => s.Add(
            "タグ付けの依頼（バウンティ）を作成しました。",
            Severity.Success,
            It.IsAny<Action<SnackbarOptions>?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ClickCancel_CancelsDialog()
    {
        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        IDialogReference dialog = await dialogService.ShowAsync<BountyCreateDialog>("バウンティの作成");

        host.WaitForState(() => host.Markup.Contains("キャンセル"));

        host.FindAll("button").First(b => b.TextContent.Contains("キャンセル")).Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    private static AuthenticationState CreateAuthState(string userId)
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId)];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private sealed class AuthDialogHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<MudDialogProvider>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
