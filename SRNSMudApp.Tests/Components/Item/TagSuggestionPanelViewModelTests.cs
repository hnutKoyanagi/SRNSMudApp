namespace SRNSMudApp.Tests.Components.Item;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Models;

/// <summary>
///     TagSuggestionPanelViewModel の純粋な単体テスト。
///     SuggestionThresholds 型による閾値不変条件の保護、および
///     ViewModel インスタンスによるタグ分類・昇格・除外の状態遷移を検証する。
/// </summary>
public class TagSuggestionPanelViewModelTests
{
    [Fact]
    public void SuggestionThresholds_GuaranteesStrongGreaterThanOrEqualToCandidate()
    {
        // Strong に Candidate より小さい値を指定した場合、Candidate が Strong と同じ値に切り詰められる
        var thresholds = new SuggestionThresholds(strong: 0.3f, candidate: 0.6f);
        Assert.Equal(0.3f, thresholds.Strong);
        Assert.Equal(0.3f, thresholds.Candidate);

        // WithStrong で小さくした場合も Candidate が追従する
        var updated = thresholds.WithStrong(0.2f);
        Assert.Equal(0.2f, updated.Strong);
        Assert.Equal(0.2f, updated.Candidate);

        // WithCandidate で大きくした場合は Strong が押し上げられる
        var pushed = thresholds.WithCandidate(0.8f);
        Assert.Equal(0.8f, pushed.Strong);
        Assert.Equal(0.8f, pushed.Candidate);
    }

    [Fact]
    public void SetSuggestions_ClassifiesIntoStrongAndCandidateTags()
    {
        var vm = new TagSuggestionPanelViewModel();
        vm.InitializeThresholds(0.70f, 0.40f);

        List<SuggestedTag> suggestions =
        [
            new(1, "新体道", 0.85f),
            new(2, "哲学", 0.55f),
            new(3, "無関係タグ", 0.20f)
        ];

        vm.SetSuggestions(suggestions);

        Assert.Single(vm.StrongTags);
        Assert.Equal(1, vm.StrongTags[0].TagId);
        Assert.Equal("新体道", vm.StrongTags[0].TagName);

        Assert.Single(vm.CandidateTags);
        Assert.Equal(2, vm.CandidateTags[0].TagId);
        Assert.Equal("哲学", vm.CandidateTags[0].TagName);

        Assert.True(vm.ConfirmedTagIds.Contains(1));
        Assert.False(vm.ConfirmedTagIds.Contains(2));
        Assert.False(vm.ConfirmedTagIds.Contains(3));
        Assert.True(vm.HasAnySuggestions);
    }

    [Fact]
    public void SetExcludedTagIds_FiltersOutSpecifiedTags()
    {
        var vm = new TagSuggestionPanelViewModel();
        vm.InitializeThresholds(0.70f, 0.40f);
        vm.SetExcludedTagIds([1]); // 1 (新体道) を除外

        List<SuggestedTag> suggestions =
        [
            new(1, "新体道", 0.85f),
            new(2, "哲学", 0.55f)
        ];

        vm.SetSuggestions(suggestions);

        Assert.Empty(vm.StrongTags);
        Assert.Single(vm.CandidateTags);
        Assert.Equal(2, vm.CandidateTags[0].TagId);
        Assert.Empty(vm.ConfirmedTagIds);
    }

    [Fact]
    public void PromoteCandidateTag_MovesTagToStrongAndConfirmsIt()
    {
        var vm = new TagSuggestionPanelViewModel();
        vm.InitializeThresholds(0.70f, 0.40f);

        List<SuggestedTag> suggestions = [new(2, "哲学", 0.55f)];
        vm.SetSuggestions(suggestions);

        Assert.Empty(vm.StrongTags);
        Assert.Single(vm.CandidateTags);

        // ユーザーが候補タグの「＋」ボタンをクリックして昇格
        vm.PromoteCandidateTag(2);

        Assert.Single(vm.StrongTags);
        Assert.Equal(2, vm.StrongTags[0].TagId);
        Assert.Empty(vm.CandidateTags);
        Assert.Contains(2, vm.ConfirmedTagIds);
    }

    [Fact]
    public void RemoveStrongTag_MovesTagToCandidateIfCandidateThresholdMet()
    {
        var vm = new TagSuggestionPanelViewModel();
        vm.InitializeThresholds(0.70f, 0.40f);

        List<SuggestedTag> suggestions = [new(1, "新体道", 0.85f)];
        vm.SetSuggestions(suggestions);

        Assert.Single(vm.StrongTags);

        // ユーザーが自動関連付けタグの「×」ボタンをクリックして除外
        vm.RemoveStrongTag(1);

        Assert.Empty(vm.StrongTags);
        Assert.Single(vm.CandidateTags);
        Assert.Equal(1, vm.CandidateTags[0].TagId);
        Assert.Empty(vm.ConfirmedTagIds);
    }

    [Fact]
    public void ToggleSettings_TogglesVisibilityFlag()
    {
        var vm = new TagSuggestionPanelViewModel();
        Assert.False(vm.ShowSettings);

        vm.ToggleSettings();
        Assert.True(vm.ShowSettings);

        vm.ToggleSettings();
        Assert.False(vm.ShowSettings);
    }
}