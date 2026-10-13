// Components/UI/ItemCardPrivacyBadgeTests.cs
#region

using Bunit;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Tests.TestSupport;

#endregion

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
///     ItemCardContent および ItemCard におけるプライベートモードバッジ（フォロワー限定、グループ限定）の表示テスト。
/// </summary>
public sealed class ItemCardPrivacyBadgeTests : IAsyncLifetime
{
    private const string UserId = "test-user-id";
    private readonly BunitContext _ctx = new();

    public ItemCardPrivacyBadgeTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices().AddMockSrnsServices();

        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!,
            null!, null!, null!, null!);
        _ctx.Services.AddScoped(_ => userManagerMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void ItemCardContent_WhenNotPrivate_DoesNotRenderPrivacyBadge()
    {
        IRenderedComponent<ItemCardContent> cut = _ctx.Render<ItemCardContent>(parameters => parameters
            .Add(p => p.ItemId, 1)
            .Add(p => p.Content, "Public Item Content")
            .Add(p => p.IsPrivate, false));

        Assert.DoesNotContain("フォロワー限定", cut.Markup);
        Assert.DoesNotContain("グループ:", cut.Markup);
    }

    [Fact]
    public void ItemCardContent_WhenPrivateFollowersOnly_RendersFollowersBadge()
    {
        IRenderedComponent<ItemCardContent> cut = _ctx.Render<ItemCardContent>(parameters => parameters
            .Add(p => p.ItemId, 2)
            .Add(p => p.Content, "Followers Only Content")
            .Add(p => p.IsPrivate, true)
            .Add(p => p.TargetUserGroupName, (string?)null));

        Assert.Contains("フォロワー限定", cut.Markup);
    }

    [Fact]
    public void ItemCardContent_WhenPrivateGroupOnly_RendersGroupBadge()
    {
        IRenderedComponent<ItemCardContent> cut = _ctx.Render<ItemCardContent>(parameters => parameters
            .Add(p => p.ItemId, 3)
            .Add(p => p.Content, "Group Only Content")
            .Add(p => p.IsPrivate, true)
            .Add(p => p.TargetUserGroupId, 7)
            .Add(p => p.TargetUserGroupName, "開発チーム"));

        Assert.Contains("グループ: 開発チーム", cut.Markup);
        Assert.Contains("href=\"/UserGroup/Members/7\"", cut.Markup);
    }

    [Fact]
    public void ItemCardContent_WhenPrivateGroupOnlyWithoutGroupId_FallsBackToGroupList()
    {
        IRenderedComponent<ItemCardContent> cut = _ctx.Render<ItemCardContent>(parameters => parameters
            .Add(p => p.ItemId, 3)
            .Add(p => p.Content, "Group Only Content")
            .Add(p => p.IsPrivate, true)
            .Add(p => p.TargetUserGroupName, "開発チーム"));

        Assert.Contains("グループ: 開発チーム", cut.Markup);
        Assert.Contains("href=\"/UserGroup/List\"", cut.Markup);
    }

    [Fact]
    public void ItemCardContent_WhenAdminHidden_RendersAdminHiddenBadge()
    {
        IRenderedComponent<ItemCardContent> cut = _ctx.Render<ItemCardContent>(parameters => parameters
            .Add(p => p.ItemId, 4)
            .Add(p => p.Content, "Hidden Content")
            .Add(p => p.IsAdminHidden, true));

        Assert.Contains("管理者非公開", cut.Markup);
    }

    [Fact]
    public void PrivacyBadgeStrategyResolver_ReturnsNoBadgeForPublicItem()
    {
        Assert.Null(PrivacyBadgeStrategyResolver.Resolve(false, null));
    }

    [Fact]
    public void PrivacyBadgeStrategyResolver_SelectsBadgeStrategyByContext()
    {
        PrivacyBadgeModel? followersBadge = PrivacyBadgeStrategyResolver.Resolve(true, null);
        PrivacyBadgeModel? groupBadgeWithoutId = PrivacyBadgeStrategyResolver.Resolve(true, "開発チーム");
        PrivacyBadgeModel? groupBadgeWithId = PrivacyBadgeStrategyResolver.Resolve(true, "開発チーム", targetUserGroupId: 12);

        Assert.Equal("フォロワー限定", followersBadge?.Text);
        Assert.Null(followersBadge?.Href);
        Assert.Equal("グループ: 開発チーム", groupBadgeWithoutId?.Text);
        Assert.Equal("/UserGroup/List", groupBadgeWithoutId?.Href);
        Assert.Equal("グループ: 開発チーム", groupBadgeWithId?.Text);
        Assert.Equal("/UserGroup/Members/12", groupBadgeWithId?.Href);
    }

    [Fact]
    public void TagCard_WhenPrivateGroupOnly_RendersGroupBadgeWithNavigation()
    {
        _ctx.Services.AddScoped<TagCardViewModel>();
        var tag = new SRNSMudApp.Data.Tag
        {
            Id = 10,
            Name = "機密タグ",
            OwnerId = UserId,
            IsPrivate = true,
            TargetUserGroupId = 5,
            TargetUserGroup = new SRNSMudApp.Data.UserGroup { Id = 5, Name = "コア開発チーム", OwnerId = UserId }
        };

        IRenderedComponent<TagCard> cut = _ctx.Render<TagCard>(parameters => parameters
            .Add(p => p.Tag, tag));

        Assert.Contains("グループ: コア開発チーム", cut.Markup);
        Assert.Contains("href=\"/UserGroup/Members/5\"", cut.Markup);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}