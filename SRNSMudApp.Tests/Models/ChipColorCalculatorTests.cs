using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Models;

public class ChipColorCalculatorTests
{
    [Fact]
    public void GetBackgroundColor_WhenDeleted_ReturnsDeletedColor()
    {
        var color = ChipColorCalculator.GetBackgroundColor(
            isDeleted: true,
            hasHighlight: false,
            isMyTag: false,
            chipIndex: 0,
            backgrounds: ["#111111"]);

        Assert.Equal(ChipColorCalculator.DeletedBackgroundColor, color);
    }

    [Fact]
    public void GetBackgroundColor_WhenHighlight_ReturnsHighlightColor()
    {
        var color = ChipColorCalculator.GetBackgroundColor(
            isDeleted: false,
            hasHighlight: true,
            isMyTag: true,
            chipIndex: 0,
            backgrounds: ["#111111"]);

        Assert.Equal(ChipColorCalculator.HighlightBackgroundColor, color);
    }

    [Fact]
    public void GetBackgroundColor_WhenMyTag_UsesPaletteOrFallback()
    {
        var customColor = ChipColorCalculator.GetBackgroundColor(
            isDeleted: false,
            hasHighlight: false,
            isMyTag: true,
            chipIndex: 1,
            backgrounds: ["#111111", "#222222"]);

        Assert.Equal("#222222", customColor);

        // インデックス超過時は0番目
        var overflowColor = ChipColorCalculator.GetBackgroundColor(
            isDeleted: false,
            hasHighlight: false,
            isMyTag: true,
            chipIndex: 5,
            backgrounds: ["#111111", "#222222"]);

        Assert.Equal("#111111", overflowColor);

        // パレットが空の場合はデフォルト色
        var fallbackColor = ChipColorCalculator.GetBackgroundColor(
            isDeleted: false,
            hasHighlight: false,
            isMyTag: true,
            chipIndex: 0,
            backgrounds: []);

        Assert.Equal(ChipColorCalculator.DefaultMyTagBackgroundColor, fallbackColor);
    }

    [Fact]
    public void GetBackgroundColor_WhenOtherTag_ReturnsDefaultColor()
    {
        var color = ChipColorCalculator.GetBackgroundColor(
            isDeleted: false,
            hasHighlight: false,
            isMyTag: false,
            chipIndex: 0,
            backgrounds: ["#111111"]);

        Assert.Equal(ChipColorCalculator.DefaultBackgroundColor, color);
    }

    [Fact]
    public void GetTextColor_CalculatesCorrectColors()
    {
        // 削除
        Assert.Equal(ChipColorCalculator.DeletedTextColor,
            ChipColorCalculator.GetTextColor(true, false, false, 0, null));

        // ハイライト
        Assert.Equal(ChipColorCalculator.HighlightTextColor,
            ChipColorCalculator.GetTextColor(false, true, true, 0, null));

        // 自分のタグ
        Assert.Equal("#333333",
            ChipColorCalculator.GetTextColor(false, false, true, 0, ["#333333"]));

        // 他人のタグ
        Assert.Equal(ChipColorCalculator.DefaultTextColor,
            ChipColorCalculator.GetTextColor(false, false, false, 0, ["#333333"]));
    }
}