namespace SRNSMudApp.Components.Item;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Models;

/// <summary>
///     アイテム投稿時に、コンテンツと類似度の高いタグを提案するUIコンポーネント。
///     強い関連（自動関連付け）と候補（推薦）の2グループに分類し、
///     ユーザーは「＋追加」や「×除外」で移動可能。閾値のスライダー調整にも対応する。
///     状態管理とビジネスルールは TagSuggestionPanelViewModel に委譲する。
/// </summary>
public partial class TagSuggestionPanel : ComponentBase
{
    /// <summary>
    ///     推薦されたタグ提案一覧。
    /// </summary>
    [Parameter]
    public IReadOnlyList<SuggestedTag> Suggestions { get; set; } = [];

    /// <summary>
    ///     類似度計算のローディング中フラグ。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    ///     既に InitialTags などで選択済みのタグID一覧（提案から除外する）。
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<int> ExcludedTagIds { get; set; } = [];

    /// <summary>
    ///     保存時に自動関連付けされる対象となったタグID一覧が更新された際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<int>> OnConfirmedTagIdsChanged { get; set; }

    /// <summary>
    ///     閾値が変更された際の通知コールバック（任意）。
    /// </summary>
    [Parameter]
    public EventCallback<(float Strong, float Candidate)> OnThresholdsChanged { get; set; }

    /// <summary>
    ///     ユーザーごとの初期強い関連閾値（未指定時はデフォルト）。
    /// </summary>
    [Parameter]
    public float? InitialStrongThreshold { get; set; }

    /// <summary>
    ///     ユーザーごとの初期候補推薦閾値（未指定時はデフォルト）。
    /// </summary>
    [Parameter]
    public float? InitialCandidateThreshold { get; set; }

    private readonly TagSuggestionPanelViewModel _vm = new();

    public float StrongThreshold => _vm.StrongThreshold;
    public float CandidateThreshold => _vm.CandidateThreshold;

    public IReadOnlyList<SuggestedTag> StrongTags => _vm.StrongTags;
    public IReadOnlyList<SuggestedTag> CandidateTags => _vm.CandidateTags;
    public bool HasAnySuggestions => _vm.HasAnySuggestions;
    public bool ShowSettings => _vm.ShowSettings;

    private bool _thresholdsInitialized;
    private IReadOnlyList<SuggestedTag> _previousSuggestions = [];
    private IReadOnlyCollection<int> _previousExcludedTagIds = [];
    private HashSet<int> _lastNotifiedConfirmedIds = [];

    protected override async Task OnParametersSetAsync()
    {
        if (!_thresholdsInitialized && (InitialStrongThreshold.HasValue || InitialCandidateThreshold.HasValue))
        {
            _thresholdsInitialized = true;
            _vm.InitializeThresholds(InitialStrongThreshold, InitialCandidateThreshold);
        }

        bool suggestionsChanged = !ReferenceEquals(_previousSuggestions, Suggestions);
        bool excludedChanged = !ReferenceEquals(_previousExcludedTagIds, ExcludedTagIds)
                               && !_previousExcludedTagIds.SequenceEqual(ExcludedTagIds);

        if (suggestionsChanged || excludedChanged)
        {
            _previousSuggestions = Suggestions;
            _previousExcludedTagIds = ExcludedTagIds;

            _vm.SetExcludedTagIds(ExcludedTagIds);
            _vm.SetSuggestions(Suggestions);

            await NotifyConfirmedTagIdsChangedAsync();
        }
    }

    /// <summary>
    ///     候補タグを自動関連付け（強い関連）へ昇格させる。
    /// </summary>
    public async Task PromoteCandidateTag(SuggestedTag tag)
    {
        _vm.PromoteCandidateTag(tag.TagId);
        await NotifyConfirmedTagIdsChangedAsync();
    }

    /// <summary>
    ///     強い関連タグを自動関連付けから除外し、候補タグへ降格する。
    /// </summary>
    public async Task RemoveStrongTag(SuggestedTag tag)
    {
        _vm.RemoveStrongTag(tag.TagId);
        await NotifyConfirmedTagIdsChangedAsync();
    }

    /// <summary>
    ///     設定パネルの表示/非表示を切り替える。
    /// </summary>
    private void ToggleSettings() => _vm.ToggleSettings();

    /// <summary>
    ///     強い関連の閾値スライダー変更時。
    /// </summary>
    private async Task OnStrongThresholdChanged(float value)
    {
        _vm.ChangeStrongThreshold(value);
        await OnThresholdsChanged.InvokeAsync((_vm.StrongThreshold, _vm.CandidateThreshold));
        await NotifyConfirmedTagIdsChangedAsync();
    }

    /// <summary>
    ///     候補の閾値スライダー変更時。
    /// </summary>
    private async Task OnCandidateThresholdChanged(float value)
    {
        _vm.ChangeCandidateThreshold(value);
        await OnThresholdsChanged.InvokeAsync((_vm.StrongThreshold, _vm.CandidateThreshold));
        await NotifyConfirmedTagIdsChangedAsync();
    }

    /// <summary>
    ///     閾値を初期値に戻す。
    /// </summary>
    private async Task ResetThresholds()
    {
        _vm.ResetThresholds();
        await OnThresholdsChanged.InvokeAsync((_vm.StrongThreshold, _vm.CandidateThreshold));
        await NotifyConfirmedTagIdsChangedAsync();
    }

    private async Task NotifyConfirmedTagIdsChangedAsync()
    {
        var confirmedIds = _vm.ConfirmedTagIds.ToHashSet();
        if (!_lastNotifiedConfirmedIds.SetEquals(confirmedIds))
        {
            _lastNotifiedConfirmedIds = confirmedIds;
            if (OnConfirmedTagIdsChanged.HasDelegate)
            {
                await OnConfirmedTagIdsChanged.InvokeAsync(confirmedIds.ToList());
            }
        }
    }
}