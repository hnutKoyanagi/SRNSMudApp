#region

using Sidio.Sitemap.Core;

using SRNSMudApp.Services.Sitemap;

#endregion

namespace SRNSMudApp.Tests.Services.Sitemap;

public class StaticPageSitemapProviderTests
{
    private readonly StaticPageSitemapProvider _provider = new();

    [Fact]
    public void GetNodes_WithString_ReturnsExpectedStaticNodes()
    {
        // Arrange
        const string baseUrl = "https://example.com";

        // Act
        var nodes = _provider.GetNodes(baseUrl);

        // Assert
        Assert.NotNull(nodes);
        Assert.Equal(3, nodes.Count);

        Assert.Contains(nodes, n => n.Url == "https://example.com/" && n.Priority == 1.0m && n.ChangeFrequency == ChangeFrequency.Daily);
        Assert.Contains(nodes, n => n.Url == "https://example.com/about" && n.Priority == 0.8m && n.ChangeFrequency == ChangeFrequency.Monthly);
        Assert.Contains(nodes, n => n.Url == "https://example.com/contact" && n.Priority == 0.5m && n.ChangeFrequency == ChangeFrequency.Monthly);
    }

    [Fact]
    public void GetNodes_WithUri_ReturnsExpectedStaticNodes()
    {
        // Arrange
        var baseUri = new Uri("https://example.com");

        // Act
        var nodes = _provider.GetNodes(baseUri);

        // Assert
        Assert.NotNull(nodes);
        Assert.Equal(3, nodes.Count);
        Assert.Contains(nodes, n => n.Url == "https://example.com/");
    }

    [Fact]
    public void GetNodes_WithTrailingSlash_NormalizesUrls()
    {
        // Arrange
        const string baseUrl = "https://example.com/";

        // Act
        var nodes = _provider.GetNodes(baseUrl);

        // Assert
        Assert.Contains(nodes, n => n.Url == "https://example.com/");
        Assert.Contains(nodes, n => n.Url == "https://example.com/about");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetNodes_WithInvalidString_ThrowsArgumentException(string invalidUrl)
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentException>(() => _provider.GetNodes(invalidUrl));
    }

    [Fact]
    public void GetNodes_WithNullUri_ThrowsArgumentNullException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => _provider.GetNodes((Uri)null!));
    }
}