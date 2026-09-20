using System.Security.Claims;

using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.User;

public sealed class UserManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IUserDataProvider> _userDataMock = new();

    public UserManagementTests()
    {
        _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ctx.Services.AddScoped(_ => _userDataMock.Object);

        var auth = _ctx.AddAuthorization();
        auth.SetAuthorized("test-admin");
        auth.SetRoles("Admin");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "test-user-id"));

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void RendersUserManagement_And_TogglesAdminRole()
    {
        var targetUser = new ApplicationUser { Id = "user1", UserName = "testuser@example.com", Email = "testuser@example.com" };

        _ = _userDataMock.Setup(d => d.GetAllUsersAsync())
            .ReturnsAsync([targetUser]);
        _ = _userDataMock.Setup(d => d.IsUserInRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
            .ReturnsAsync(false);
        _ = _userDataMock.Setup(d => d.UpdateUserAdminRoleAsync("user1", true))
            .ReturnsAsync(IdentityResult.Success);

        IRenderedComponent<UserManagement> component = _ctx.Render<UserManagement>();

        component.WaitForState(() => component.Markup.Contains("testuser@example.com"));
        Assert.Contains("testuser@example.com", component.Markup);

        IElement targetRow = component
            .FindAll("td")
            .First(td => td.TextContent.Contains("testuser@example.com"))
            .Closest("tr")!;
        IElement mudSwitch = targetRow.QuerySelector("input[type='checkbox']")!;
        mudSwitch.Change(true);

        component.WaitForAssertion(() => _userDataMock.Verify(d => d.UpdateUserAdminRoleAsync("user1", true), Times.Once));
    }

    [Fact]
    public void RendersUserManagement_And_TogglesBanStatus()
    {
        var targetUser = new ApplicationUser
        {
            Id = "user1",
            UserName = "target@example.com",
            Email = "target@example.com",
            IsBanned = false
        };

        _ = _userDataMock.Setup(d => d.GetAllUsersAsync())
            .ReturnsAsync([targetUser]);
        _ = _userDataMock.Setup(d => d.IsUserInRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
            .ReturnsAsync(false);
        _ = _userDataMock.Setup(d => d.SetUserBanStatusAsync("user1", true, null))
            .ReturnsAsync(IdentityResult.Success);

        IRenderedComponent<UserManagement> component = _ctx.Render<UserManagement>();

        component.WaitForState(() => component.Markup.Contains("target@example.com"));

        IElement targetRow = component
            .FindAll("td")
            .First(td => td.TextContent.Contains("target@example.com"))
            .Closest("tr")!;

        // 2番目のスイッチがBANスイッチ（1番目はAdmin権限スイッチ）
        var checkboxes = targetRow.QuerySelectorAll("input[type='checkbox']").ToList();
        Assert.Equal(2, checkboxes.Count);

        checkboxes[1].Change(true);

        component.WaitForAssertion(() => _userDataMock.Verify(d => d.SetUserBanStatusAsync("user1", true, null), Times.Once));
    }

    [Fact]
    public void RendersUserManagement_ProtectsCurrentUserAndSystemUser()
    {
        var currentUser = new ApplicationUser
        {
            Id = "test-user-id",
            UserName = "admin@example.com",
            Email = "admin@example.com",
            IsBanned = false
        };
        var systemUser = new ApplicationUser
        {
            Id = "system",
            UserName = "system",
            Email = "system@example.com",
            IsBanned = false
        };

        _ = _userDataMock.Setup(d => d.GetAllUsersAsync())
            .ReturnsAsync([currentUser, systemUser]);
        _ = _userDataMock.Setup(d => d.IsUserInRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
            .ReturnsAsync(true);

        IRenderedComponent<UserManagement> component = _ctx.Render<UserManagement>();

        component.WaitForState(() => component.Markup.Contains("admin@example.com"));

        // ログイン中の管理者自身の行
        IElement adminRow = component
            .FindAll("td")
            .First(td => td.TextContent.Contains("admin@example.com"))
            .Closest("tr")!;
        foreach (var cb in adminRow.QuerySelectorAll("input[type='checkbox']"))
        {
            Assert.True(cb.HasAttribute("disabled"));
        }

        // systemユーザーの行
        IElement systemRow = component
            .FindAll("td")
            .First(td => td.TextContent.Trim() == "system")
            .Closest("tr")!;
        foreach (var cb in systemRow.QuerySelectorAll("input[type='checkbox']"))
        {
            Assert.True(cb.HasAttribute("disabled"));
        }
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}