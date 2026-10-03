using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace SRNSMudApp.Components.Item;

/// <summary>
///     モバイル表示等で全画面表示されるアイテム追加ダイアログ。
///     上部バーに閉じるボタンと保存ボタンを配置し、Twitter風のUXを提供する。
/// </summary>
public partial class AddItemDialog : ComponentBase
{
    [CascadingParameter]
    protected internal IMudDialogInstance MudDialog { get; set; } = null!;

    protected internal static string FormId => "add-item-dialog-form";

    protected internal void Cancel() => MudDialog.Cancel();

    protected internal void HandleItemAdded() => MudDialog.Close(DialogResult.Ok(true));
}