namespace SRNSMudApp.Components.UI;

using Microsoft.AspNetCore.Components;

using MudBlazor;

/// <summary>
///     リクエスト却下理由入力ダイアログコンポーネントのコードビハインド。
///     却下理由コメントの入力と却下操作を制御する。
/// </summary>
public partial class RejectRequestDialog : ComponentBase
{
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private string? Comment { get; set; }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private void Submit()
    {
        MudDialog.Close(DialogResult.Ok(Comment ?? string.Empty));
    }
}