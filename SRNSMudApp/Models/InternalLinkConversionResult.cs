namespace SRNSMudApp.Models;

/// <summary>
///     内部リンク変換の検出結果。
///     自動置換対象と手動確認対象に分類された候補リストを保持する。
/// </summary>
/// <param name="AutoReplaceCandidates">閾値以上の類似度で自動置換される候補</param>
/// <param name="ManualCandidates">閾値未満だがある程度一致しており、ユーザー確認が必要な候補</param>
public sealed record InternalLinkConversionResult(
    IReadOnlyList<LinkConversionCandidate> AutoReplaceCandidates,
    IReadOnlyList<LinkConversionCandidate> ManualCandidates)
{
    /// <summary>候補が1件もないかどうか</summary>
    public bool IsEmpty => AutoReplaceCandidates.Count == 0 && ManualCandidates.Count == 0;
}