// Components/Account/ManageProfileTests.cs
#region

using System.Security.Claims;

using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.Account;
using SRNSMudApp.Data;

using ManageIndex = SRNSMudApp.Components.Account.Pages.Manage.Index;

#endregion

namespace SRNSMudApp.Tests.Components.Account;

/// <summary>
///     プロフィール管理画面（/Account/Manage）におけるユーザー名変更機能のテスト。
/// </summary>
public sealed class ManageProfileTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ManageProfileTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    private static UserManager<ApplicationUser> CreateUserManager(
        Mock<IUserStore<ApplicationUser>> storeMock,
        string? allowedUserNameCharacters = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(storeMock.Object);
        _ = services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.AllowedUserNameCharacters = allowedUserNameCharacters;
        });

        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<UserManager<ApplicationUser>>();
    }

    [Fact]
    public async Task UserManager_WithNullAllowedUserNameCharacters_AllowsJapaneseAndSpecialCharacters()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1", UserName = "initial@example.com" };
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _ = storeMock.Setup(s => s.GetUserNameAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => user.UserName);
        _ = storeMock.Setup(s => s.SetUserNameAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationUser, string, CancellationToken>((u, name, _) => u.UserName = name)
            .Returns(Task.CompletedTask);
        _ = storeMock.Setup(s => s.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);
        _ = storeMock.Setup(s => s.GetUserIdAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _ = storeMock.Setup(s => s.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        UserManager<ApplicationUser> userManager = CreateUserManager(storeMock, allowedUserNameCharacters: null);

        // Act & Assert 1: 日本語文字列（漢字・ひらがな・カタカナ）への変更が成功すること
        IdentityResult updateJapaneseResult = await userManager.SetUserNameAsync(user, "山田 太郎（テスト）");
        Assert.True(updateJapaneseResult.Succeeded);
        Assert.Equal("山田 太郎（テスト）", user.UserName);

        // Act & Assert 2: 任意の記号・英数字が混在する文字列への変更が成功すること
        IdentityResult updateSymbolResult = await userManager.SetUserNameAsync(user, "User_01@Special!#$");
        Assert.True(updateSymbolResult.Succeeded);
        Assert.Equal("User_01@Special!#$", user.UserName);
    }

    [Fact]
    public async Task UserManager_DuplicateUserName_FailsWithDuplicateUserNameError()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u2", UserName = "AnotherUser" };
        var existingUser = new ApplicationUser { Id = "u1", UserName = "ExistingUser" };

        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _ = storeMock.Setup(s => s.GetUserNameAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => user.UserName);
        _ = storeMock.Setup(s => s.SetUserNameAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationUser, string, CancellationToken>((u, name, _) => u.UserName = name)
            .Returns(Task.CompletedTask);
        _ = storeMock.Setup(s => s.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _ = storeMock.Setup(s => s.GetUserIdAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _ = storeMock.Setup(s => s.GetUserIdAsync(existingUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser.Id);

        UserManager<ApplicationUser> userManager = CreateUserManager(storeMock, allowedUserNameCharacters: null);

        // Act
        IdentityResult result = await userManager.SetUserNameAsync(user, "ExistingUser");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task UserManager_EmptyOrWhitespaceUserName_FailsWithInvalidUserNameError()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1", UserName = "ValidUser" };
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _ = storeMock.Setup(s => s.GetUserNameAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => user.UserName);
        _ = storeMock.Setup(s => s.SetUserNameAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationUser, string, CancellationToken>((u, name, _) => u.UserName = name)
            .Returns(Task.CompletedTask);
        _ = storeMock.Setup(s => s.GetUserIdAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);

        UserManager<ApplicationUser> userManager = CreateUserManager(storeMock, allowedUserNameCharacters: null);

        // Act & Assert
        IdentityResult result = await userManager.SetUserNameAsync(user, "   ");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "InvalidUserName");
    }

    [Fact]
    public void Index_RendersUsernameField_AsEditableWithCurrentUsername()
    {
        // Arrange
        var testUser = new ApplicationUser
        {
            Id = "test-user-id",
            UserName = "CurrentUserName",
            Email = "current@example.com",
            PhoneNumber = "090-1234-5678"
            Email = "current@example.com"
        };

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _ = userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(testUser);
        _ = userManagerMock.Setup(m => m.GetUserNameAsync(testUser))
            .ReturnsAsync(testUser.UserName);
        _ = userManagerMock.Setup(m => m.GetPhoneNumberAsync(testUser))
            .ReturnsAsync(testUser.PhoneNumber);

        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            null!, null!, null!, null!);

        var antiforgeryMock = new Mock<IAntiforgery>();
        _ = antiforgeryMock.Setup(a => a.GetTokens(It.IsAny<HttpContext>()))
            .Returns(new AntiforgeryTokenSet("dummy-request-token", "dummy-cookie-token", "form-field-name", "header-name"));

        _ = _ctx.Services.AddSingleton(antiforgeryMock.Object);
        _ = _ctx.Services.AddMudServices();
        _ = _ctx.Services.AddScoped(_ => userManagerMock.Object);
        _ = _ctx.Services.AddScoped(_ => signInManagerMock.Object);
        _ = _ctx.Services.AddScoped<IdentityRedirectManager>();

        var httpContext = new DefaultHttpContext();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUser.Id),
            new Claim(ClaimTypes.Name, testUser.UserName)
        ], "TestAuth"));
        httpContext.User = claimsPrincipal;

        // Act
        _ctx.Renderer.SetRendererInfo(new RendererInfo("Static", true));
        IRenderedComponent<ManageIndex> cut = _ctx.Render<ManageIndex>(parameters =>
            parameters.AddCascadingValue(httpContext));

        // Assert: Username の入力欄が存在し、Disabled ではないこと
        var usernameInput = cut.Find("input[placeholder='Please choose your username.']");
        Assert.NotNull(usernameInput);
        Assert.False(usernameInput.HasAttribute("disabled"), "Username field should be editable (not disabled).");
        Assert.Equal("CurrentUserName", usernameInput.GetAttribute("value"));

        // PhoneNumber フィールドが存在しないことを検証
        Assert.Empty(cut.FindAll("input[autocomplete='tel-national']"));
    }
}
