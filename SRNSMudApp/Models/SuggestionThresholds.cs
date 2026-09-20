namespace SRNSMudApp.Models;

/// <summary>
///     タグ提案の類似度閾値ペア（強い関連 / 候補推薦）を表す値オブジェクト。
///     StrongThreshold >= CandidateThreshold かつ 0.0f 〜 1.0f の範囲制約を型レベルで保証する。
/// </summary>
public readonly record struct SuggestionThresholds
{
    public float Strong { get; }
    public float Candidate { get; }

    public SuggestionThresholds(float strong, float candidate)
    {
        var s = Math.Clamp(strong, 0.0f, 1.0f);
        var c = Math.Clamp(candidate, 0.0f, 1.0f);
        if (s < c)
        {
            c = s;
        }
        Strong = s;
        Candidate = c;
    }

    public static SuggestionThresholds Default =>
        new(SuggestedTag.DefaultStrongThreshold, SuggestedTag.DefaultCandidateThreshold);

    /// <summary>
    ///     強い関連の閾値を変更し、必要に応じて候補閾値を押し下げる。
    /// </summary>
    public SuggestionThresholds WithStrong(float newStrong)
    {
        var s = Math.Clamp(newStrong, 0.0f, 1.0f);
        return new SuggestionThresholds(s, Math.Min(Candidate, s));
    }

    /// <summary>
    ///     候補推薦の閾値を変更し、必要に応じて強い関連の閾値を押し上げる。
    /// </summary>
    public SuggestionThresholds WithCandidate(float newCandidate)
    {
        var c = Math.Clamp(newCandidate, 0.0f, 1.0f);
        return new SuggestionThresholds(Math.Max(Strong, c), c);
    }
}