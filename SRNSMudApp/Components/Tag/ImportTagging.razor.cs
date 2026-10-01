namespace SRNSMudApp.Components.Tag;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

using MudBlazor;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     タグ付け構造インポート (JSON) 画面コンポーネントのコードビハインド。
/// </summary>
public partial class ImportTagging : ComponentBase
{
    private const string PlaceholderJson = """
    {
      "TaggingRequestEntity": {
        "Item": [...],
        "TagRelations": [...],
        "TagEdges": [...]
      }
    }
    """;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    [Inject]
    private ImportTaggingViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    private IBrowserFile? _file;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var auth = await AuthState;
            ViewModel.Initialize(auth.User);
        }
    }

    [SuppressMessage("Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Display error notification to user")]
    private async Task OnFileSelected(IBrowserFile? file)
    {
        _file = file;
        if (file is not null)
        {
            try
            {
                using var stream = file.OpenReadStream(10 * 1024 * 1024);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                ViewModel.JsonText = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"ファイルの読み込みに失敗しました: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task StartImportAsync()
    {
        var result = await ViewModel.ExecuteImportAsync(JS);
        switch (result)
        {
            case Success<TaggingImportResult>:
                Snackbar.Add("インポートが完了しました！", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }
}