namespace SRNSMudApp.Components.UI;

using Microsoft.AspNetCore.Components;

using MudBlazor;

/// <summary>
///     タグ重み編集ダイアログコンポーネントのコードビハインド。
///     重み数値の編集と確定操作を制御する。
/// </summary>
public partial class WeightEditDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int Weight { get; set; }

    private void Submit()
    {
        MudDialog.Close(DialogResult.Ok(Weight));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}