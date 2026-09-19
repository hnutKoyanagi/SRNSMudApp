#region

using SRNSMudApp.Models;

#endregion

namespace SRNSMudApp.Services;

/// <summary>
///     テキスト中のタグ名候補を検出し、内部リンク (/TagDetail/{id}) への変換を行うサービスの契約。
/// </summary>
public interface IInternalLinkConversionService
{
    /// <summary>
    ///     テキスト中のタグ名に一致する部分を検出し、変換候補を返す。
    ///     autoReplaceThreshold 以上の類似度を持つ候補は <see cref="LinkConversionCandidate.IsAutoReplace"/> = true。
    /// </summary>
    /// <param name="content">走査対象のテキスト</param>
    /// <param name="autoReplaceThreshold">自動置換する類似度閾値</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>自動置換候補と手動確認候補に分類された検出結果</returns>
    Task<InternalLinkConversionResult> DetectLinkCandidatesAsync(
        string content,
        float autoReplaceThreshold = LinkConversionCandidate.DefaultAutoReplaceThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     確定した変換候補に基づきテキストを置換する。
    ///     候補は StartIndex 降順で適用し、位置ずれを防ぐ。
    /// </summary>
    /// <param name="content">元テキスト</param>
    /// <param name="candidates">適用する変換候補リスト</param>
    /// <returns>変換後のテキスト</returns>
    string ApplyReplacements(string content, IReadOnlyList<LinkConversionCandidate> candidates);
}