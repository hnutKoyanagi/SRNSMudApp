using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

using SRNSMudApp.Data;

namespace SRNSMudApp.Services;

/// <summary>
///     新規タグの親タグ階層をベクトル類似度およびLLM（Chrome Built-in AI / Prompt API または Gemini）を用いて判定・サジェストするサービス。
/// </summary>
public interface ITagHierarchyService
{
    /// <summary>
    ///     対象タグの親となる候補タグ（Level >= 2）をベクトル類似度とLLM（Chrome Prompt API / Geminiフォールバック）で判定してサジェストする。
    /// </summary>
    /// <param name="newTagName">新規作成するタグ名。</param>
    /// <param name="jsRuntime">クライアント側の IJSRuntime（window.ai 呼び出し用。null の場合はフォールバック）。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>サジェストされた親タグ（見つからない場合は null）。</returns>
    Task<Tag?> SuggestParentTagAsync(
        string newTagName,
        IJSRuntime? jsRuntime,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     指定した親タグの直下に配置するための新しい HierarchyId を算出する。
    /// </summary>
    /// <param name="parentTagId">親タグの ID。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>新規ノードの HierarchyId。</returns>
    Task<HierarchyId> DetermineNewNodeAsync(int parentTagId, CancellationToken cancellationToken = default);
}