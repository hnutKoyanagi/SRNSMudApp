using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TagLinkReplaceDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();

    public TagLinkReplaceDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _dataProviderMock.Setup(d => d.SearchTagsAsync(It.IsAny<string?>(), default)).ReturnsAsync([]);
        _ = _ctx.Services.AddScoped(_ => _dataProviderMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth("testuser");
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WhenFirstCandidateExists_DefaultIsReplaceWithFirstCandidate()
    {
        var candidateTag = new SRNSMudApp.Data.Tag
        {
            Id = 42,
            Name = "既存タグ",
            OwnerId = "testuser"
        };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagLinkReplaceDialog.OriginalMatchText), "[既存タグ](https://example.com/rel-1)" },
            { nameof(TagLinkReplaceDialog.TagName), "既存タグ" },
            { nameof(TagLinkReplaceDialog.FirstCandidateTag), candidateTag }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagLinkReplaceDialog>("内部リンクへの置き換え確認", parameters);
        host.WaitForState(() => host.Markup.Contains("サジェストの第1候補通りに置き換え"));

        // ラジオボタングループの選択値が ReplaceWithFirstCandidate であること
        IRenderedComponent<MudRadioGroup<TagLinkReplaceAction>> radioGroup = host.FindComponent<MudRadioGroup<TagLinkReplaceAction>>();
        Assert.Equal(TagLinkReplaceAction.ReplaceWithFirstCandidate, radioGroup.Instance.Value);

        // 適用ボタンが有効であること
        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "適用");
        Assert.False(submitButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task WhenFirstCandidateIsNull_DefaultIsDoNotReplace()
    {
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagLinkReplaceDialog.OriginalMatchText), "[未知タグ](https://example.com/rel-2)" },
            { nameof(TagLinkReplaceDialog.TagName), "未知タグ" },
            { nameof(TagLinkReplaceDialog.FirstCandidateTag), null }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagLinkReplaceDialog>("内部リンクへの置き換え確認", parameters);
        host.WaitForState(() => host.Markup.Contains("置き換えない / スキップ"));

        // ラジオボタングループの選択値が DoNotReplace であること
        IRenderedComponent<MudRadioGroup<TagLinkReplaceAction>> radioGroup = host.FindComponent<MudRadioGroup<TagLinkReplaceAction>>();
        Assert.Equal(TagLinkReplaceAction.DoNotReplace, radioGroup.Instance.Value);

        // 適用ボタンが有効であること（スキップ選択時は適用可能）
        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "適用");
        Assert.False(submitButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task SkipButton_ClosesDialogWithDoNotReplaceDecision()
    {
        var candidateTag = new SRNSMudApp.Data.Tag
        {
            Id = 42,
            Name = "既存タグ",
            OwnerId = "testuser"
        };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagLinkReplaceDialog.OriginalMatchText), "[既存タグ](https://example.com/rel-1)" },
            { nameof(TagLinkReplaceDialog.TagName), "既存タグ" },
            { nameof(TagLinkReplaceDialog.FirstCandidateTag), candidateTag }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagLinkReplaceDialog>("内部リンクへの置き換え確認", parameters);
        host.WaitForState(() => host.Markup.Contains("スキップ"));

        // スキップボタンをクリック
        IElement skipButton = host.FindAll("button").First(b => b.TextContent.Trim() == "スキップ");
        await host.InvokeAsync(() => skipButton.Click());

        DialogResult? result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.IsType<TagLinkReplaceDecision>(result.Data);

        var decision = (TagLinkReplaceDecision)result.Data;
        Assert.Equal(TagLinkReplaceAction.DoNotReplace, decision.Action);
        Assert.Null(decision.SelectedTagId);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private sealed class DialogHost : ComponentBase
    {
        [Parameter] public RenderFragment ChildContent { get; set; } = _ => { };

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(0);
            builder.AddAttribute(1, nameof(Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<MudDialogProvider>(0);
                b.CloseComponent();
                b.AddContent(1, ChildContent);
            }));
            builder.CloseComponent();
        }
    }
}