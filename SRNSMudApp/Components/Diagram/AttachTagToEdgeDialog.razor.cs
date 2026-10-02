namespace SRNSMudApp.Components.Diagram;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

using TagEntity = SRNSMudApp.Data.Tag;

/// <summary>
///     エッジへのタグ紐付けダイアログコンポーネント。
///     ドメインロジックおよびバリデーションは <see cref="AttachTagToEdgeViewModel"/> に委譲する。
/// </summary>
public partial class AttachTagToEdgeDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public TagEdge Edge { get; set; } = null!;

    [Parameter]
    public string CurrentUserId { get; set; } = "";

    [Parameter]
    public IReadOnlyList<TagEntity> AvailableTags { get; set; } = [];

    [Inject]
    private AttachTagToEdgeViewModel ViewModel { get; set; } = null!;

    protected override void OnInitialized()
    {
        ViewModel.Initialize(Edge, CurrentUserId, AvailableTags);
    }

    private async Task OnTagSelected(TagEntity? tag)
    {
        await ViewModel.SelectTagAsync(tag);
    }

    private Task<IEnumerable<TagEntity>> SearchTags(string value, CancellationToken token)
    {
        return Task.FromResult(ViewModel.SearchTags(value));
    }

    private void Cancel() => MudDialog.Cancel();

    private void Submit()
    {
        Result<(int TagId, int RightAssetId, int Weight)> result = ViewModel.Submit();
        switch (result)
        {
            case Success<(int TagId, int RightAssetId, int Weight)> s:
                MudDialog.Close(DialogResult.Ok(s.Value));
                break;
            default:
                break;
        }
    }
}