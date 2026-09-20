namespace SRNSMudApp.Components.Item;

using SRNSMudApp.Models;

/// <summary>
///     LinkConversionPanel の純粋なロジックを切り出した ViewModel。
///     閾値は AutoReplaceThreshold 値オブジェクトにより有効範囲が保証される。
/// </summary>
public static class LinkConversionPanelViewModel
{
    /// <summary>
    ///     パネルを表示すべきかどうか（読み込み中、候補あり、または設定展開中のいずれか）。
    /// </summary>
    public static bool ShouldDisplay(bool isLoading, int autoReplaceCount, int manualCount, bool showSettings) =>
        isLoading || autoReplaceCount > 0 || manualCount > 0 || showSettings;

    /// <summary>
    ///     候補が存在するかどうか。
    /// </summary>
    public static bool HasAnyCandidates(int autoReplaceCount, int manualCount) =>
        autoReplaceCount > 0 || manualCount > 0;

    /// <summary>
    ///     閾値を有効範囲（0.50f 〜 1.0f）内にクランプする（AutoReplaceThreshold 値オブジェクトに委譲）。
    /// </summary>
    public static AutoReplaceThreshold ClampThreshold(float threshold) =>
        new(threshold);
}