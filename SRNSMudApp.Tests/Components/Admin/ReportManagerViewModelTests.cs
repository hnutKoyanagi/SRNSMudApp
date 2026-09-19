// Components/Admin/ReportManagerViewModelTests.cs
#region

using MudBlazor;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="ReportManagerViewModel" /> の単体テスト。
/// </summary>
public class ReportManagerViewModelTests
{
    [Theory]
    [InlineData(ReportStatus.Pending, "未対応")]
    [InlineData(ReportStatus.Reviewed, "確認済み")]
    [InlineData(ReportStatus.ActionTaken, "処置済み")]
    [InlineData(ReportStatus.Dismissed, "却下")]
    [InlineData((ReportStatus)999, "不明")]
    public void GetStatusText_MapsReportStatusToJapaneseText(ReportStatus status, string expectedText)
    {
        var actual = ReportManagerViewModel.GetStatusText(status);
        Assert.Equal(expectedText, actual);
    }

    [Theory]
    [InlineData(ReportStatus.Pending, Color.Warning)]
    [InlineData(ReportStatus.Reviewed, Color.Info)]
    [InlineData(ReportStatus.ActionTaken, Color.Success)]
    [InlineData(ReportStatus.Dismissed, Color.Default)]
    [InlineData((ReportStatus)999, Color.Default)]
    public void GetStatusColor_MapsReportStatusToMudColor(ReportStatus status, Color expectedColor)
    {
        var actual = ReportManagerViewModel.GetStatusColor(status);
        Assert.Equal(expectedColor, actual);
    }

    [Theory]
    [InlineData(ReportTargetType.Item, "アイテム")]
    [InlineData(ReportTargetType.Tag, "タグ")]
    [InlineData((ReportTargetType)999, "その他")]
    public void GetTargetTypeText_MapsReportTargetTypeToJapaneseText(ReportTargetType targetType, string expectedText)
    {
        var actual = ReportManagerViewModel.GetTargetTypeText(targetType);
        Assert.Equal(expectedText, actual);
    }

    [Theory]
    [InlineData(ReportTargetType.Item, Color.Primary)]
    [InlineData(ReportTargetType.Tag, Color.Secondary)]
    [InlineData((ReportTargetType)999, Color.Default)]
    public void GetTargetTypeColor_MapsReportTargetTypeToMudColor(ReportTargetType targetType, Color expectedColor)
    {
        var actual = ReportManagerViewModel.GetTargetTypeColor(targetType);
        Assert.Equal(expectedColor, actual);
    }
}