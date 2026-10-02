namespace SRNSMudApp.Components.Admin;

using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     通報詳細・処置ダイアログコンポーネントのコードビハインド。
///     通報ステータスの変更、対象コンテンツの非公開化・削除処置のダイアログ操作を担当する。
///     ドメイン操作は <see cref="ReportDetailActionViewModel"/> に委譲する。
/// </summary>
public partial class ReportDetailDialog : ComponentBase
{
    [Inject] private ReportDetailActionViewModel ActionViewModel { get; set; } = null!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Parameter] public ContentReport? Report { get; set; }

    private string _resolutionNote = "";
    private bool _isProcessing;

    protected override void OnInitialized()
    {
        if (Report is not null)
        {
            _resolutionNote = Report.ResolutionNote ?? "";
        }
    }

    private void Close() => MudDialog.Cancel();

    private async Task<string?> GetAdminUserIdAsync()
    {
        var authState = await AuthStateTask;
        return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task DismissReportAsync()
    {
        await UpdateStatusAsync(ReportStatus.Dismissed, "通報を却下しました。");
    }

    private async Task ReviewReportAsync()
    {
        await UpdateStatusAsync(ReportStatus.Reviewed, "通報を確認済みに変更しました。");
    }

    private async Task ResolveWithoutDeleteAsync()
    {
        await UpdateStatusAsync(ReportStatus.ActionTaken, "処置を完了として記録しました。");
    }

    private async Task UpdateStatusAsync(ReportStatus status, string successMessage)
    {
        if (Report is null)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            var adminId = await GetAdminUserIdAsync();
            var success = await ActionViewModel.UpdateStatusAsync(Report.Id, status, _resolutionNote, adminId, successMessage);
            if (success)
            {
                MudDialog.Close(DialogResult.Ok(true));
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ResolveAndHideTargetAsync()
    {
        if (Report is null)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            var adminId = await GetAdminUserIdAsync();
            var success = await ActionViewModel.ResolveAndHideTargetAsync(Report.Id, _resolutionNote, adminId);
            if (success)
            {
                MudDialog.Close(DialogResult.Ok(true));
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ResolveAndDeleteTargetAsync()
    {
        if (Report is null)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            var adminId = await GetAdminUserIdAsync();
            var success = await ActionViewModel.ResolveAndDeleteTargetAsync(Report.Id, _resolutionNote, adminId);
            if (success)
            {
                MudDialog.Close(DialogResult.Ok(true));
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
}