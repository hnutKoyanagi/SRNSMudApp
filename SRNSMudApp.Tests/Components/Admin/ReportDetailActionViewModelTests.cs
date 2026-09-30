using Moq;

using MudBlazor;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

public sealed class ReportDetailActionViewModelTests
{
    private const string CurrentAdminId = "admin-100";
    private readonly Mock<IContentReportService> _contentReportServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly ReportDetailActionViewModel _sut;

    public ReportDetailActionViewModelTests()
    {
        _sut = new ReportDetailActionViewModel(
            _contentReportServiceMock.Object,
            _snackbarMock.Object);
    }

    [Theory]
    [InlineData(0, CurrentAdminId)]
    [InlineData(-1, CurrentAdminId)]
    [InlineData(10, "")]
    [InlineData(10, null)]
    public async Task UpdateStatusAsync_WhenArgumentsInvalid_ShowsSnackbarAndReturnsFalse(int reportId, string? adminUserId)
    {
        bool result = await _sut.UpdateStatusAsync(reportId, ReportStatus.Dismissed, "note", adminUserId, "Success");

        Assert.False(result);
        _contentReportServiceMock.Verify(s => s.UpdateReportStatusAsync(
            It.IsAny<int>(), It.IsAny<ReportStatus>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _snackbarMock.Verify(s => s.Add(It.IsAny<string>(), Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenValid_CallsServiceAndShowsSuccessSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.UpdateReportStatusAsync(10, ReportStatus.Reviewed, "note", CurrentAdminId))
            .ReturnsAsync(true);

        bool result = await _sut.UpdateStatusAsync(10, ReportStatus.Reviewed, "note", CurrentAdminId, "確認済みに変更しました。");

        Assert.True(result);
        _contentReportServiceMock.Verify(s => s.UpdateReportStatusAsync(10, ReportStatus.Reviewed, "note", CurrentAdminId), Times.Once);
        _snackbarMock.Verify(s => s.Add("確認済みに変更しました。", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenServiceReturnsFalse_ShowsErrorSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.UpdateReportStatusAsync(10, ReportStatus.Dismissed, "note", CurrentAdminId))
            .ReturnsAsync(false);

        bool result = await _sut.UpdateStatusAsync(10, ReportStatus.Dismissed, "note", CurrentAdminId, "却下しました。");

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("通報の更新に失敗しました。", Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenExceptionThrown_ShowsExceptionSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.UpdateReportStatusAsync(It.IsAny<int>(), It.IsAny<ReportStatus>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        bool result = await _sut.UpdateStatusAsync(10, ReportStatus.Dismissed, "note", CurrentAdminId, "却下しました。");

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(msg => msg.Contains("DB error")), Severity.Error, null, null), Times.Once);
    }

    [Theory]
    [InlineData(0, CurrentAdminId)]
    [InlineData(10, "")]
    [InlineData(10, null)]
    public async Task ResolveAndHideTargetAsync_WhenArgumentsInvalid_ReturnsFalse(int reportId, string? adminUserId)
    {
        bool result = await _sut.ResolveAndHideTargetAsync(reportId, "note", adminUserId);

        Assert.False(result);
        _contentReportServiceMock.Verify(s => s.ResolveReportWithHideAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAndHideTargetAsync_WhenValid_CallsServiceAndShowsSuccessSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.ResolveReportWithHideAsync(10, "hide note", CurrentAdminId))
            .ReturnsAsync(true);

        bool result = await _sut.ResolveAndHideTargetAsync(10, "hide note", CurrentAdminId);

        Assert.True(result);
        _contentReportServiceMock.Verify(s => s.ResolveReportWithHideAsync(10, "hide note", CurrentAdminId), Times.Once);
        _snackbarMock.Verify(s => s.Add("対象コンテンツを非公開化し、処置完了としました。", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task ResolveAndHideTargetAsync_WhenServiceReturnsFalse_ShowsErrorSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.ResolveReportWithHideAsync(10, "hide note", CurrentAdminId))
            .ReturnsAsync(false);

        bool result = await _sut.ResolveAndHideTargetAsync(10, "hide note", CurrentAdminId);

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("非公開化の実行に失敗しました。", Severity.Error, null, null), Times.Once);
    }

    [Theory]
    [InlineData(0, CurrentAdminId)]
    [InlineData(10, "")]
    [InlineData(10, null)]
    public async Task ResolveAndDeleteTargetAsync_WhenArgumentsInvalid_ReturnsFalse(int reportId, string? adminUserId)
    {
        bool result = await _sut.ResolveAndDeleteTargetAsync(reportId, "note", adminUserId);

        Assert.False(result);
        _contentReportServiceMock.Verify(s => s.ResolveReportWithActionAsync(
            It.IsAny<int>(), true, It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAndDeleteTargetAsync_WhenValid_CallsServiceAndShowsSuccessSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.ResolveReportWithActionAsync(10, true, "delete note", CurrentAdminId))
            .ReturnsAsync(true);

        bool result = await _sut.ResolveAndDeleteTargetAsync(10, "delete note", CurrentAdminId);

        Assert.True(result);
        _contentReportServiceMock.Verify(s => s.ResolveReportWithActionAsync(10, true, "delete note", CurrentAdminId), Times.Once);
        _snackbarMock.Verify(s => s.Add("対象コンテンツを削除し、処置完了としました。", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task ResolveAndDeleteTargetAsync_WhenServiceReturnsFalse_ShowsErrorSnackbar()
    {
        _contentReportServiceMock
            .Setup(s => s.ResolveReportWithActionAsync(10, true, "delete note", CurrentAdminId))
            .ReturnsAsync(false);

        bool result = await _sut.ResolveAndDeleteTargetAsync(10, "delete note", CurrentAdminId);

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("処置の実行に失敗しました。", Severity.Error, null, null), Times.Once);
    }
}