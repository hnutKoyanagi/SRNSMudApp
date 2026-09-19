// Components/Contract/ContractManagementViewModel.cs
#region

using MudBlazor;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Components.Contract;

/// <summary>
///     ContractManagement コンポーネントの表示・判定ロジックを担当する ViewModel。
///     ステータスに応じた日本語表示文字列や MudBlazor の Color をカプセル化する。
/// </summary>
public static class ContractManagementViewModel
{
    /// <summary>
    ///     TradeStatus に対応する日本語表示文字列を取得する。
    /// </summary>
    /// <param name="status">取引ステータス。</param>
    /// <returns>ステータスの日本語表示名。</returns>
    public static string GetStatusDisplayText(TradeStatus status) => status switch
    {
        TradeStatus.Proposed => "提案中",
        TradeStatus.Executed => "承認済み",
        TradeStatus.Rejected => "拒否",
        TradeStatus.Canceled => "キャンセル",
        _ => "その他"
    };

    /// <summary>
    ///     TradeStatus に対応する MudBlazor の Color を取得する。
    /// </summary>
    /// <param name="status">取引ステータス。</param>
    /// <returns>MudBlazor Color。</returns>
    public static Color GetStatusColor(TradeStatus status) => status switch
    {
        TradeStatus.Proposed => Color.Info,
        TradeStatus.Executed => Color.Success,
        TradeStatus.Rejected => Color.Error,
        TradeStatus.Canceled => Color.Default,
        _ => Color.Default
    };

    /// <summary>
    ///     コントラクトが取り下げ（キャンセル）可能かどうかを判定する。
    /// </summary>
    /// <param name="contract">対象のコントラクト。</param>
    /// <returns>提案中（Proposed）であれば true。</returns>
    public static bool CanCancelContract(TaggingRequestEntity contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Status == TradeStatus.Proposed;
    }
}

