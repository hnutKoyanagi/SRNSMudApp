using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TagResolutionDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();

    public TagResolutionDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _dataProviderMock.Setup(d => d.SearchTagsAsync(It.IsAny<string?>(), default)).ReturnsAsync([]);
        _ = _dataProviderMock.Setup(d => d.SearchParentCandidateTagsAsync(It.IsAny<string?>(), default)).ReturnsAsync([]);
        _ = _ctx.Services.AddScoped(_ => _dataProviderMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth("testuser");
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DefaultSelection_ShouldBeUseExisting_AndInitialSuggestionSelected()
    {
        // Arrange
        var candidateTag = new SRNSMudApp.Data.Tag
        {
            Id = 10,
            Name = "既存の人格タグ",
            OwnerId = "testuser"
        };

        _ = _dataProviderMock
            .Setup(d => d.SearchTagsAsync("人格", default))
            .ReturnsAsync([candidateTag]);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagResolutionDialog.TagName), "人格" },
            { nameof(TagResolutionDialog.TagKind), "UserCustomTag" }
        };

        // Act
        _ = await dialogService.ShowAsync<TagResolutionDialog>("未登録タグの解決", parameters);
        host.WaitForState(() => host.Markup.Contains("既存の人格タグ"));

        // Assert: 既存の別タグを割り当てるが選択され、候補タグが表示されていること
        Assert.Contains("既存の別タグを割り当てる", host.Markup);
        Assert.Contains("既存の人格タグ", host.Markup);

        // 決定ボタンが表示されていること
        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "決定");
        Assert.False(submitButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task WhenNoCandidate_DefaultsToSkip_AndSubmitDisabled()
    {
        // Arrange: 検索ワードに直接一致するタグがない場合（候補がない場合）
        _ = _dataProviderMock
            .Setup(d => d.SearchTagsAsync("未知のタグ", default))
            .ReturnsAsync([]);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagResolutionDialog.TagName), "未知のタグ" },
            { nameof(TagResolutionDialog.TagKind), "UserCustomTag" }
        };

        // Act
        IDialogReference dialogRef = await dialogService.ShowAsync<TagResolutionDialog>("未登録タグの解決", parameters);
        host.WaitForState(() => host.Markup.Contains("一致する既存タグの候補はありません"));

        // Assert: 候補がない旨のアラートが表示されていること
        Assert.Contains("一致する既存タグの候補はありません", host.Markup);
        Assert.DoesNotContain("既存の別タグを割り当てる (推奨)", host.Markup);

        // 決定ボタンは無効であること
        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "決定");
        Assert.True(submitButton.HasAttribute("disabled"));

        // スキップボタンがプライマリ（Filled）であること
        IRenderedComponent<MudButton> skipButtonComponent = host.FindComponents<MudButton>()
            .First(b => b.Instance.ChildContent != null && b.Markup.Contains("スキップ"));
        Assert.Equal(Color.Primary, skipButtonComponent.Instance.Color);
        Assert.Equal(Variant.Filled, skipButtonComponent.Instance.Variant);

        // スキップボタンをクリックしてダイアログが Skip で閉じること
        IElement skipButton = host.FindAll("button").First(b => b.TextContent.Trim() == "スキップ");
        await host.InvokeAsync(() => skipButton.Click());

        DialogResult? result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.IsType<TagResolutionDecision>(result.Data);

        var decision = (TagResolutionDecision)result.Data;
        Assert.Equal(TagResolutionAction.Skip, decision.Action);
    }

    [Fact]
    public async Task CreateNew_WhenManualParentSelected_PreloadsLevel1OrHigherParentCandidates()
    {
        // Arrange
        var level1Parent = new SRNSMudApp.Data.Tag
        {
            Id = 101,
            Name = "哲学",
            OwnerId = "system",
            Node = HierarchyId.Parse("/1/")
        };
        var level2Parent = new SRNSMudApp.Data.Tag
        {
            Id = 102,
            Name = "倫理学",
            OwnerId = "system",
            Node = HierarchyId.Parse("/1/1/")
        };

        _ = _dataProviderMock
            .Setup(d => d.SearchParentCandidateTagsAsync("自由", default))
            .ReturnsAsync([level1Parent, level2Parent]);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var suggestedParent = new SRNSMudApp.Data.Tag
        {
            Id = 200,
            Name = "AIサジェスト親",
            OwnerId = "system",
            Node = HierarchyId.Parse("/2/1/")
        };

        var parameters = new DialogParameters
        {
            { nameof(TagResolutionDialog.TagName), "自由" },
            { nameof(TagResolutionDialog.TagKind), "UserCustomTag" },
            { nameof(TagResolutionDialog.SuggestedParentTag), suggestedParent }
        };

        // Act
        _ = await dialogService.ShowAsync<TagResolutionDialog>("未登録タグの解決", parameters);
        host.WaitForState(() => host.Markup.Contains("既存の別タグを割り当てる"));

        // 新規タグとして作成する を選択
        IRenderedComponent<MudRadioGroup<TagResolutionAction>> actionRadioGroup =
            host.FindComponent<MudRadioGroup<TagResolutionAction>>();
        await host.InvokeAsync(() => actionRadioGroup.Instance.ValueChanged.InvokeAsync(TagResolutionAction.CreateNew));

        host.WaitForState(() => host.Markup.Contains("親タグ階層の決定"));

        // 別の親タグを手動で選択する を選択
        IReadOnlyList<IRenderedComponent<MudRadioGroup<bool>>> boolRadioGroups =
            host.FindComponents<MudRadioGroup<bool>>();
        IRenderedComponent<MudRadioGroup<bool>> parentRadioGroup = boolRadioGroups.First();
        await host.InvokeAsync(() => parentRadioGroup.Instance.ValueChanged.InvokeAsync(false));

        host.WaitForState(() => host.Markup.Contains("哲学"));

        // Assert: 1階層目以降の親タグ候補（哲学、倫理学）が展開されていること
        Assert.Contains("哲学", host.Markup);
        Assert.Contains("倫理学", host.Markup);

        // 決定ボタンが有効であること
        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "決定");
        Assert.False(submitButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task SkipButton_IsLeftOfSubmit_ClosesDialogWithSkipDecision_AndNoSkipRadio()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(TagResolutionDialog.TagName), "スキップ対象タグ" },
            { nameof(TagResolutionDialog.TagKind), "UserCustomTag" }
        };

        // Act
        IDialogReference dialogRef = await dialogService.ShowAsync<TagResolutionDialog>("未登録タグの解決", parameters);
        host.WaitForState(() => host.Markup.Contains("スキップ"));

        // Assert 1: ラジオボタンに「このタグのインポートをスキップする」が含まれないこと
        Assert.DoesNotContain("このタグのインポートをスキップする", host.Markup);

        // Assert 2: 決定ボタンの左隣に「スキップ」ボタンが存在すること
        var actionButtons = host.FindAll("button")
            .Where(b => b.TextContent.Trim() is "スキップ" or "決定")
            .ToList();

        Assert.Equal(2, actionButtons.Count);
        Assert.Equal("スキップ", actionButtons[0].TextContent.Trim());
        Assert.Equal("決定", actionButtons[1].TextContent.Trim());

        // Act: スキップボタンをクリック
        await host.InvokeAsync(() => actionButtons[0].Click());

        // Assert 3: TagResolutionAction.Skip でダイアログがクローズされること
        DialogResult? result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.IsType<TagResolutionDecision>(result.Data);

        var decision = (TagResolutionDecision)result.Data;
        Assert.Equal(TagResolutionAction.Skip, decision.Action);
        Assert.Equal("スキップ対象タグ", decision.NewTagName);
        Assert.Null(decision.SelectedExistingTagId);
        Assert.Null(decision.SelectedParentTagId);
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