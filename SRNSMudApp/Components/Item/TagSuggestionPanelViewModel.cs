namespace SRNSMudApp.Components.Item;

using SRNSMudApp.Models;

/// <summary>
///     TagSuggestionPanel コンポーネントの状態と操作をカプセル化する ViewModel。
///     閾値制約は SuggestionThresholds 型により不変条件が保護される。
/// </summary>
public class TagSuggestionPanelViewModel
{
    private readonly HashSet<int> _manuallyPromotedTagIds = [];
    private readonly HashSet<int> _manuallyRemovedTagIds = [];
    private readonly List<SuggestedTag> _strongTags = [];
    private readonly List<SuggestedTag> _candidateTags = [];
    private HashSet<int> _excludedTagIds = [];
    private IReadOnlyList<SuggestedTag> _suggestions = [];

    public SuggestionThresholds Thresholds { get; private set; } = SuggestionThresholds.Default;

    public float StrongThreshold => Thresholds.Strong;
    public float CandidateThreshold => Thresholds.Candidate;

    public IReadOnlyList<SuggestedTag> StrongTags => _strongTags;
    public IReadOnlyList<SuggestedTag> CandidateTags => _candidateTags;
    public IReadOnlySet<int> ConfirmedTagIds => _strongTags.Select(t => t.TagId).ToHashSet();
    public bool HasAnySuggestions => _strongTags.Count > 0 || _candidateTags.Count > 0;
    public bool ShowSettings { get; private set; }

    public void InitializeThresholds(float? initialStrong, float? initialCandidate)
    {
        var strong = initialStrong ?? SuggestionThresholds.Default.Strong;
        var candidate = initialCandidate ?? SuggestionThresholds.Default.Candidate;
        Thresholds = new SuggestionThresholds(strong, candidate);
    }

    public void ToggleSettings() => ShowSettings = !ShowSettings;

    public void SetExcludedTagIds(IEnumerable<int>? excludedTagIds)
    {
        _excludedTagIds = excludedTagIds != null ? [.. excludedTagIds] : [];
        Reclassify();
    }

    public void SetSuggestions(IReadOnlyList<SuggestedTag>? suggestions)
    {
        _suggestions = suggestions ?? [];
        _manuallyPromotedTagIds.Clear();
        _manuallyRemovedTagIds.Clear();
        Reclassify();
    }

    public void PromoteCandidateTag(int tagId)
    {
        _manuallyPromotedTagIds.Add(tagId);
        _manuallyRemovedTagIds.Remove(tagId);
        Reclassify();
    }

    public void RemoveStrongTag(int tagId)
    {
        _manuallyRemovedTagIds.Add(tagId);
        _manuallyPromotedTagIds.Remove(tagId);
        Reclassify();
    }

    public void ChangeStrongThreshold(float value)
    {
        Thresholds = Thresholds.WithStrong(value);
        Reclassify();
    }

    public void ChangeCandidateThreshold(float value)
    {
        Thresholds = Thresholds.WithCandidate(value);
        Reclassify();
    }

    public void ResetThresholds()
    {
        Thresholds = SuggestionThresholds.Default;
        Reclassify();
    }

    private void Reclassify()
    {
        _strongTags.Clear();
        _candidateTags.Clear();

        var available = _suggestions.Where(s => !_excludedTagIds.Contains(s.TagId));

        foreach (var suggestion in available)
        {
            if (_manuallyPromotedTagIds.Contains(suggestion.TagId))
            {
                _strongTags.Add(suggestion);
            }
            else if (_manuallyRemovedTagIds.Contains(suggestion.TagId))
            {
                if (suggestion.Score >= Thresholds.Candidate)
                {
                    _candidateTags.Add(suggestion);
                }
            }
            else if (suggestion.Score >= Thresholds.Strong)
            {
                _strongTags.Add(suggestion);
            }
            else if (suggestion.Score >= Thresholds.Candidate)
            {
                _candidateTags.Add(suggestion);
            }
        }
    }
}