namespace SRNSMudApp.Components.UI;

using Microsoft.AspNetCore.Components;

using MudBlazor;

/// <summary>
///     汎用削除確認ダイアログコンポーネントのコードビハインド。
///     確認メッセージの表示と削除承認・キャンセル操作を制御する。
/// </summary>
public partial class ConfirmDeleteDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    ///     ダイアログ本文に表示する確認メッセージ。
    /// </summary>
    [Parameter]
    public string ContentText { get; set; } = "削除してもよろしいですか？この操作は取り消せません。";

    private void Cancel() => MudDialog.Cancel();

    private void Submit() => MudDialog.Close(DialogResult.Ok(true));
}