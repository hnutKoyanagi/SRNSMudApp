using SRNSMudApp.Models;

namespace SRNSMudApp.Services;

/// <summary>
///     コンテンツに基づいてタグの提案・類似度計算を行うサービスインターフェース。
/// </summary>
public interface ITagSuggestionService
{
    /// <summary>
    ///     コンテンツのテキスト埋め込みベクトルと既存タグの埋め込みベクトルの類似度を計算し、
    ///     閾値以上のスコアを持つタグ提案一覧をスコア降順で返す。
    /// </summary>
    /// <param name="content">入力コンテンツテキスト</param>
    /// <param name="minScore">取得する最小スコア閾値（デフォルトは候補閾値）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>スコア降順にソートされたタグ提案リスト</returns>
    Task<IReadOnlyList<SuggestedTag>> SuggestTagsAsync(
        string content,
        float minScore = SuggestedTag.DefaultCandidateThreshold,
        CancellationToken cancellationToken = default);
}

