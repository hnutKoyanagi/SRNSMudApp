namespace SRNSMudApp.Components.Item;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

using SRNSMudApp.Models.Unions;

/// <summary>
///     CSVアイテムインポートコンポーネントのコードビハインド。
///     CSVファイルの読み込み、サイズ検証、およびインポート処理を制御する。
/// </summary>
public partial class ItemImport : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthState { get; set; } = default!;

    [Parameter]
    public EventCallback OnImportCompleted { get; set; }

    [Inject]
    private ItemImportViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var authState = await AuthState;
            ViewModel.CurrentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "ファイル読み込み時の予期せぬ例外を捕捉し、ユーザー向けエラーメッセージとして通知するため")]
    private async Task OnCsvUpload(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file.Size > ItemImportViewModel.MaxFileSizeBytes)
        {
            Snackbar.Add("ファイルサイズが大きすぎます。5MB以下のファイルを選択してください。", Severity.Error);
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(ItemImportViewModel.MaxFileSizeBytes);
            var result = await ViewModel.ImportCsvAsync(stream, file.Size);

            switch (result)
            {
                case Success<int> s:
                    Snackbar.Add($"{s.Value}件のアイテムをインポートしました。", Severity.Success);
                    await OnImportCompleted.InvokeAsync();
                    break;
                case Failure f:
                    var severity = f.ErrorMessage.Contains("ありませんでした", StringComparison.Ordinal)
                        ? Severity.Info
                        : Severity.Error;
                    Snackbar.Add(f.ErrorMessage, severity);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
        }
    }
}