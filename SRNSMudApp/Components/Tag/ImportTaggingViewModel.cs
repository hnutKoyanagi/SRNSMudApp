#region

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.JSInterop;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     タグ付け構造インポート (ImportTagging.razor) の JSON パース、タグ解決調整、内部リンク置換、永続化コーディネーションを担当する ViewModel。
/// </summary>
public sealed class ImportTaggingViewModel
{
    private static readonly Regex LinkRegex = new(@"\[(?<label>[^\]]+)\]\((?<url>https?://[^\)]+)\)", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITaggingImportDataProvider _dataProvider;
    private readonly ITagHierarchyService _hierarchyService;
    private readonly IDialogLauncher _dialogLauncher;

    public ImportTaggingViewModel(
        ITaggingImportDataProvider dataProvider,
        ITagHierarchyService hierarchyService,
        IDialogLauncher dialogLauncher)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _hierarchyService = hierarchyService ?? throw new ArgumentNullException(nameof(hierarchyService));
        _dialogLauncher = dialogLauncher ?? throw new ArgumentNullException(nameof(dialogLauncher));
    }

    public string JsonText { get; set; } = string.Empty;
    public bool IsProcessing { get; private set; }
    public string CurrentStatusText { get; private set; } = "処理中...";
    public string? CurrentUserId { get; private set; }
    public TaggingImportResult? ImportResult { get; private set; }
    public int TotalNewTagsCreated { get; private set; }

    public bool CanStart => !IsProcessing && !string.IsNullOrWhiteSpace(JsonText) && !string.IsNullOrWhiteSpace(CurrentUserId);

    public void Initialize(ClaimsPrincipal? user)
    {
        CurrentUserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    ///     Markdown リンク記号 [label](url) をダイアログ決定に応じて置換またはラベル残置します。
    /// </summary>
    public static string ReplaceLink(string content, string matchValue, string label, TagLinkReplaceDecision? decision)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(matchValue);
        ArgumentNullException.ThrowIfNull(label);

        if (decision is { Action: not TagLinkReplaceAction.DoNotReplace, SelectedTagId: not null })
        {
            var replacement = $"/TagDetail/{decision.SelectedTagId.Value}";
            return content.Replace(matchValue, replacement, StringComparison.Ordinal);
        }

        return content.Replace(matchValue, label, StringComparison.Ordinal);
    }

    /// <summary>
    ///     JSON テキストをパースしてペイロードを抽出します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JSON解析エラーを捕捉してFailureを返すため")]
    public static Result<TaggingImportPayload> ParsePayload(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return new Failure("JSONテキストが空です。");
        }

        try
        {
            var root = JsonSerializer.Deserialize<TaggingImportRoot>(jsonText, JsonOptions);

            if (root?.TaggingRequestEntity == null)
            {
                return new Failure("有効な TaggingRequestEntity が含まれていません。");
            }

            return new Success<TaggingImportPayload>(root.TaggingRequestEntity);
        }
        catch (Exception ex)
        {
            return new Failure($"JSONの解析に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    ///     インポート処理を一括実行します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "インポート処理全体の失敗を捕捉してUIへエラー結果を返すため")]
    public async Task<Result<TaggingImportResult>> ExecuteImportAsync(IJSRuntime jsRuntime, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);

        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return new Failure("ログインが必要です。");
        }

        var parseResult = ParsePayload(JsonText);
        TaggingImportPayload payload;
        switch (parseResult)
        {
            case Success<TaggingImportPayload> success:
                payload = success.Value;
                break;
            case Failure fail:
                return fail;
            default:
                return new Failure("JSONの解析に失敗しました。");
        }

        IsProcessing = true;
        TotalNewTagsCreated = 0;
        ImportResult = null;

        try
        {
            // 1. タグの検証・解決・親階層決定
            CurrentStatusText = "タグの検証・解決中...";
            var relationIdToTagMap = new Dictionary<string, int>();

            foreach (var relDto in payload.TagRelations)
            {
                bool isSystem = string.Equals(relDto.Tag.TagKind, "SystemClassificationTag", StringComparison.OrdinalIgnoreCase);
                var existingTag = await _dataProvider.FindExistingTagAsync(relDto.Tag.Name, CurrentUserId, isSystem, cancellationToken);

                if (existingTag != null)
                {
                    relationIdToTagMap[relDto.RelationId] = existingTag.Id;
                }
                else
                {
                    CurrentStatusText = $"タグ「{relDto.Tag.Name}」の階層サジェストを生成中...";
                    var suggestedParent = await _hierarchyService.SuggestParentTagAsync(relDto.Tag.Name, jsRuntime, cancellationToken);

                    var parameters = new DialogParameters
                    {
                        { nameof(TagResolutionDialog.TagName), relDto.Tag.Name },
                        { nameof(TagResolutionDialog.TagKind), relDto.Tag.TagKind },
                        { nameof(TagResolutionDialog.SuggestedParentTag), suggestedParent }
                    };

                    var dialog = await _dialogLauncher.ShowAsync<TagResolutionDialog>(
                        $"未登録タグの解決: {relDto.Tag.Name}", parameters);
                    var dialogResult = await dialog.Result;

                    if (dialogResult is { Canceled: false, Data: TagResolutionDecision decision })
                    {
                        if (decision.Action == TagResolutionAction.CreateNew)
                        {
                            var createdTag = await _dataProvider.CreateTagAsync(
                                relDto.Tag.Name, CurrentUserId, isSystem, decision.SelectedParentTagId, cancellationToken);
                            relationIdToTagMap[relDto.RelationId] = createdTag.Id;
                            TotalNewTagsCreated++;
                        }
                        else if (decision.Action == TagResolutionAction.UseExisting && decision.SelectedExistingTagId.HasValue)
                        {
                            relationIdToTagMap[relDto.RelationId] = decision.SelectedExistingTagId.Value;
                        }
                    }
                }
            }

            // TagEdges の AppliedTags に含まれるタグも同様に解決
            foreach (var edgeDto in payload.TagEdges)
            {
                foreach (var appliedTagName in edgeDto.AppliedTags)
                {
                    if (string.IsNullOrWhiteSpace(appliedTagName)) continue;

                    var existingTag = await _dataProvider.FindExistingTagAsync(appliedTagName, CurrentUserId, false, cancellationToken);
                    if (existingTag == null)
                    {
                        var suggestedParent = await _hierarchyService.SuggestParentTagAsync(appliedTagName, jsRuntime, cancellationToken);
                        var parameters = new DialogParameters
                        {
                            { nameof(TagResolutionDialog.TagName), appliedTagName },
                            { nameof(TagResolutionDialog.TagKind), "UserCustomTag" },
                            { nameof(TagResolutionDialog.SuggestedParentTag), suggestedParent }
                        };

                        var dialog = await _dialogLauncher.ShowAsync<TagResolutionDialog>(
                            $"エッジ意味付けタグの解決: {appliedTagName}", parameters);
                        var dialogResult = await dialog.Result;

                        if (dialogResult is { Canceled: false, Data: TagResolutionDecision decision }
                            && decision.Action == TagResolutionAction.CreateNew)
                        {
                            _ = await _dataProvider.CreateTagAsync(appliedTagName, CurrentUserId, false, decision.SelectedParentTagId, cancellationToken);
                            TotalNewTagsCreated++;
                        }
                    }
                }
            }

            // 2. Item 内の内部リンク置き換え
            CurrentStatusText = "内部リンク置き換えを確認中...";
            var processedContents = new List<string>();

            foreach (var item in payload.Item)
            {
                var content = item.Content;
                var matches = LinkRegex.Matches(content);

                foreach (Match match in matches)
                {
                    var label = match.Groups["label"].Value;
                    var url = match.Groups["url"].Value;

                    var matchedRelId = payload.TagRelations
                        .FirstOrDefault(r => url.Contains(r.RelationId, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Tag.Name, label, StringComparison.OrdinalIgnoreCase))
                        ?.RelationId;

                    TagEntity? firstCandidateTag = null;
                    if (matchedRelId != null && relationIdToTagMap.TryGetValue(matchedRelId, out int resolvedTagId))
                    {
                        var existingTags = await _dataProvider.SearchTagsAsync(label, cancellationToken);
                        firstCandidateTag = existingTags.FirstOrDefault(t => t.Id == resolvedTagId)
                            ?? (await _dataProvider.SearchTagsAsync(null, cancellationToken)).FirstOrDefault(t => t.Id == resolvedTagId);
                    }

                    var dialogParams = new DialogParameters
                    {
                        { nameof(TagLinkReplaceDialog.OriginalMatchText), match.Value },
                        { nameof(TagLinkReplaceDialog.TagName), label },
                        { nameof(TagLinkReplaceDialog.FirstCandidateTag), firstCandidateTag }
                    };

                    var dialog = await _dialogLauncher.ShowAsync<TagLinkReplaceDialog>(
                        $"リンク置換確認: {label}", dialogParams);
                    var dialogResult = await dialog.Result;

                    TagLinkReplaceDecision? decision = dialogResult is { Canceled: false, Data: TagLinkReplaceDecision d } ? d : null;
                    content = ReplaceLink(content, match.Value, label, decision);
                }

                processedContents.Add(content);
            }

            // 3. インポートトランザクション実行
            CurrentStatusText = "データベースへ永続化中...";
            ImportResult = await _dataProvider.ExecuteImportAsync(
                CurrentUserId,
                payload,
                relationIdToTagMap,
                processedContents,
                cancellationToken);

            return new Success<TaggingImportResult>(ImportResult);
        }
        catch (Exception ex)
        {
            return new Failure($"インポート処理中にエラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }
}