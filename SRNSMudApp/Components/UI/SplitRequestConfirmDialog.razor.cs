namespace SRNSMudApp.Components.UI;

using Microsoft.AspNetCore.Components;

using MudBlazor;

/// <summary>
///     テキスト分割リクエスト確認ダイアログコンポーネントのコードビハインド。
///     選択テキストのプレビュー表示とリクエスト送信承認を制御する。
/// </summary>
public partial class SplitRequestConfirmDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string SelectedText { get; set; } = string.Empty;

    private void Cancel() => MudDialog.Cancel();

    private void Submit() => MudDialog.Close(DialogResult.Ok(true));
}