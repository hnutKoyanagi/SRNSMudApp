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
using SRNSMudApp.Components.Tag;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Pages;

public sealed class PageRenderSmokeTests : IAsyncLifetime
{
    private const string UserId = "smoke-user-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IHomeDataProvider> _homeDataMock = new();
    private readonly Mock<IItemListDataProvider> _itemListDataMock = new();
    private readonly Mock<ITagTreeDataProvider> _treeDataMock = new();

    public PageRenderSmokeTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _homeDataMock.Object);

        _itemListDataMock
            .Setup(d => d.LoadItemsAndTagsAsync(It.IsAny<List<ItemListFilter>>(), It.IsAny<List<ItemListSort>>(), It.IsAny<string>()))
            .ReturnsAsync(new ItemListPageData([], []));
        _itemListDataMock
            .Setup(d => d.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, SRNSMudApp.Data.Tag>());
        _itemListDataMock
            .Setup(d => d.GetTagsByNamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, SRNSMudApp.Data.Tag>());
        _ = _ctx.Services.AddScoped(_ => _itemListDataMock.Object);

        _treeDataMock
            .Setup(d => d.LoadTagsAsync())
            .ReturnsAsync([]);
        _ = _ctx.Services.AddScoped(_ => _treeDataMock.Object);

        Bunit.TestDoubles.BunitAuthorizationContext authorization = _ctx.AddAuthorization();
        authorization.SetAuthorized("smoke_user");
        authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId), new Claim(ClaimTypes.Name, "smoke_user"));

        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Home_Renders_WithoutException()
    {
        _ = _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(UserId))
            .ReturnsAsync([]);
        _ = _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync())
            .ReturnsAsync(([], []));
        _ = _homeDataMock.Setup(d => d.EnsureSystemTagsAsync(UserId))
            .ReturnsAsync(new SystemTagsResult(GoodTagId: 1, BadTagId: 2, Created: false));
        _ = _homeDataMock.Setup(d => d.LoadTimelineAsync(It.IsAny<System.Collections.Generic.IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new HomeTimelinePage([], 0));

        RenderFragment home = builder =>
        {
            builder.OpenComponent<Home>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<AuthHost> host =
            _ctx.Render<AuthHost>(parameters => parameters.Add(p => p.ChildContent, home));

        host.WaitForState(() => host.Markup.Contains("タイムライン") ||
                                 host.Markup.Contains("まだタグをフォローしていません"));

        Assert.Contains("タイムライン", host.Markup);
    }

    [Fact]
    public void TagSearch_Renders_WithoutException()
    {
        IRenderedComponent<TagSearch> cut = _ctx.Render<TagSearch>();

        Assert.Contains("タグ検索", cut.Markup);
        Assert.Contains("タグを検索", cut.Markup);
    }

    [Fact]
    public void TagList_Renders_WithoutException()
    {
        IRenderedComponent<TagList> cut = _ctx.Render<TagList>();
        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public void TagTree_Renders_WithoutException()
    {
        IRenderedComponent<TagTree> cut = _ctx.Render<TagTree>();
        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public void ItemList_Renders_WithoutException()
    {
        IRenderedComponent<SRNSMudApp.Components.Item.ItemList> cut =
            _ctx.Render<SRNSMudApp.Components.Item.ItemList>();
        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public void UserSearch_Renders_WithoutException()
    {
        IRenderedComponent<SRNSMudApp.Components.User.UserSearch> cut =
            _ctx.Render<SRNSMudApp.Components.User.UserSearch>();
        Assert.NotNull(cut.Markup);
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
                b.AddContent(0, ChildContent);
            }));
            builder.CloseComponent();
        }
    }
}