namespace SRNSMudApp.Components.Tag;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     タグインポート (CSV) 画面コンポーネントのコードビハインド。
/// </summary>
public partial class ImportTag : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private ImportTagViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    public int Weight { get; set; }

    private IBrowserFile? _file;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        ViewModel.Initialize(authState.User);
    }

    private async Task<IEnumerable<Tag>> SearchTagsAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchTagsAsync(value, token);
    }

    private void UploadFiles(IBrowserFile? file)
    {
        _file = file;
    }

    [SuppressMessage("Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Display error notification to user")]
    private async Task ImportData()
    {
        if (_file is null)
        {
            Snackbar.Add("ファイルが選択されていません", Severity.Warning);
            return;
        }

        try
        {
            using var stream = _file.OpenReadStream(104857600); // Max 10MB
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();

            var result = await ViewModel.ImportCsvAsync(content);

            switch (result)
            {
                case Success<TagImportResult> success:
                    Snackbar.Add($"インポートが完了しました。{success.Value.CreatedCount}個のタグを新規作成、{success.Value.UpdatedCount}個のタグを更新しました。", Severity.Success);
                    _file = null;
                    NavigationManager.NavigateTo("/tag-tree");
                    break;
                case Failure fail:
                    Snackbar.Add(fail.ErrorMessage, Severity.Error);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"インポート中にエラーが発生しました: {ex.Message}", Severity.Error);
        }
    }
}