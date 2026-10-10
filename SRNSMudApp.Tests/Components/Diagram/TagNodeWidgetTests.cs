using Blazor.Diagrams;
using Blazor.Diagrams.Core.Geometry;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;
using SRNSMudApp.Tests.TestSupport;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Diagram;

public class TagNodeWidgetTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();

    public TagNodeWidgetTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void TagNodeWidget_RendersTagDetailButton_AndNavigatesToTagDetailPage()
    {
        // Arrange
        var diagram = new BlazorDiagram();
        var tag = new TagEntity { Id = 42, Name = "Rust", OwnerId = "user1" };
        var node = new TagNode(tag, new Point(10, 10));
        diagram.Nodes.Add(node);

        var navManager = _ctx.Services.GetRequiredService<NavigationManager>();

        var cut = _ctx.Render<TagNodeWidget>(parameters => parameters
            .Add(p => p.Node, node)
            .AddCascadingValue(diagram));

        // Assert: 詳細リンクが描画され、Href が /TagDetail/42 であること
        var detailLink = cut.Find("a.tag-detail-button");
        Assert.NotNull(detailLink);
        Assert.Equal("/TagDetail/42", detailLink.GetAttribute("href"));
    }

    [Theory]
    [InlineData(TagFocusRole.Source, "tag-role-chip", "始点 (Source)")]
    [InlineData(TagFocusRole.Target, "tag-role-chip", "終点 (Target)")]
    public void TagNodeWidget_RendersRoleChip_WhenFocusedRoleIsSet(TagFocusRole role, string expectedClass, string expectedTooltip)
    {
        // Arrange
        var diagram = new BlazorDiagram();
        var tag = new TagEntity { Id = 10, Name = "C#", OwnerId = "user1" };
        var node = new TagNode(tag, new Point(0, 0)) { FocusRole = role };
        diagram.Nodes.Add(node);

        // Act
        var cut = _ctx.Render<TagNodeWidget>(parameters => parameters
            .Add(p => p.Node, node)
            .AddCascadingValue(diagram));

        // Assert
        var chip = cut.Find($".{expectedClass}");
        Assert.NotNull(chip);
        var tooltip = cut.FindComponents<MudTooltip>().FirstOrDefault(t => t.Instance.Text == expectedTooltip);
        Assert.NotNull(tooltip);
    }

    [Fact]
    public void TagNodeWidget_RendersMultilineTagName_InNodeNameElement()
    {
        // Arrange
        var diagram = new BlazorDiagram();
        var multilineName = "Line 1\nLine 2\nLine 3";
        var tag = new TagEntity { Id = 99, Name = multilineName, OwnerId = "user1" };
        var node = new TagNode(tag, new Point(0, 0));
        diagram.Nodes.Add(node);

        // Act
        var cut = _ctx.Render<TagNodeWidget>(parameters => parameters
            .Add(p => p.Node, node)
            .AddCascadingValue(diagram));

        // Assert
        var nameSpan = cut.Find(".tag-node-name");
        Assert.NotNull(nameSpan);
        Assert.Contains("Line 1", nameSpan.TextContent);
        Assert.Contains("Line 2", nameSpan.TextContent);
        Assert.Contains("Line 3", nameSpan.TextContent);
    }
}