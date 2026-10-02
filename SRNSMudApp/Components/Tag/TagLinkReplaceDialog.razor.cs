namespace SRNSMudApp.Components.Tag;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;

/// <summary>
///     タグ内部リンク置き換え確認ダイアログコンポーネントのコードビハインド。
/// </summary>
public partial class TagLinkReplaceDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private TagLinkReplaceViewModel ViewModel { get; set; } = null!;

    [Parameter]
    public string OriginalMatchText { get; set; } = string.Empty;

    [Parameter]
    public string TagName { get; set; } = string.Empty;

    [Parameter]
    public Tag? FirstCandidateTag { get; set; }

    protected override void OnParametersSet()
    {
        ViewModel.OriginalMatchText = OriginalMatchText;
        ViewModel.TagName = TagName;
        ViewModel.SetCandidate(FirstCandidateTag);
    }

    private async Task<IEnumerable<Tag>> SearchTagsAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchTagsAsync(value, token);
    }

    private void Skip()
    {
        MudDialog.Close(DialogResult.Ok(ViewModel.CreateSkipDecision()));
    }

    private void Submit()
    {
        MudDialog.Close(DialogResult.Ok(ViewModel.CreateDecision()));
    }
}