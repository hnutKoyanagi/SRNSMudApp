using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UserGroup;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using UserGroupEntity = SRNSMudApp.Data.UserGroup;

namespace SRNSMudApp.Tests.Components.UserGroup;

/// <summary>
///     <see cref="UserGroupMembers"/> 画面（URL ルーティング対応メンバー管理ページ）の UI テスト。
///     GroupId パラメータに応じたグループ情報の読み込み、オーナー/メンバー表示、エラー表示を検証する。
/// </summary>
public class UserGroupMembersPageTests : IAsyncDisposable
{
    private const string OwnerUserId = "owner-user-123";
    private const string MemberUserId = "member-user-456";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IUserGroupDataProvider> _userGroupDataMock = new();
    private readonly Mock<IUserDataProvider> _userDataMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly BunitAuthorizationContext _authContext;

    public UserGroupMembersPageTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _ctx.Services.RemoveAll<IUserGroupDataProvider>();
        _ctx.Services.AddScoped(_ => _userGroupDataMock.Object);

        _ctx.Services.RemoveAll<IUserDataProvider>();
        _ctx.Services.AddScoped(_ => _userDataMock.Object);

        _ctx.Services.RemoveAll<ISnackbar>();
        _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.Services.AddTransient<UserGroupMembersViewModel>();

        _authContext = _ctx.AddAuthorization();
        _authContext.SetAuthorized("OwnerUser");
        _authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, OwnerUserId),
            new Claim(ClaimTypes.Name, "OwnerUser"));

        _ = _ctx.Render<MudPopoverProvider>();
    }

    [Fact]
    public void WhenGroupNotFound_RendersErrorMessage()
    {
        _userGroupDataMock
            .Setup(d => d.GetUserGroupByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserGroupEntity?)null);

        var cut = _ctx.Render<UserGroupMembers>(parameters => parameters
            .Add(p => p.GroupId, 999));

        Assert.Contains("指定されたユーザーグループが見つかりませんでした", cut.Markup);
    }

    [Fact]
    public void WhenGroupFoundAndUserIsOwner_RendersAddMemberSectionAndMemberList()
    {
        var group = new UserGroupEntity
        {
            Id = 1,
            Name = "開発チーム",
            Description = "コア開発チームのグループ",
            OwnerId = OwnerUserId,
            Members =
            [
                new UserGroupMember
                {
                    UserId = OwnerUserId,
                    OwnerId = OwnerUserId,
                    User = new ApplicationUser { Id = OwnerUserId, UserName = "Alice" },
                    CreatedDate = DateTime.UtcNow
                },
                new UserGroupMember
                {
                    UserId = MemberUserId,
                    OwnerId = OwnerUserId,
                    User = new ApplicationUser { Id = MemberUserId, UserName = "Bob" },
                    CreatedDate = DateTime.UtcNow
                }
            ]
        };

        _userGroupDataMock
            .Setup(d => d.GetUserGroupByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var cut = _ctx.Render<UserGroupMembers>(parameters => parameters
            .Add(p => p.GroupId, 1));

        Assert.Contains("開発チーム", cut.Markup);
        Assert.Contains("コア開発チームのグループ", cut.Markup);
        Assert.Contains("新規メンバー追加", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("オーナー", cut.Markup);
        Assert.Contains("管理者", cut.Markup);
    }

    [Fact]
    public void WhenGroupFoundAndUserIsNotOwner_DoesNotRenderAddMemberSection()
    {
        _authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, MemberUserId),
            new Claim(ClaimTypes.Name, "Bob"));

        var group = new UserGroupEntity
        {
            Id = 1,
            Name = "開発チーム",
            OwnerId = OwnerUserId,
            Members =
            [
                new UserGroupMember
                {
                    UserId = OwnerUserId,
                    OwnerId = OwnerUserId,
                    User = new ApplicationUser { Id = OwnerUserId, UserName = "Alice" },
                    CreatedDate = DateTime.UtcNow
                }
            ]
        };

        _userGroupDataMock
            .Setup(d => d.GetUserGroupByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var cut = _ctx.Render<UserGroupMembers>(parameters => parameters
            .Add(p => p.GroupId, 1));

        Assert.Contains("開発チーム", cut.Markup);
        Assert.DoesNotContain("新規メンバー追加", cut.Markup);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}