using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.Pages;

namespace SRNSMudApp.Tests.Components.Pages;

public sealed class AuthCallbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AuthCallbackTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AuthCallback_WithProviderAndCodeQuery_InvokesLoginWithTokenJs()
    {
        // Arrange
        NavigationManager navigationManager = _ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/auth/callback?provider=Google&code=mock-google-test");

        // Act
        IRenderedComponent<AuthCallback> cut = _ctx.Render<AuthCallback>();

        // Assert: 2000ms delay inside component plus buffer
        await Task.Delay(2500);
        _ctx.JSInterop.VerifyInvoke("customAuth.loginWithToken");
    }

    [Fact]
    public void AuthCallback_WithoutQueryParameters_RedirectsToLogin()
    {
        // Arrange
        NavigationManager navigationManager = _ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/auth/callback");

        // Act
        IRenderedComponent<AuthCallback> cut = _ctx.Render<AuthCallback>();

        // Assert: Missing parameters redirect to /Account/Login
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("/Account/Login", navigationManager.Uri);
        }, TimeSpan.FromSeconds(5));
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}