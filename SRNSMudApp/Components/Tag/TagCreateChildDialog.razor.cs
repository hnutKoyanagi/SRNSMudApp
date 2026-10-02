namespace SRNSMudApp.Components.Tag;

using Microsoft.AspNetCore.Components;

using MudBlazor;

/// <summary>
///     子タグ作成ダイアログコンポーネントのコードビハインド。
///     タグ名・内容の入力と結果返却を制御する。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
    Justification = "ダイアログ結果型として既存の呼び出し元との互換性を維持するため入れ子型を維持する")]
public partial class TagCreateChildDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private string _name = string.Empty;
    private string _content = string.Empty;

    public record Result(string Name, string Content);

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(new Result(_name, _content ?? string.Empty)));
    }
}