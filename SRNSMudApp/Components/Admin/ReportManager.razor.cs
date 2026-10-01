namespace SRNSMudApp.Components.Admin;

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     通報一覧管理画面コンポーネントのコードビハインド。
///     ステータスや対象種別による絞り込み、一覧テーブルの描画、詳細ダイアログの表示を制御する。
///     通報データの取得・更新ロジックは <see cref="ReportManagerViewModel"/> に委譲する。
/// </summary>
public partial class ReportManager : ComponentBase
{
    [Inject] private ReportManagerViewModel ViewModel { get; set; } = null!;
    [Inject] private IDialogLauncher DialogLauncher { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private IReadOnlyList<ContentReport> Reports => ViewModel.Reports;

    private ReportStatus? SelectedStatus
    {
        get => ViewModel.SelectedStatus;
        set => ViewModel.SelectedStatus = value;
    }

    private ReportTargetType? SelectedTargetType
    {
        get => ViewModel.SelectedTargetType;
        set => ViewModel.SelectedTargetType = value;
    }

    private bool IsLoading => ViewModel.IsLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadReportsAsync();
    }

    private async Task LoadReportsAsync()
    {
        var (success, errorMessage) = await ViewModel.LoadReportsAsync();
        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            Snackbar.Add(errorMessage, Severity.Error);
        }
    }

    private async Task OnStatusFilterChangedAsync(ReportStatus? status)
    {
        SelectedStatus = status;
        await LoadReportsAsync();
    }

    private async Task OnTargetTypeFilterChangedAsync(ReportTargetType? targetType)
    {
        SelectedTargetType = targetType;
        await LoadReportsAsync();
    }

    private async Task OpenDetailDialogAsync(ContentReport report)
    {
        var latestReport = await ViewModel.GetReportByIdAsync(report.Id);
        if (latestReport is null)
        {
            Snackbar.Add("通報が見つかりません。", Severity.Error);
            await LoadReportsAsync();
            return;
        }

        var parameters = new DialogParameters
        {
            [nameof(ReportDetailDialog.Report)] = latestReport
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        var dialog = await DialogLauncher.ShowAsync<ReportDetailDialog>("通報の詳細・対応", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadReportsAsync();
        }
    }
}