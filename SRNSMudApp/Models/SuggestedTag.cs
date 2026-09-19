namespace SRNSMudApp.Models;

/// <summary>
///     コンテンツ分析に基づくタグ提案の1件分を表すモデル。
///     タグ情報とコサイン類似度スコアを保持する。
/// </summary>
/// <param name="TagId">タグID</param>
/// <param name="TagName">タグ名</param>
/// <param name="Score">コサイン類似度スコア (-1.0f 〜 1.0f)</param>
public sealed record SuggestedTag(int TagId, string TagName, float Score)
{
    /// <summary>既定の強い関連（自動関連付け）の閾値</summary>
    public const float DefaultStrongThreshold = 0.65f;

    /// <summary>既定の候補（推薦）の閾値</summary>
    public const float DefaultCandidateThreshold = 0.40f;
}

