#region

using Bunit;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.Account;
using SRNSMudApp.Components.Account.Shared;
using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Tests;

public class AccountPagesTests : BunitContext
{
    public AccountPagesTests()
    {
        // 共通のモックや依存関係の設定
        var antiforgeryMock = new Mock<IAntiforgery>();
        _ = antiforgeryMock.Setup(a => a.GetTokens(It.IsAny<HttpContext>()))
            .Returns(new AntiforgeryTokenSet("dummy-request-token", "dummy-cookie-token", "form-field-name",
                "header-name"));
        _ = Services.AddSingleton(antiforgeryMock.Object);
        // NavigationManager は bUnit に標準で組み込まれている
    }

    [Fact]
    public void StatusMessage_RendersWithoutException_WhenHttpContextIsNull()
    {
        // Arrange
        // bUnit はデフォルトで HttpContext などの CascadingParameter を提供しないため、null になる

        // Act & Assert
        // 例外（NullReferenceException など）が発生しないことを確認する
        Exception? exception = Record.Exception(() => Render<StatusMessage>());
        Assert.Null(exception);
    }

    [Fact]
    public void PasskeySubmit_RendersWithoutException_WhenHttpContextIsNull()
    {
        // Arrange
        var testContext = new BunitContext();

        // Mock AntiforgeryStateProvider
        // In bUnit, AntiforgeryStateProvider is not registered by default.
        // We can either mock it or just use Bunit.BunitContext.Services to add a dummy implementation or use bUnit's built-in support if available.
        // However, for AntiforgeryStateProvider, the simplest is to just add it as a mock or use the underlying mechanism if possible.
        // Actually, since AntiforgeryStateProvider is an abstract class, we can mock it with Moq.
        var antiforgeryStateProviderMock = new Mock<AntiforgeryStateProvider>();
        _ = antiforgeryStateProviderMock.Setup(p => p.GetAntiforgeryToken())
            .Returns(new AntiforgeryRequestToken("RequestVerificationToken", "dummy-token"));

        _ = testContext.Services.AddSingleton(antiforgeryStateProviderMock.Object);

        // Act & Assert
        Exception? exception = Record.Exception(() => testContext.Render<PasskeySubmit>(parameters =>
            parameters
                .Add(p => p.Operation, PasskeyOperation.Request)
                .Add(p => p.Name, "test-name")
                .Add(p => p.ChildContent, "Submit")));

        Assert.Null(exception);
    }

    [Fact]
    public void Login_RendersExternalLoginButtons_AndInvokesGoogleRender()
    {
        // Arrange
        var configurationMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        _ = configurationMock.Setup(c => c["Authentication:Google:ClientId"]).Returns("test-google-client-id");
        _ = Services.AddSingleton(configurationMock.Object);
        _ = Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Act
        IRenderedComponent<SRNSMudApp.Components.Account.Pages.Login> cut =
            Render<SRNSMudApp.Components.Account.Pages.Login>();

        // Assert: Google button container is rendered
        Assert.NotNull(cut.Find("#google-login-button-container"));

        // Assert: LINE and GitHub buttons are rendered
        var markup = cut.Markup;
        Assert.Contains("Continue with LINE", markup);
        Assert.Contains("Continue with GitHub", markup);

        // Assert: customAuth.renderGoogleButton JS invocation was made
        JSInterop.VerifyInvoke("customAuth.renderGoogleButton");
    }

    [Fact]
    public void RenamePasskey_RendersInputWithCorrectModelBindingName()
    {
        // Arrange
        var testContext = new BunitContext();
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = testContext.Services.AddMudServices();

        var storeMock = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _ = testContext.Services.AddScoped(_ => userManagerMock.Object);

        _ = testContext.Services.AddScoped<IdentityRedirectManager>();

        var antiforgeryMock = new Mock<IAntiforgery>();
        _ = antiforgeryMock.Setup(a => a.GetTokens(It.IsAny<HttpContext>()))
            .Returns(new AntiforgeryTokenSet("dummy", "dummy", "form", "header"));
        _ = testContext.Services.AddSingleton(antiforgeryMock.Object);

        var antiforgeryStateProviderMock = new Mock<AntiforgeryStateProvider>();
        _ = antiforgeryStateProviderMock.Setup(p => p.GetAntiforgeryToken())
            .Returns(new AntiforgeryRequestToken("RequestVerificationToken", "dummy-token"));
        _ = testContext.Services.AddSingleton(antiforgeryStateProviderMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        testContext.Renderer.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", false));

        var dummyUser = new ApplicationUser { Id = "test-user-id", UserName = "testuser" };
        _ = userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(dummyUser);
        var passkeyInfo = new Microsoft.AspNetCore.Identity.UserPasskeyInfo([1, 2, 3], [], DateTimeOffset.UtcNow, 0, null, false, false, false, [], [])
        {
            Name = "My Passkey"
        };
        _ = userManagerMock.Setup(m => m.GetPasskeyAsync(It.IsAny<ApplicationUser>(), It.IsAny<byte[]>()))
            .ReturnsAsync(passkeyInfo);

        // Act
        IRenderedComponent<SRNSMudApp.Components.Account.Pages.Manage.RenamePasskey> cut =
            testContext.Render<SRNSMudApp.Components.Account.Pages.Manage.RenamePasskey>(parameters => parameters
                .Add(p => p.Id, "AQID") // Base64Url for [1, 2, 3]
                .AddCascadingValue(httpContext));

        // Assert: name="Input.Name" is generated, guaranteeing model binding on POST
        AngleSharp.Dom.IElement inputElement = cut.Find("input[name='Input.Name']");
        Assert.NotNull(inputElement);
        Assert.Equal("My Passkey", inputElement.GetAttribute("value"));
    }
}