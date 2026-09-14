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

        // Act: 詳細ボタンをクリック
        var detailBtn = cut.Find("button.tag-detail-button");
        Assert.NotNull(detailBtn);
        detailBtn.Click();

        // Assert: TagDetail ページへ遷移していること
        Assert.Contains("/TagDetail/42", navManager.Uri);
    }
}