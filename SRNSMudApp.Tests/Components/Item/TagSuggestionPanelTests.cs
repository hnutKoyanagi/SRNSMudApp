using Bunit;
using MudBlazor;
using MudBlazor.Services;
using SRNSMudApp.Components.Item;
using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     TagSuggestionPanel コンポーネントの bUnit 単体テスト。
///     強い関連タグと候補タグの表示、昇格・除外操作、閾値調整を検証する。
/// </summary>
public sealed class TagSuggestionPanelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TagSuggestionPanelTests()
    {
        _ = _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Render_WhenSuggestionsProvided_ClassifiesIntoStrongAndCandidate()
    {
        // Arrange: 0.70 は強い関連 (>= 0.65)、0.50 は候補 (0.40 〜 0.65)
        List<SuggestedTag> suggestions =
        [
            new(1, "C#", 0.75f),
            new(2, "Blazor", 0.50f)
        ];

        // Act
        var cut = _ctx.Render<TagSuggestionPanel>(parameters => parameters
            .Add(p => p.Suggestions, suggestions));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='tag-suggestion-panel']"));
        Assert.NotNull(cut.Find("[data-testid='strong-tag-chip-1']"));
        Assert.NotNull(cut.Find("[data-testid='candidate-tag-chip-2']"));
        Assert.Contains("C#", cut.Markup);
        Assert.Contains("Blazor", cut.Markup);
    }

    [Fact]
    public void Render_ExcludedTagIds_AreNotShown()
    {
        // Arrange
        List<SuggestedTag> suggestions =
        [
            new(1, "C#", 0.75f),
            new(2, "Blazor", 0.50f)
        ];

        // Act: TagId=1 を除外
        var cut = _ctx.Render<TagSuggestionPanel>(parameters => parameters
            .Add(p => p.Suggestions, suggestions)
            .Add(p => p.ExcludedTagIds, [1]));

        // Assert: C# は表示されず、Blazor のみ表示
        Assert.Empty(cut.FindAll("[data-testid='strong-tag-chip-1']"));
        Assert.NotNull(cut.Find("[data-testid='candidate-tag-chip-2']"));
    }

    [Fact]
    public void CandidateTag_WhenClicked_PromotesToStrongAndFiresCallback()
    {
        // Arrange
        List<SuggestedTag> suggestions =
        [
            new(10, "TypeScript", 0.45f) // 候補タグ
        ];

        IReadOnlyCollection<int>? confirmedIds = null;

        var cut = _ctx.Render<TagSuggestionPanel>(parameters => parameters
            .Add(p => p.Suggestions, suggestions)
            .Add(p => p.OnConfirmedTagIdsChanged, ids => confirmedIds = ids));

        // 初期状態: 候補タグとして表示、確定タグは空
        Assert.NotNull(cut.Find("[data-testid='candidate-tag-chip-10']"));
        Assert.Empty(confirmedIds ?? []);

        // Act: 候補チップをクリックして昇格
        cut.Find("[data-testid='candidate-tag-chip-10']").Click();

        // Assert: 強い関連タグに移動し、OnConfirmedTagIdsChanged に通知される
        Assert.NotNull(cut.Find("[data-testid='strong-tag-chip-10']"));
        Assert.NotNull(confirmedIds);
        Assert.Contains(10, confirmedIds);
    }

    [Fact]
    public void StrongTag_WhenCloseClicked_DemotesToCandidateAndRemovesFromCallback()
    {
        // Arrange
        List<SuggestedTag> suggestions =
        [
            new(20, "DotNet", 0.85f) // 強い関連タグ
        ];

        IReadOnlyCollection<int>? confirmedIds = null;

        var cut = _ctx.Render<TagSuggestionPanel>(parameters => parameters
            .Add(p => p.Suggestions, suggestions)
            .Add(p => p.OnConfirmedTagIdsChanged, ids => confirmedIds = ids));

        // 初期状態で強い関連タグに含まれる
        Assert.NotNull(confirmedIds);
        Assert.Contains(20, confirmedIds);

        // Act: チップの閉じるボタンをクリック
        var closeButton = cut.Find("[data-testid='strong-tag-chip-20'] button");
        closeButton.Click();

        // Assert: 候補タグへ移動し、確定リストから除外される
        Assert.NotNull(cut.Find("[data-testid='candidate-tag-chip-20']"));
        Assert.NotNull(confirmedIds);
        Assert.DoesNotContain(20, confirmedIds);
    }

    [Fact]
    public void ToggleSettings_OpensThresholdSliders()
    {
        // Arrange
        List<SuggestedTag> suggestions = [new(1, "Test", 0.70f)];
        var cut = _ctx.Render<TagSuggestionPanel>(parameters => parameters
            .Add(p => p.Suggestions, suggestions));

        // 初期状態ではスライダー非表示
        Assert.Empty(cut.FindAll("[data-testid='strong-threshold-slider']"));

        // Act: 設定ボタンをクリック
        cut.Find("[data-testid='toggle-threshold-settings-button']").Click();

        // Assert: スライダーが表示される
        Assert.NotNull(cut.Find("[data-testid='strong-threshold-slider']"));
        Assert.NotNull(cut.Find("[data-testid='candidate-threshold-slider']"));
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}

