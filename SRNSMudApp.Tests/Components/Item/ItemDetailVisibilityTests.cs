#region

using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     ItemDetail コンポーネントにおけるユーザーグループ指定・プライベートモードの閲覧制限テスト。
///     プライベートモードでユーザーグループが指定されたアイテムに対して、
///     アイテムの Owner 以外のグループメンバーからアクセスした場合に正常に詳細が表示されること、
///     および非メンバーには非表示となることを検証する。
/// </summary>
public sealed class ItemDetailVisibilityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemDetailDataProvider> _itemDetailDataMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Bunit.TestDoubles.BunitAuthorizationContext _authorization;

    public ItemDetailVisibilityTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _itemDetailDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _contractServiceMock.Object);

        _authorization = _ctx.AddAuthorization();

        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!,
            null!, null!, null!, null!);
        _ctx.Services.AddScoped(_ => userManagerMock.Object);

        _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void ItemDetail_WhenAccessedByGroupMember_WhoIsNotItemOwner_RendersDetailSuccessfully()
    {
        // http://localhost:5009/ItemDetail/19009 に対して
        // プライベートモードのユーザーグループメンバーかつ Item の owner 以外からアクセス
        const int itemId = 19009;
        const string itemOwnerId = "item-author-1001";
        const string groupMemberId = "group-member-2002";
        const string groupMemberName = "group_member_user";
        const int targetGroupId = 501;

        NavigationManager navigationManager = _ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost:5009/ItemDetail/{itemId}");

        _authorization.SetAuthorized(groupMemberName);
        _authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, groupMemberId));

        var item = new SRNSMudApp.Data.Item
        {
            Id = itemId,
            Content = "Private user group restricted content",
            IsPrivate = true,
            TargetUserGroupId = targetGroupId,
            OwnerId = itemOwnerId, // Item の owner 以外
            Owner = new ApplicationUser { Id = itemOwnerId, UserName = "item_author" }
        };

        var pageData = new ItemDetailPageData(item, [], [], []);

        // プロバイダーがグループメンバーに対してデータを返す
        _ = _itemDetailDataMock.Setup(d => d.GetItemDetailAsync(itemId, groupMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageData);
        _ = _contractServiceMock.Setup(s => s.GetRequestsByItemIdAsync(itemId))
            .ReturnsAsync([]);

        IRenderedComponent<SRNSMudApp.Components.Item.ItemDetail> cut =
            _ctx.Render<SRNSMudApp.Components.Item.ItemDetail>(parameters => parameters.Add(p => p.ItemId, itemId));

        cut.WaitForState(() => !cut.Markup.Contains("mud-progress-circular"));

        // 閲覧可能であり、アイテム本文が描画されること（「アイテムが見つかりません。」ではない）
        Assert.Contains("Private user group restricted content", cut.Markup);
        Assert.DoesNotContain("アイテムが見つかりません。", cut.Markup);

        _itemDetailDataMock.Verify(d => d.GetItemDetailAsync(itemId, groupMemberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ItemDetail_WhenAccessedByNonMember_WhoIsNotItemOwner_DisplaysNotFoundMessage()
    {
        // http://localhost:5009/ItemDetail/19009 に対して
        // ユーザーグループのメンバーでもなく Item の owner でもないユーザーからアクセス
        const int itemId = 19009;
        const string strangerId = "stranger-9999";
        const string strangerName = "stranger_user";

        NavigationManager navigationManager = _ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost:5009/ItemDetail/{itemId}");

        _authorization.SetAuthorized(strangerName);
        _authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, strangerId));

        // 閲覧権限がないため null を返す
        _ = _itemDetailDataMock.Setup(d => d.GetItemDetailAsync(itemId, strangerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemDetailPageData?)null);

        IRenderedComponent<SRNSMudApp.Components.Item.ItemDetail> cut =
            _ctx.Render<SRNSMudApp.Components.Item.ItemDetail>(parameters => parameters.Add(p => p.ItemId, itemId));

        cut.WaitForState(() => !cut.Markup.Contains("mud-progress-circular"));

        Assert.Contains("アイテムが見つかりません。", cut.Markup);
        _itemDetailDataMock.Verify(d => d.GetItemDetailAsync(itemId, strangerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ItemDetail_WhenAdminUser_PassesIsAdminTrue()
    {
        const int itemId = 19009;
        const string adminId = "admin-001";
        const string adminName = "admin_user";

        NavigationManager navigationManager = _ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost:5009/ItemDetail/{itemId}");

        _authorization.SetAuthorized(adminName);
        _authorization.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, adminId),
            new Claim(ClaimTypes.Role, "Admin"));

        var item = new SRNSMudApp.Data.Item
        {
            Id = itemId,
            Content = "Admin visible content",
            IsPrivate = true,
            IsAdminHidden = true,
            OwnerId = "author-001",
            Owner = new ApplicationUser { Id = "author-001", UserName = "author" }
        };

        var pageData = new ItemDetailPageData(item, [], [], []);

        _ = _itemDetailDataMock.Setup(d => d.GetItemDetailAsync(itemId, adminId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageData);
        _ = _contractServiceMock.Setup(s => s.GetRequestsByItemIdAsync(itemId))
            .ReturnsAsync([]);

        IRenderedComponent<SRNSMudApp.Components.Item.ItemDetail> cut =
            _ctx.Render<SRNSMudApp.Components.Item.ItemDetail>(parameters => parameters.Add(p => p.ItemId, itemId));

        cut.WaitForState(() => !cut.Markup.Contains("mud-progress-circular"));

        Assert.Contains("Admin visible content", cut.Markup);
        _itemDetailDataMock.Verify(d => d.GetItemDetailAsync(itemId, adminId, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}