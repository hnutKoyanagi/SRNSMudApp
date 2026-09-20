namespace SRNSMudApp.Tests.Components.Item;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Models;

/// <summary>
///     LinkConversionPanelViewModel の純粋な単体テスト。
///     AutoReplaceThreshold 型による類似度制約の保護、および
///     パネル表示判定の論理を検証する。
/// </summary>
public class LinkConversionPanelViewModelTests
{
    [Theory]
    [InlineData(true, 0, 0, false, true)]   // ローディング中なら表示
    [InlineData(false, 1, 0, false, true)]  // 自動置換候補があれば表示
    [InlineData(false, 0, 1, false, true)]  // 手動候補があれば表示
    [InlineData(false, 0, 0, true, true)]   // 設定展開中なら表示
    [InlineData(false, 0, 0, false, false)] // 候補もローディングも設定展開も無ければ非表示
    public void ShouldDisplay_ReturnsExpectedResult(
        bool isLoading, int autoReplaceCount, int manualCount, bool showSettings, bool expected)
    {
        var result = LinkConversionPanelViewModel.ShouldDisplay(isLoading, autoReplaceCount, manualCount, showSettings);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(2, 3, true)]
    public void HasAnyCandidates_ReturnsExpectedResult(int autoReplaceCount, int manualCount, bool expected)
    {
        var result = LinkConversionPanelViewModel.HasAnyCandidates(autoReplaceCount, manualCount);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.85f, 0.85f)]
    [InlineData(0.40f, 0.50f)] // 最小値 0.50f へクランプ
    [InlineData(1.20f, 1.00f)] // 最大値 1.00f へクランプ
    public void AutoReplaceThreshold_ClampsToValidRange(float input, float expected)
    {
        AutoReplaceThreshold threshold = new(input);
        Assert.Equal(expected, threshold.Value);
        Assert.Equal(expected, (float)threshold);
    }
}