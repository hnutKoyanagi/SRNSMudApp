using SRNSMudApp.Models;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     TagLinkReplaceDialog の状態管理、タグ検索、バリデーション、および決定モデル生成を担当する ViewModel。
///     UI（Blazor / bUnit）から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class TagLinkReplaceViewModel
{
    private readonly ITaggingImportDataProvider _dataProvider;

    public TagLinkReplaceViewModel(ITaggingImportDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public string OriginalMatchText { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public TagEntity? FirstCandidateTag { get; set; }

    public TagLinkReplaceAction Action { get; set; } = TagLinkReplaceAction.ReplaceWithFirstCandidate;
    public TagEntity? SelectedCustomTag { get; set; }

    public bool CanSubmit
    {
        get
        {
            if (Action == TagLinkReplaceAction.DoNotReplace) return true;
            if (Action == TagLinkReplaceAction.ReplaceWithFirstCandidate) return FirstCandidateTag != null;
            if (Action == TagLinkReplaceAction.ReplaceWithSelectedCandidate) return SelectedCustomTag != null;
            return false;
        }
    }

    /// <summary>
    ///     第1候補タグの有無に応じて初期アクションを設定する。
    /// </summary>
    public void SetCandidate(TagEntity? firstCandidate)
    {
        FirstCandidateTag = firstCandidate;
        if (firstCandidate == null)
        {
            Action = TagLinkReplaceAction.DoNotReplace;
        }
        else
        {
            Action = TagLinkReplaceAction.ReplaceWithFirstCandidate;
        }
    }

    /// <summary>
    ///     置換先タグを非同期検索する。
    /// </summary>
    public async Task<IEnumerable<TagEntity>> SearchTagsAsync(string? value, CancellationToken cancellationToken = default)
    {
        return await _dataProvider.SearchTagsAsync(value, cancellationToken);
    }

    /// <summary>
    ///     現在の選択状態から置き換え決定モデル（TagLinkReplaceDecision）を生成する。
    /// </summary>
    public TagLinkReplaceDecision CreateDecision()
    {
        int? tagId = Action switch
        {
            TagLinkReplaceAction.ReplaceWithFirstCandidate => FirstCandidateTag?.Id,
            TagLinkReplaceAction.ReplaceWithSelectedCandidate => SelectedCustomTag?.Id,
            TagLinkReplaceAction.DoNotReplace => null,
            _ => null
        };

        return new TagLinkReplaceDecision(Action, tagId);
    }

    /// <summary>
    ///     スキップ（置き換えない）決定モデルを生成する。
    /// </summary>
    public TagLinkReplaceDecision CreateSkipDecision()
    {
        return new TagLinkReplaceDecision(TagLinkReplaceAction.DoNotReplace, null);
    }
}