using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

using ItemEntity = SRNSMudApp.Data.Item;

namespace SRNSMudApp.Components.Diagram;

/// <summary>
///     ItemNodeWidget で使用される表示計算およびプレビュー生成ロジックを提供する ViewModel。
///     UI 非依存の純粋なロジックとして切り出すことで、xUnit での単体テストを可能にする（SRP 準拠）。
/// </summary>
public static partial class ItemNodeViewModel
{
    /// <summary>
    ///     未展開時プレビューテキストの既定最大文字数。
    /// </summary>
    public const int DefaultMaxPreviewLength = 80;

    /// <summary>
    ///     プレビュー用説明文の最大文字数。
    /// </summary>
    public const int MaxDescriptionLength = 200;

    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    ///     HTML タグを除去したクリーンなプレビュー用テキストを返す。
    /// </summary>
    /// <param name="text">HTML を含む可能性のある入力テキスト。</param>
    /// <returns>HTML タグが除去されトリムされたプレーンテキスト。</returns>
    public static string StripHtmlTags(string? text) =>
        string.IsNullOrEmpty(text) ? "" : HtmlTagRegex().Replace(text, string.Empty).Trim();

    /// <summary>
    ///     本文テキストをセグメントに分割し、指定文字数（デフォルト 80 文字）で切り詰めたセグメント一覧を返す。
    ///     URL セグメントは途中で不自然に分断されないように保持される。
    /// </summary>
    /// <param name="rawContent">生の本文テキスト。</param>
    /// <param name="maxLength">最大文字数。</param>
    /// <returns>切り詰め処理後のコンテンツセグメント一覧。</returns>
    public static IReadOnlyList<ContentSegment> GetTruncatedSegments(string? rawContent, int maxLength = DefaultMaxPreviewLength)
    {
        var cleanContent = StripHtmlTags(rawContent);
        var segments = ItemCardViewModel.GetContentSegments(cleanContent);

        var result = new List<ContentSegment>();
        int currentLength = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.IsUrl)
            {
                result.Add(segment);
                currentLength += segment.Text.Length;
                if (currentLength >= maxLength)
                {
                    if (i < segments.Count - 1)
                    {
                        result.Add(new ContentSegment("...", false));
                    }
                    break;
                }
            }
            else
            {
                int remaining = maxLength - currentLength;
                if (segment.Text.Length > remaining)
                {
                    result.Add(new ContentSegment(segment.Text[..remaining] + "...", false));
                    break;
                }

                result.Add(segment);
                currentLength += segment.Text.Length;
            }
        }

        return result;
    }

    /// <summary>
    ///     Item エンティティから内部リンクプレビュー用の LinkPreviewData を生成する（Factory Method）。
    /// </summary>
    /// <param name="contextItem">プレビューの元データとなる Item エンティティ。</param>
    /// <param name="url">プレビュー対象の URL。</param>
    /// <returns>生成された <see cref="LinkPreviewData"/> インスタンス。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Internal links are relative paths")]
    public static LinkPreviewData CreateLinkPreviewData(ItemEntity contextItem, string url)
    {
        ArgumentNullException.ThrowIfNull(contextItem);
        ArgumentNullException.ThrowIfNull(url);

        var text = StripHtmlTags(contextItem.Content ?? "");
        if (text.Length > MaxDescriptionLength)
        {
            text = $"{text.AsSpan(0, MaxDescriptionLength)}...";
        }

        return new LinkPreviewData
        {
            Url = url,
            Title = $"Item #{contextItem.Id}",
            Description = text,
            Tags = contextItem.TagRelations?
                .Where(tr => tr.Tag != null && !tr.Tag.IsSystem)
                .OrderByDescending(tr => tr.Weight)
                .Select(tr => new TagPreviewItem(tr.Tag.Id, tr.Tag.Name, tr.Tag.Owner?.UserName ?? "unknown", tr.Weight))
                .ToList() ?? [],
            SiteName = "SRNSMudApp",
            IsSuccess = true
        };
    }

    /// <summary>
    ///     URL に対するリンクプレビューデータを解決する。
    ///     カスタムローダーが指定されている場合はそれを優先し、コンテキスト内に存在するアイテムであれば
    ///     DB クエリを行わずに即時生成し、それ以外は <see cref="ILinkPreviewService"/> に委譲する。
    /// </summary>
    /// <param name="url">解決対象の URL。</param>
    /// <param name="contextItems">ダイアグラム上のコンテキストに含まれる Item 一覧。</param>
    /// <param name="previewService">外部プレビュー解決用のサービス（null 許容）。</param>
    /// <param name="customLoader">テストや上書き用のカスタムローダーデリゲート（null 許容）。</param>
    /// <returns>解決された <see cref="LinkPreviewData"/>。解決できなかった場合は null。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Internal links are relative paths")]
    public static async Task<LinkPreviewData?> ResolvePreviewAsync(
        string url,
        IReadOnlyList<ItemEntity> contextItems,
        ILinkPreviewService? previewService = null,
        Func<string, Task<LinkPreviewData?>>? customLoader = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(contextItems);

        if (customLoader != null)
        {
            return await customLoader(url);
        }

        if (url.StartsWith("/ItemDetail/", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(url.AsSpan("/ItemDetail/".Length), out var itemId))
        {
            var contextItem = contextItems.FirstOrDefault(i => i.Id == itemId);
            if (contextItem != null)
            {
                return CreateLinkPreviewData(contextItem, url);
            }
        }

        return previewService != null
            ? await previewService.GetPreviewAsync(url)
            : null;
    }
}