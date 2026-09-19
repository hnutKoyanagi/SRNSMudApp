#region

using System.Collections.Generic;
using System.Linq;

using Bunit;

using Microsoft.AspNetCore.Components;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Models;

#endregion

namespace SRNSMudApp.Tests.Components.Item;

public class LinkConversionPanelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public LinkConversionPanelTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Render_WhenNoCandidates_ReturnsEmpty()
    {
        var cut = _ctx.Render<LinkConversionPanel>(parameters => parameters
            .Add(p => p.AutoReplaceCandidates, Array.Empty<LinkConversionCandidate>())
            .Add(p => p.ManualCandidates, Array.Empty<LinkConversionCandidate>())
            .Add(p => p.AutoReplaceThreshold, 0.85f));

        Assert.Empty(cut.Markup);
    }



    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}