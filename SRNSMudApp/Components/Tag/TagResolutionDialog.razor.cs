namespace SRNSMudApp.Components.Tag;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     未登録タグの解決ダイアログコンポーネントのコードビハインド。
///     既存タグ割り当て、または新規作成時の親タグ決定・サジェスト選択の UI を制御する。
///     候補検索・決定ロジックは <see cref="TagResolutionViewModel"/> に委譲する。
/// </summary>
public partial class TagResolutionDialog : ComponentBase
{
    [Inject] private TagResolutionViewModel ViewModel { get; set; } = null!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public string TagName { get; set; } = string.Empty;

    [Parameter] public string TagKind { get; set; } = "UserCustomTag";

    [Parameter] public Tag? SuggestedParentTag { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ViewModel.TagKind = TagKind;
        await ViewModel.InitializeAsync(TagName, SuggestedParentTag);
    }

    private void OnSuggestionSelected(Tag? tag)
    {
        ViewModel.SelectedExistingTag = tag;
    }

    private void OnParentSuggestionSelected(Tag? tag)
    {
        ViewModel.SelectedCustomParentTag = tag;
    }

    private void OnUseSuggestedParentChanged(bool useSuggested)
    {
        ViewModel.SetUseSuggestedParent(useSuggested);
    }

    private async Task<IEnumerable<Tag>> SearchParentCandidateTagsAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchParentCandidateTagsAsync(value, token);
    }

    private async Task<IEnumerable<Tag>> SearchExistingTagsAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchExistingTagsAsync(value, token);
    }

    private void Submit()
    {
        MudDialog.Close(DialogResult.Ok(ViewModel.CreateDecision()));
    }

    private void Skip()
    {
        MudDialog.Close(DialogResult.Ok(ViewModel.CreateSkipDecision()));
    }
}