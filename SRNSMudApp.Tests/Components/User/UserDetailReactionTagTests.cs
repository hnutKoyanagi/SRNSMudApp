// Components/User/UserDetailReactionTagTests.cs
#region

using System.Security.Claims;

using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.User;

using Tag = SRNSMudApp.Data.Tag;

/// <summary>
///     <see cref="UserDetail" /> コンポーネントにおける「Reactionタグ」タブの UI 単体テスト。
/// </summary>
public sealed class UserDetailReactionTagTests : IAsyncLifetime
{
    private const string ReactionTabText = "Reactionタグ";
    private const string ViewerUserId = "viewer-user-id";
    private const string TargetUserId = "target-user-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IUserDataProvider> _userDataMock = new();

    public UserDetailReactionTagTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _userDataMock.Object);

        Bunit.TestDoubles.BunitAuthorizationContext authorization = _ctx.AddAuthorization();
        authorization.SetAuthorized("testviewer");
        authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, ViewerUserId));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void ReactionTab_ShouldRenderTabHeader()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser", Email = "target@example.com" };
        var pageData = new UserDetailPageData(targetUser, [], []);

        _ = _userDataMock.Setup(d => d.GetUserDetailAsync(TargetUserId, ViewerUserId))
            .ReturnsAsync(pageData);

        IRenderedComponent<UserDetail> component =
            _ctx.Render<UserDetail>(parameters => parameters.Add(p => p.UserId, TargetUserId));

        component.WaitForState(() => !component.Markup.Contains("mud-progress-circular"));

        IElement? tab = component.FindAll("*").FirstOrDefault(e => e.TextContent.Trim() == ReactionTabText);
        Assert.NotNull(tab);
    }

    [Fact]
    public async Task ReactionTab_WhenReactionTagsExist_ShouldRenderReactionCards()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser", Email = "target@example.com" };
        var shinjiTag = new Tag { Id = 101, Name = "真実", OwnerId = TargetUserId, CachedWeight = 7 };
        var zenTag = new Tag { Id = 102, Name = "善", OwnerId = TargetUserId, CachedWeight = 3 };
        var biTag = new Tag { Id = 103, Name = "美", OwnerId = TargetUserId, CachedWeight = 12 };

        var pageData = new UserDetailPageData(targetUser, [shinjiTag, zenTag, biTag], []);

        _ = _userDataMock.Setup(d => d.GetUserDetailAsync(TargetUserId, ViewerUserId))
            .ReturnsAsync(pageData);

        IRenderedComponent<UserDetail> component =
            _ctx.Render<UserDetail>(parameters => parameters.Add(p => p.UserId, TargetUserId));

        component.WaitForState(() => !component.Markup.Contains("mud-progress-circular"));

        // Reactionタグ タブをクリックしてアクティブにする
        await component.InvokeAsync(() =>
        {
            IElement? tabLabel = component.FindAll("*").LastOrDefault(e => e.TextContent.Trim() == ReactionTabText);
            Assert.NotNull(tabLabel);
            tabLabel!.Click();
        });

        // 各リアクションタグ名とスコア、リンクが表示されていることを検証
        Assert.Contains("真実", component.Markup);
        Assert.Contains("善", component.Markup);
        Assert.Contains("美", component.Markup);
        Assert.Contains("7", component.Markup);
        Assert.Contains("3", component.Markup);
        Assert.Contains("12", component.Markup);
        Assert.Contains("/TagDetail/101", component.Markup);
        Assert.Contains("/TagDetail/102", component.Markup);
        Assert.Contains("/TagDetail/103", component.Markup);
    }

    [Fact]
    public async Task ReactionTab_WhenNoReactionTags_ShouldRenderEmptyMessage()
    {
        var targetUser = new ApplicationUser { Id = TargetUserId, UserName = "TargetUser", Email = "target@example.com" };
        var pageData = new UserDetailPageData(targetUser, [], []);

        _ = _userDataMock.Setup(d => d.GetUserDetailAsync(TargetUserId, ViewerUserId))
            .ReturnsAsync(pageData);

        IRenderedComponent<UserDetail> component =
            _ctx.Render<UserDetail>(parameters => parameters.Add(p => p.UserId, TargetUserId));

        component.WaitForState(() => !component.Markup.Contains("mud-progress-circular"));

        // Reactionタグ タブをクリックしてアクティブにする
        await component.InvokeAsync(() =>
        {
            IElement? tabLabel = component.FindAll("*").LastOrDefault(e => e.TextContent.Trim() == ReactionTabText);
            Assert.NotNull(tabLabel);
            tabLabel!.Click();
        });

        Assert.Contains("Reactionタグはありません。", component.Markup);
    }

    [Fact]
    public async Task ReactionTab_WhenUserIdNotSpecified_DefaultsToCurrentLoggedInUser()
    {
        var viewerUser = new ApplicationUser { Id = ViewerUserId, UserName = "ViewerUser", Email = "viewer@example.com" };
        var shinjiTag = new Tag { Id = 201, Name = "真実", OwnerId = ViewerUserId, CachedWeight = 99 };

        var pageData = new UserDetailPageData(viewerUser, [shinjiTag], []);

        _ = _userDataMock.Setup(d => d.GetUserDetailAsync(ViewerUserId, ViewerUserId))
            .ReturnsAsync(pageData);

        // UserId パラメータを渡さずにレンダリング（/User/UserDetail/ 相当）
        IRenderedComponent<UserDetail> component =
            _ctx.Render<UserDetail>(parameters => parameters.Add(p => p.UserId, string.Empty));

        component.WaitForState(() => !component.Markup.Contains("mud-progress-circular"));

        // ログインユーザー名が表示されていること
        Assert.Contains("ViewerUser", component.Markup);

        // Reactionタグ タブをクリックしてアクティブにする
        await component.InvokeAsync(() =>
        {
            IElement? tabLabel = component.FindAll("*").LastOrDefault(e => e.TextContent.Trim() == ReactionTabText);
            Assert.NotNull(tabLabel);
            tabLabel!.Click();
        });

        // 自分の ReactionTag が見えること
        Assert.Contains("真実", component.Markup);
        Assert.Contains("99", component.Markup);
        Assert.Contains("/TagDetail/201", component.Markup);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}