namespace SRNSMudApp.Models;

/// <summary>
///     テキスト中で検出された内部リンク変換候補。
///     元テキストの位置情報・一致したタグ情報・類似度スコアを保持し、
///     閾値に基づく自動置換かユーザー確認かを判別する。
/// </summary>
/// <param name="OriginalText">元のテキスト断片</param>
/// <param name="TagId">一致したタグID</param>
/// <param name="TagName">一致したタグ名</param>
/// <param name="Similarity">正規化レーベンシュタイン類似度スコア (0.0f〜1.0f)</param>
/// <param name="StartIndex">テキスト中の開始位置</param>
/// <param name="Length">テキスト中の長さ</param>
/// <param name="IsAutoReplace">閾値以上で自動置換対象かどうか</param>
public sealed record LinkConversionCandidate(
    string OriginalText,
    int TagId,
    string TagName,
    float Similarity,
    int StartIndex,
    int Length,
    bool IsAutoReplace)
{
    /// <summary>既定の自動置換類似度閾値（85%一致以上で自動置換）</summary>
    public const float DefaultAutoReplaceThreshold = 0.85f;
}