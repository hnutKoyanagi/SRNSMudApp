using Moq;

using MudBlazor;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="ReportManagerViewModel" /> の単体テスト。
///     通報一覧のロード、フィルタリング、単一通報取得、表示テキスト・カラーマッピングを検証する。
/// </summary>
public class ReportManagerViewModelTests
{
    private readonly Mock<IContentReportService> _contentReportServiceMock = new();
    private readonly ReportManagerViewModel _sut;

    public ReportManagerViewModelTests()
    {
        _sut = new ReportManagerViewModel(_contentReportServiceMock.Object);
    }

    [Fact]
    public void DefaultState_HasPendingStatus_AndIsLoadingTrue()
    {
        Assert.Equal(ReportStatus.Pending, _sut.SelectedStatus);
        Assert.Null(_sut.SelectedTargetType);
        Assert.True(_sut.IsLoading);
        Assert.Empty(_sut.Reports);
    }

    [Fact]
    public async Task LoadReportsAsync_Success_PopulatesReports()
    {
        // Arrange
        var testReports = new List<ContentReport>
        {
            new()
            {
                Id = 1,
                TargetType = ReportTargetType.Item,
                Reason = "スパム",
                OwnerId = "reporter-1"
            },
            new()
            {
                Id = 2,
                TargetType = ReportTargetType.Tag,
                Reason = "不適切な表現",
                OwnerId = "reporter-2"
            }
        };

        _sut.SelectedStatus = ReportStatus.Pending;
        _sut.SelectedTargetType = null;

        _contentReportServiceMock.Setup(s => s.GetReportsAsync(ReportStatus.Pending, null))
            .ReturnsAsync(testReports);

        // Act
        var (success, errorMessage) = await _sut.LoadReportsAsync();

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.False(_sut.IsLoading);
        Assert.Equal(2, _sut.Reports.Count);
        _contentReportServiceMock.Verify(s => s.GetReportsAsync(ReportStatus.Pending, null), Times.Once);
    }

    [Fact]
    public async Task LoadReportsAsync_Exception_ReturnsErrorAndResetsLoading()
    {
        // Arrange
        _contentReportServiceMock.Setup(s => s.GetReportsAsync(It.IsAny<ReportStatus?>(), It.IsAny<ReportTargetType?>()))
            .ThrowsAsync(new InvalidOperationException("DB connection error"));

        // Act
        var (success, errorMessage) = await _sut.LoadReportsAsync();

        // Assert
        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("DB connection error", errorMessage);
        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public async Task GetReportByIdAsync_CallsService()
    {
        // Arrange
        var expectedReport = new ContentReport
        {
            Id = 10,
            TargetType = ReportTargetType.Item,
            Reason = "スパム",
            OwnerId = "reporter-1"
        };

        _contentReportServiceMock.Setup(s => s.GetReportByIdAsync(10))
            .ReturnsAsync(expectedReport);

        // Act
        var report = await _sut.GetReportByIdAsync(10);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(10, report.Id);
        _contentReportServiceMock.Verify(s => s.GetReportByIdAsync(10), Times.Once);
    }

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