using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services.Providers;

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     各 <see cref="SRNSMudApp.Services.ILinkPreviewProvider"/> 実装の CanHandle およびプレビュー生成ロジックの単体テスト。
/// </summary>
public class LinkPreviewProviderTests
{
    [Theory]
    [InlineData("/ItemDetail/42", true)]
    [InlineData("https://localhost/ItemDetail/42", true)]
    [InlineData("/ItemDetail/abc", false)]
    [InlineData("/TagDetail/42", false)]
    [InlineData("https://example.com", false)]
    public void ItemLinkPreviewProvider_CanHandle_MatchesExpectedPaths(string url, bool expected)
    {
        var mockDbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var provider = new ItemLinkPreviewProvider(mockDbFactory.Object);
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);

        Assert.Equal(expected, provider.CanHandle(uri));
    }

    [Theory]
    [InlineData("/TagDetail/10", true)]
    [InlineData("https://localhost/TagDetail/10", true)]
    [InlineData("/TagDetail/not-a-number", false)]
    [InlineData("/ItemDetail/10", false)]
    [InlineData("https://example.com", false)]
    public void TagLinkPreviewProvider_CanHandle_MatchesExpectedPaths(string url, bool expected)
    {
        var mockDbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var provider = new TagLinkPreviewProvider(mockDbFactory.Object);
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);

        Assert.Equal(expected, provider.CanHandle(uri));
    }

    [Theory]
    [InlineData("/User/UserDetail/u123", true)]
    [InlineData("https://localhost/User/UserDetail/u123", true)]
    [InlineData("/User/UserDetail/", true)]
    [InlineData("/ItemDetail/10", false)]
    [InlineData("https://example.com", false)]
    public void UserLinkPreviewProvider_CanHandle_MatchesExpectedPaths(string url, bool expected)
    {
        var mockDbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var provider = new UserLinkPreviewProvider(mockDbFactory.Object);
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);

        Assert.Equal(expected, provider.CanHandle(uri));
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com/page", true)]
    [InlineData("/ItemDetail/10", false)]
    [InlineData("ftp://example.com", false)]
    public void ExternalOgpLinkPreviewProvider_CanHandle_MatchesHttpAndHttps(string url, bool expected)
    {
        var httpClient = new HttpClient();
        var provider = new ExternalOgpLinkPreviewProvider(httpClient, NullLogger<ExternalOgpLinkPreviewProvider>.Instance);
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);

        Assert.Equal(expected, provider.CanHandle(uri));
    }

    [Fact]
    public async Task ExternalOgpLinkPreviewProvider_GetPreviewAsync_ParsesOgpAndResolvesRelativeImageUrl()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta property="og:title" content="OGP Title" />
                <meta property="og:description" content="OGP Description text" />
                <meta property="og:image" content="/images/preview.png" />
                <meta property="og:site_name" content="ExampleSite" />
            </head>
            <body>
                <p>Body text</p>
            </body>
            </html>
            """;

        var handler = new FakeHtmlResponseHandler(HttpStatusCode.OK, html, "text/html");
        var httpClient = new HttpClient(handler);
        var provider = new ExternalOgpLinkPreviewProvider(httpClient, NullLogger<ExternalOgpLinkPreviewProvider>.Instance);

        var uri = new Uri("https://example.com/articles/1");
        LinkPreviewData result = await provider.GetPreviewAsync(uri, "https://example.com/articles/1");

        Assert.True(result.IsSuccess);
        Assert.Equal("OGP Title", result.Title);
        Assert.Equal("OGP Description text", result.Description);
        Assert.Equal("https://example.com/images/preview.png", result.ImageUrl);
        Assert.Equal("ExampleSite", result.SiteName);
    }

    [Fact]
    public async Task ExternalOgpLinkPreviewProvider_GetPreviewAsync_NonHtmlResponse_ReturnsUnsuccessful()
    {
        var handler = new FakeHtmlResponseHandler(HttpStatusCode.OK, "{\"key\":\"value\"}", "application/json");
        var httpClient = new HttpClient(handler);
        var provider = new ExternalOgpLinkPreviewProvider(httpClient, NullLogger<ExternalOgpLinkPreviewProvider>.Instance);

        var uri = new Uri("https://example.com/api/data");
        LinkPreviewData result = await provider.GetPreviewAsync(uri, "https://example.com/api/data");

        Assert.False(result.IsSuccess);
    }

    private sealed class FakeHtmlResponseHandler(HttpStatusCode statusCode, string content, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, mediaType)
            };
            return Task.FromResult(response);
        }
    }
}