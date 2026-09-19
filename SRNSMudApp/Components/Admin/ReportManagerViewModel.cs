// Components/Admin/ReportManagerViewModel.cs
#region

using MudBlazor;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Components.Admin;

/// <summary>
///     ReportManager コンポーネントの表示・判定ロジックを担当する ViewModel。
///     通報ステータスや通報対象種別の表示文字列・カラーマッピングをカプセル化する。
/// </summary>
public static class ReportManagerViewModel
{
    /// <summary>
    ///     ReportStatus に対応する日本語表示文字列を取得する。
    /// </summary>
    public static string GetStatusText(ReportStatus status) => status switch
    {
        ReportStatus.Pending => "未対応",
        ReportStatus.Reviewed => "確認済み",
        ReportStatus.ActionTaken => "処置済み",
        ReportStatus.Dismissed => "却下",
        _ => "不明"
    };

    /// <summary>
    ///     ReportStatus に対応する MudBlazor Color を取得する。
    /// </summary>
    public static Color GetStatusColor(ReportStatus status) => status switch
    {
        ReportStatus.Pending => Color.Warning,
        ReportStatus.Reviewed => Color.Info,
        ReportStatus.ActionTaken => Color.Success,
        ReportStatus.Dismissed => Color.Default,
        _ => Color.Default
    };

    /// <summary>
    ///     ReportTargetType に対応する日本語表示文字列を取得する。
    /// </summary>
    public static string GetTargetTypeText(ReportTargetType targetType) => targetType switch
    {
        ReportTargetType.Item => "アイテム",
        ReportTargetType.Tag => "タグ",
        _ => "その他"
    };

    /// <summary>
    ///     ReportTargetType に対応する MudBlazor Color を取得する。
    /// </summary>
    public static Color GetTargetTypeColor(ReportTargetType targetType) => targetType switch
    {
        ReportTargetType.Item => Color.Primary,
        ReportTargetType.Tag => Color.Secondary,
        _ => Color.Default
    };
}