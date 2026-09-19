using Microsoft.AspNetCore.Components;

using SRNSMudApp.Models;

namespace SRNSMudApp.Components.Item;

/// <summary>
///     アイテム投稿時に、コンテンツと類似度の高いタグを提案するUIコンポーネント。
///     強い関連（自動関連付け）と候補（推薦）の2グループに分類し、
///     ユーザーは「＋追加」や「×除外」で移動可能。閾値のスライダー調整にも対応する。
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

    /// <summary>強い関連（自動関連付け）の閾値</summary>
    public float StrongThreshold { get; private set; } = SuggestedTag.DefaultStrongThreshold;

    /// <summary>候補（推薦）の閾値</summary>
    public float CandidateThreshold { get; private set; } = SuggestedTag.DefaultCandidateThreshold;

    private readonly List<SuggestedTag> _strongTags = [];
    private readonly List<SuggestedTag> _candidateTags = [];
    private readonly HashSet<int> _manuallyPromotedTagIds = [];
    private readonly HashSet<int> _manuallyRemovedTagIds = [];

    private bool _showSettings;
    private IReadOnlyList<SuggestedTag> _previousSuggestions = [];

    private bool HasAnySuggestions => _strongTags.Count > 0 || _candidateTags.Count > 0;

    protected override async Task OnParametersSetAsync()
    {
        // 提案リストのインスタンスが変わった、または除外タグが変わった場合に再分類
        if (!ReferenceEquals(_previousSuggestions, Suggestions))
        {
            _previousSuggestions = Suggestions;
            // 新しい提案テキストになった場合は手動操作をリセット
            _manuallyPromotedTagIds.Clear();
            _manuallyRemovedTagIds.Clear();
            await ReclassifyAndNotifyAsync();
        }
        else
        {
            await ReclassifyAndNotifyAsync();
        }
    }

    /// <summary>
    ///     候補タグを自動関連付け（強い関連）へ昇格させる。
    /// </summary>
    public async Task PromoteCandidateTag(SuggestedTag tag)
    {
        _manuallyPromotedTagIds.Add(tag.TagId);
        _manuallyRemovedTagIds.Remove(tag.TagId);
        await ReclassifyAndNotifyAsync();
    }

    /// <summary>
    ///     強い関連タグを自動関連付けから除外し、候補タグへ降格する。
    /// </summary>
    public async Task RemoveStrongTag(SuggestedTag tag)
    {
        _manuallyRemovedTagIds.Add(tag.TagId);
        _manuallyPromotedTagIds.Remove(tag.TagId);
        await ReclassifyAndNotifyAsync();
    }

    /// <summary>
    ///     設定パネルの表示/非表示を切り替える。
    /// </summary>
    private void ToggleSettings()
    {
        _showSettings = !_showSettings;
    }

    /// <summary>
    ///     強い関連の閾値スライダー変更時。
    /// </summary>
    private async Task OnStrongThresholdChanged(float value)
    {
        StrongThreshold = value;
        // Strong が Candidate より小さくならないよう補正
        if (StrongThreshold < CandidateThreshold)
        {
            CandidateThreshold = StrongThreshold;
        }

        await OnThresholdsChanged.InvokeAsync((StrongThreshold, CandidateThreshold));
        await ReclassifyAndNotifyAsync();
    }

    /// <summary>
    ///     候補の閾値スライダー変更時。
    /// </summary>
    private async Task OnCandidateThresholdChanged(float value)
    {
        CandidateThreshold = value;
        // Candidate が Strong より大きくならないよう補正
        if (CandidateThreshold > StrongThreshold)
        {
            StrongThreshold = CandidateThreshold;
        }

        await OnThresholdsChanged.InvokeAsync((StrongThreshold, CandidateThreshold));
        await ReclassifyAndNotifyAsync();
    }

    /// <summary>
    ///     閾値を初期値に戻す。
    /// </summary>
    private async Task ResetThresholds()
    {
        StrongThreshold = SuggestedTag.DefaultStrongThreshold;
        CandidateThreshold = SuggestedTag.DefaultCandidateThreshold;
        await OnThresholdsChanged.InvokeAsync((StrongThreshold, CandidateThreshold));
        await ReclassifyAndNotifyAsync();
    }

    /// <summary>
    ///     提案されたタグ一覧をスコアおよび手動指定に基づき分類し、親へ確定タグID一覧を通知する。
    /// </summary>
    private async Task ReclassifyAndNotifyAsync()
    {
        _strongTags.Clear();
        _candidateTags.Clear();

        var availableSuggestions = Suggestions
            .Where(s => !ExcludedTagIds.Contains(s.TagId));

        foreach (var suggestion in availableSuggestions)
        {
            if (_manuallyPromotedTagIds.Contains(suggestion.TagId))
            {
                _strongTags.Add(suggestion);
            }
            else if (_manuallyRemovedTagIds.Contains(suggestion.TagId))
            {
                // 手動除外されたタグは、CandidateThreshold を満たしていれば候補側へ
                if (suggestion.Score >= CandidateThreshold)
                {
                    _candidateTags.Add(suggestion);
                }
            }
            else if (suggestion.Score >= StrongThreshold)
            {
                _strongTags.Add(suggestion);
            }
            else if (suggestion.Score >= CandidateThreshold)
            {
                _candidateTags.Add(suggestion);
            }
        }

        if (OnConfirmedTagIdsChanged.HasDelegate)
        {
            var confirmedIds = _strongTags.Select(t => t.TagId).ToList();
            await OnConfirmedTagIdsChanged.InvokeAsync(confirmedIds);
        }
    }
}