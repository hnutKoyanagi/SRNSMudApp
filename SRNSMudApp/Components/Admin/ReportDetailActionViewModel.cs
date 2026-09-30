using System.Diagnostics.CodeAnalysis;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Admin;

/// <summary>
///     通報詳細ダイアログにおけるステータス変更・非公開化・削除処置を集約する ViewModel。
///     bUnit を使わずに xUnit で直接単体テスト可能。
/// </summary>
public class ReportDetailActionViewModel
{
    private readonly IContentReportService _contentReportService;
    private readonly ISnackbar _snackbar;

    public ReportDetailActionViewModel(
        IContentReportService contentReportService,
        ISnackbar snackbar)
    {
        _contentReportService = contentReportService;
        _snackbar = snackbar;
    }

    /// <summary>
    ///     通報のステータス（却下、確認済み、処置完了など）を更新する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Display error in snackbar on failure")]
    public async Task<bool> UpdateStatusAsync(
        int reportId,
        ReportStatus status,
        string resolutionNote,
        string? adminUserId,
        string successMessage)
    {
        if (reportId <= 0)
        {
            _snackbar.Add("無効な通報IDです。", Severity.Error);
            return false;
        }

        if (string.IsNullOrEmpty(adminUserId))
        {
            _snackbar.Add("管理者情報が取得できません。", Severity.Error);
            return false;
        }

        try
        {
            bool success = await _contentReportService.UpdateReportStatusAsync(
                reportId,
                status,
                resolutionNote,
                adminUserId);

            if (success)
            {
                _snackbar.Add(successMessage, Severity.Success);
                return true;
            }

            _snackbar.Add("通報の更新に失敗しました。", Severity.Error);
            return false;
        }
        catch (Exception ex)
        {
            _snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
            return false;
        }
    }

    /// <summary>
    ///     対象コンテンツを非公開化し、通報を処置完了とする。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Display error in snackbar on failure")]
    public async Task<bool> ResolveAndHideTargetAsync(
        int reportId,
        string resolutionNote,
        string? adminUserId)
    {
        if (reportId <= 0)
        {
            _snackbar.Add("無効な通報IDです。", Severity.Error);
            return false;
        }

        if (string.IsNullOrEmpty(adminUserId))
        {
            _snackbar.Add("管理者情報が取得できません。", Severity.Error);
            return false;
        }

        try
        {
            bool success = await _contentReportService.ResolveReportWithHideAsync(
                reportId,
                resolutionNote,
                adminUserId);

            if (success)
            {
                _snackbar.Add("対象コンテンツを非公開化し、処置完了としました。", Severity.Success);
                return true;
            }

            _snackbar.Add("非公開化の実行に失敗しました。", Severity.Error);
            return false;
        }
        catch (Exception ex)
        {
            _snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
            return false;
        }
    }

    /// <summary>
    ///     対象コンテンツを物理削除し、通報を処置完了とする。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Display error in snackbar on failure")]
    public async Task<bool> ResolveAndDeleteTargetAsync(
        int reportId,
        string resolutionNote,
        string? adminUserId)
    {
        if (reportId <= 0)
        {
            _snackbar.Add("無効な通報IDです。", Severity.Error);
            return false;
        }

        if (string.IsNullOrEmpty(adminUserId))
        {
            _snackbar.Add("管理者情報が取得できません。", Severity.Error);
            return false;
        }

        try
        {
            bool success = await _contentReportService.ResolveReportWithActionAsync(
                reportId,
                deleteTarget: true,
                resolutionNote,
                adminUserId);

            if (success)
            {
                _snackbar.Add("対象コンテンツを削除し、処置完了としました。", Severity.Success);
                return true;
            }

            _snackbar.Add("処置の実行に失敗しました。", Severity.Error);
            return false;
        }
        catch (Exception ex)
        {
            _snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
            return false;
        }
    }
}