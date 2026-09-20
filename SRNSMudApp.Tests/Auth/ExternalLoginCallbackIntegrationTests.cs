using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services.Auth;

namespace SRNSMudApp.Tests.Auth;

public class ExternalLoginCallbackIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class TestExternalTokenVerificationService : IExternalTokenVerificationService
    {
        public Task<Result<ExternalTokenPayload>> VerifyTokenAsync(
            string provider,
            string token,
            CancellationToken cancellationToken = default)
        {
            if (token.StartsWith("mock-"))
            {
                var providerKey = $"{token.Replace("mock-", "")}-id";
                var email = $"{token.Replace("mock-", "")}@example.com";
                var payload = new ExternalTokenPayload(email, providerKey);
                return Task.FromResult<Result<ExternalTokenPayload>>(new Success<ExternalTokenPayload>(payload));
            }

            return Task.FromResult<Result<ExternalTokenPayload>>(new Failure("Invalid mock token"));
        }
    }

    private HttpClient CreateCustomClient()
    {
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalTokenVerificationService>();
                services.AddScoped<IExternalTokenVerificationService, TestExternalTokenVerificationService>();
            });
        });

        return customFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("Google", "mock-google-test")]
    [InlineData("Line", "mock-line-test")]
    [InlineData("Github", "mock-github-test")]
    public async Task ExternalLogin_WithValidMockToken_ReturnsOkAndIssuesAuthCookie(string provider, string token)
    {
        // Arrange
        using HttpClient client = CreateCustomClient();
        var requestPayload = new
        {
            Provider = provider,
            Token = token
        };

        // Act: POST /api/auth/external-login
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/external-login", requestPayload);

        // Assert: 200 OK が返り、Cookie が発行される
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // レスポンスヘッダーに Set-Cookie が含まれること（ASP.NET Core Identity Cookie）
        Assert.True(response.Headers.Contains("Set-Cookie"), "Set-Cookie header should be present after successful external login");
        var cookieHeader = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains(".AspNetCore.Identity.Application", cookieHeader);
    }

    [Fact]
    public async Task ExternalLogin_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = CreateCustomClient();
        var requestPayload = new
        {
            Provider = "Google",
            Token = "invalid-token"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/external-login", requestPayload);

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExternalLogin_WhenUserIsBanned_ReturnsForbiddenAndNoAuthCookie()
    {
        // Arrange
        using HttpClient client = CreateCustomClient();

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var token = $"mock-ban-{uniqueKey}";
        var expectedEmail = $"ban-{uniqueKey}@example.com";
        var requestPayload = new
        {
            Provider = "Google",
            Token = token
        };

        // 1回目のログインでユーザー作成
        HttpResponseMessage firstResponse = await client.PostAsJsonAsync("/api/auth/external-login", requestPayload);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // データベースから対象ユーザーを取得し、BAN状態にする
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(expectedEmail);
            Assert.NotNull(user);
            user.IsBanned = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            _ = await userManager.UpdateSecurityStampAsync(user);
            _ = await userManager.UpdateAsync(user);
        }

        // Act: BANされた後に再度ログインを試みる
        HttpResponseMessage bannedResponse = await client.PostAsJsonAsync("/api/auth/external-login", requestPayload);

        // Assert: 403 Forbidden が返り、Cookie は発行されない
        Assert.Equal(HttpStatusCode.Forbidden, bannedResponse.StatusCode);
        Assert.False(bannedResponse.Headers.Contains("Set-Cookie"));
    }
}