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

using ItemEntity = SRNSMudApp.Data.Item;
using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Bounty;

/// <summary>
///     <see cref="FulfillBountyDialog"/> の UI インタラクションテスト。
///     タグオーナー時の無償完了、一般ユーザー時のアセット消費完了、およびアセット未所持時の警告・ボタン非活性を検証する。
/// </summary>
public sealed class FulfillBountyDialogTests : IAsyncDisposable
{
    private const string TagOwnerId = "tag-owner-user";
    private const string HolderUserId = "holder-user";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly Mock<AuthenticationStateProvider> _authMock = new();

    public FulfillBountyDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _contractServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddScoped(_ => _authMock.Object);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task WhenUserIsTagOwner_ShowsOwnerAlert_AndSubmitsWithoutConsumingAsset()
    {
        SetAuthenticatedUser(TagOwnerId);

        var bounty = CreateBounty(100, TagOwnerId, offeredRewardAssetId: 0);
        _ = _contractServiceMock.Setup(s => s.AcceptContractAsync(100, TagOwnerId, null))
            .ReturnsAsync(new Success<string>("Completed"));

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<FulfillBountyDialog>
        {
            { x => x.Bounty, bounty }
        };

        IDialogReference dialog = await dialogService.ShowAsync<FulfillBountyDialog>("バウンティの完了", parameters);

        host.WaitForState(() => host.Markup.Contains("アセットを消費せずに依頼を完了できます。"));
        Assert.Contains("あなたはタグ「TargetTag」のオーナーです。", host.Markup);

        IElement submitBtn = host.FindAll("button").First(b => b.TextContent.Contains("依頼を完了"));
        Assert.False(submitBtn.HasAttribute("disabled"));
        submitBtn.Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _contractServiceMock.Verify(s => s.AcceptContractAsync(100, TagOwnerId, null), Times.Once);
        _snackbarMock.Verify(s => s.Add(
            "依頼を完了しました！",
            Severity.Success,
            It.IsAny<Action<SnackbarOptions>?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task WhenUserHasNoValidAssets_ShowsErrorAlert_AndDisablesSubmitButton()
    {
        SetAuthenticatedUser(HolderUserId);

        var bounty = CreateBounty(101, TagOwnerId, offeredRewardAssetId: 0);
        _ = _contractDataMock.Setup(d => d.GetValidRightAssetsAsync(HolderUserId, bounty.RequestedTagId, It.IsAny<int?>()))
            .ReturnsAsync([]);

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<FulfillBountyDialog>
        {
            { x => x.Bounty, bounty }
        };

        _ = await dialogService.ShowAsync<FulfillBountyDialog>("バウンティの完了", parameters);

        host.WaitForState(() => host.Markup.Contains("権利（RightAsset）を所持していません。"));

        IElement submitBtn = host.FindAll("button").First(b => b.TextContent.Contains("依頼を完了"));
        Assert.True(submitBtn.HasAttribute("disabled"));
    }

    [Fact]
    public async Task WhenUserHasValidAsset_AutoSelectsSingleAsset_AndSubmitsWithAssetId()
    {
        SetAuthenticatedUser(HolderUserId);

        var bounty = CreateBounty(102, TagOwnerId, offeredRewardAssetId: 500);
        var rewardAsset = new RightAsset
        {
            Id = 500,
            OwnerId = "requester",
            TargetTagId = 99,
            TargetTag = new TagEntity { Id = 99, Name = "BonusTag", OwnerId = "requester" },
            Amount = 1
        };
        var myAsset = new RightAsset
        {
            Id = 301,
            OwnerId = HolderUserId,
            TargetTagId = bounty.RequestedTagId,
            TargetTag = bounty.RequestedTag,
            Amount = 2
        };

        _ = _contractDataMock.Setup(d => d.GetRightAssetByIdAsync(500))
            .ReturnsAsync(rewardAsset);
        _ = _contractDataMock.Setup(d => d.GetValidRightAssetsAsync(HolderUserId, bounty.RequestedTagId, It.IsAny<int?>()))
            .ReturnsAsync([myAsset]);
        _ = _contractServiceMock.Setup(s => s.AcceptContractAsync(102, HolderUserId, 301))
            .ReturnsAsync(new Success<string>("Completed"));

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<FulfillBountyDialog>
        {
            { x => x.Bounty, bounty }
        };

        IDialogReference dialog = await dialogService.ShowAsync<FulfillBountyDialog>("バウンティの完了", parameters);

        host.WaitForState(() => host.Markup.Contains("BonusTag の権利"));

        IRenderedComponent<MudForm> form = host.FindComponents<MudForm>()[0];
        await host.InvokeAsync(() => form.Instance.ValidateAsync());

        IElement submitBtn = host.FindAll("button").First(b => b.TextContent.Contains("依頼を完了"));
        submitBtn.Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _contractServiceMock.Verify(s => s.AcceptContractAsync(102, HolderUserId, 301), Times.Once);
    }

    private void SetAuthenticatedUser(string userId)
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId)];
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")));
        _ = _authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
    }

    private static TaggingRequestEntity CreateBounty(int id, string tagOwnerId, int offeredRewardAssetId)
    {
        var tag = new TagEntity { Id = 10, Name = "TargetTag", OwnerId = tagOwnerId };
        var item = new ItemEntity { Id = 20, Content = "TargetItemContent", OwnerId = "requester" };
        return new TaggingRequestEntity
        {
            Id = id,
            ContractType = "Bounty",
            OwnerId = "requester",
            RequesterUserId = "requester",
            TagOwnerUserId = tagOwnerId,
            RequestedTagId = tag.Id,
            RequestedTag = tag,
            TargetItemId = item.Id,
            TargetItem = item,
            Payload = new BountyPayload(offeredRewardAssetId)
        };
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