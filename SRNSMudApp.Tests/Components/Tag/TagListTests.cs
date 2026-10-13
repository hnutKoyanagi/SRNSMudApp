using System.Collections.Generic;
using System.Threading.Tasks;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using Xunit;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TagListTests
{
    private static (BunitContext ctx, Mock<ITagSearchQueryService> queryServiceMock) CreateContext(string? currentUserId = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = ctx.Services.AddCascadingAuthenticationState();
        var mock = new Mock<ITagSearchQueryService>();
        _ = ctx.Services.AddScoped(_ => mock.Object);
        if (currentUserId is not null)
        {
            _ = ctx.Services.AddAuth(currentUserId);
        }
        else
        {
            Mock<AuthenticationStateProvider> anonAuthMock = new();
            var anonymousState = new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));
            _ = anonAuthMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(anonymousState);
            _ = ctx.Services.AddScoped(_ => anonAuthMock.Object);
        }
        _ = ctx.Render<MudPopoverProvider>();
        return (ctx, mock);
    }

    [Fact]
    public async Task TagList_WhenLoggedIn_LoadsPrivateTagsForUserAndMatchesSearch()
    {
        const string currentUserId = "test-user-id";
        var (ctx, queryServiceMock) = CreateContext(currentUserId);
        await using (ctx)
        {
            var myPrivateTag = new SRNSMudApp.Data.Tag
            {
                Id = 1,
                Name = "MyConfidentialTag",
                Content = "Private Project",
                IsPrivate = true,
                OwnerId = currentUserId
            };
            var publicTag = new SRNSMudApp.Data.Tag
            {
                Id = 2,
                Name = "GeneralPublicTag",
                Content = "Everyone can see",
                IsPrivate = false,
                OwnerId = "other-user"
            };

            queryServiceMock
                .Setup(s => s.GetTagsWithDetailsAsync(currentUserId, false))
                .ReturnsAsync([myPrivateTag, publicTag]);

            IRenderedComponent<TagList> cut = ctx.Render<TagList>();

            // ページ内にプライベートタグ名が表示されていること（非同期読み込み待機）
            cut.WaitForState(() => cut.Markup.Contains("MyConfidentialTag"), TimeSpan.FromSeconds(5));
            Assert.Contains("MyConfidentialTag", cut.Markup);
            Assert.Contains("GeneralPublicTag", cut.Markup);

            // クエリサービスがログインユーザーのコンテキストで呼び出されたことを検証
            queryServiceMock.Verify(s => s.GetTagsWithDetailsAsync(currentUserId, false), Times.Once);
        }
    }

    [Fact]
    public async Task TagList_WhenAnonymous_LoadsOnlyPublicTags()
    {
        var (ctx, queryServiceMock) = CreateContext(null);
        await using (ctx)
        {
            var publicTag = new SRNSMudApp.Data.Tag
            {
                Id = 2,
                Name = "PublicTagOnly",
                IsPrivate = false,
                OwnerId = "user-1"
            };

            queryServiceMock
                .Setup(s => s.GetTagsWithDetailsAsync(null, false))
                .ReturnsAsync([publicTag]);

            IRenderedComponent<TagList> cut = ctx.Render<TagList>();

            cut.WaitForState(() => cut.Markup.Contains("PublicTagOnly"), TimeSpan.FromSeconds(5));
            Assert.Contains("PublicTagOnly", cut.Markup);
            queryServiceMock.Verify(s => s.GetTagsWithDetailsAsync(null, false), Times.Once);
        }
    }

    [Fact]
    public async Task TagList_WhenUserIsAdmin_LoadsAllPrivateTags()
    {
        const string adminUserId = "admin-user-id";
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = ctx.Services.AddCascadingAuthenticationState();
        var mock = new Mock<ITagSearchQueryService>();
        _ = ctx.Services.AddScoped(_ => mock.Object);
        _ = ctx.Services.AddAuth(adminUserId, "Admin");
        _ = ctx.Render<MudPopoverProvider>();

        await using (ctx)
        {
            var otherUserPrivateTag = new SRNSMudApp.Data.Tag
            {
                Id = 1,
                Name = "OtherUserSecretTag",
                Content = "Classified",
                IsPrivate = true,
                OwnerId = "someone-else"
            };

            mock.Setup(s => s.GetTagsWithDetailsAsync(adminUserId, true))
                .ReturnsAsync([otherUserPrivateTag]);

            IRenderedComponent<TagList> cut = ctx.Render<TagList>();

            cut.WaitForState(() => cut.Markup.Contains("OtherUserSecretTag"), TimeSpan.FromSeconds(5));
            Assert.Contains("OtherUserSecretTag", cut.Markup);
            mock.Verify(s => s.GetTagsWithDetailsAsync(adminUserId, true), Times.Once);
        }
    }

    [Fact]
    public async Task TagList_WhenDataChangedFires_PreservesUserContextAndReloadsData()
    {
        const string currentUserId = "user-reload-test";
        var (ctx, queryServiceMock) = CreateContext(currentUserId);
        await using (ctx)
        {
            var tag1 = new SRNSMudApp.Data.Tag { Id = 1, Name = "Tag1", IsPrivate = true, OwnerId = currentUserId };
            var tag2 = new SRNSMudApp.Data.Tag { Id = 2, Name = "Tag2", IsPrivate = true, OwnerId = currentUserId };

            queryServiceMock
                .SetupSequence(s => s.GetTagsWithDetailsAsync(currentUserId, false))
                .ReturnsAsync([tag1])
                .ReturnsAsync([tag1, tag2]);

            IRenderedComponent<TagList> cut = ctx.Render<TagList>();

            cut.WaitForState(() => cut.Markup.Contains("Tag1"), TimeSpan.FromSeconds(5));
            Assert.Contains("Tag1", cut.Markup);

            // TagTable の OnDataChanged コールバックを発火させてリロードを検証
            IRenderedComponent<TagTable> tagTable = cut.FindComponent<TagTable>();
            await cut.InvokeAsync(() => tagTable.Instance.OnDataChanged.InvokeAsync());

            queryServiceMock.Verify(s => s.GetTagsWithDetailsAsync(currentUserId, false), Times.Exactly(2));
            cut.WaitForState(() => cut.Markup.Contains("Tag2"), TimeSpan.FromSeconds(5));
            Assert.Contains("Tag2", cut.Markup);
        }
    }
}