#region

using Bunit;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

#endregion

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     管理者用通報管理画面 (<see cref="ReportManager" />) の bUnit コンポーネントテスト。
/// </summary>
public sealed class ReportManagerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IContentReportService> _reportServiceMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();
    private readonly Mock<IDialogReference> _dialogReferenceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public ReportManagerTests()
    {
        _ = _ctx.Services.AddAuth("admin-user-id", "Admin");
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _reportServiceMock.Object);

        _dialogReferenceMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Cancel());
        _dialogLauncherMock
            .Setup(l => l.ShowAsync(It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(_dialogReferenceMock.Object);

        _ctx.Services.RemoveAll<IDialogLauncher>();
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);

        _ctx.Services.RemoveAll<ISnackbar>();
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void ReportManager_RendersReportsTable_WithCorrectData()
    {
        var reporter = new ApplicationUser { Id = "reporter1", UserName = "reporter_alice" };
        var testReports = new List<ContentReport>
        {
            new()
            {
                Id = 101,
                TargetType = ReportTargetType.Item,
                ItemId = 1,
                TargetContentSnapshot = "不適切な投稿のテスト内容",
                Reason = "スパム・宣伝目的",
                Detail = "広告リンクです",
                OwnerId = "reporter1",
                Owner = reporter,
                CreatedDate = DateTime.UtcNow
            }
        };

        _ = _reportServiceMock.Setup(s => s.GetReportsAsync(ReportStatus.Pending, null))
            .ReturnsAsync(testReports);

        var httpContext = new DefaultHttpContext();
        IRenderedComponent<ReportManager> component =
            _ctx.Render<ReportManager>(parameters => parameters.AddCascadingValue(httpContext));

        component.WaitForState(() => component.Markup.Contains("通報管理"));

        Assert.Contains("不適切な投稿の通報管理", component.Markup);
        Assert.Contains("101", component.Markup);
        Assert.Contains("スパム・宣伝目的", component.Markup);
        Assert.Contains("reporter_alice", component.Markup);
        Assert.Contains("未対応", component.Markup);
    }

    [Fact]
    public void ReportManager_RendersEmptyMessage_WhenNoReports()
    {
        _ = _reportServiceMock.Setup(s => s.GetReportsAsync(It.IsAny<ReportStatus?>(), It.IsAny<ReportTargetType?>()))
            .ReturnsAsync([]);

        var httpContext = new DefaultHttpContext();
        IRenderedComponent<ReportManager> component =
            _ctx.Render<ReportManager>(parameters => parameters.AddCascadingValue(httpContext));

        component.WaitForState(() => component.Markup.Contains("該当する通報はありません。"));

        Assert.Contains("該当する通報はありません。", component.Markup);
    }

    [Fact]
    public void ClickingDetailButton_OpensReportDetailDialogViaLauncher()
    {
        // Arrange
        var sampleReport = new ContentReport
        {
            Id = 101,
            TargetType = ReportTargetType.Item,
            ItemId = 1,
            TargetContentSnapshot = "不適切な投稿のテスト内容",
            Reason = "スパム・宣伝目的",
            OwnerId = "reporter1",
            CreatedDate = DateTime.UtcNow
        };

        _reportServiceMock
            .Setup(s => s.GetReportsAsync(ReportStatus.Pending, null))
            .ReturnsAsync([sampleReport]);

        _reportServiceMock
            .Setup(s => s.GetReportByIdAsync(101))
            .ReturnsAsync(sampleReport);

        var httpContext = new DefaultHttpContext();
        IRenderedComponent<ReportManager> cut =
            _ctx.Render<ReportManager>(parameters => parameters.AddCascadingValue(httpContext));

        // Act: 「詳細・対応」ボタンをクリック
        var detailButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("詳細・対応"));
        Assert.NotNull(detailButton);
        detailButton.Click();

        // Assert: IDialogLauncher 経由で ReportDetailDialog が起動されたことを検証
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(ReportDetailDialog),
                "通報の詳細・対応",
                It.Is<DialogParameters>(p => ((ContentReport)p[nameof(ReportDetailDialog.Report)]!).Id == 101),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void ClickingDetailButton_WhenReportNotFound_ShowsErrorSnackbar()
    {
        // Arrange
        var sampleReport = new ContentReport
        {
            Id = 102,
            TargetType = ReportTargetType.Item,
            ItemId = 1,
            TargetContentSnapshot = "削除済みの通報",
            Reason = "スパム",
            OwnerId = "reporter1",
            CreatedDate = DateTime.UtcNow
        };

        _reportServiceMock
            .Setup(s => s.GetReportsAsync(ReportStatus.Pending, null))
            .ReturnsAsync([sampleReport]);

        _reportServiceMock
            .Setup(s => s.GetReportByIdAsync(102))
            .ReturnsAsync((ContentReport?)null);

        var httpContext = new DefaultHttpContext();
        IRenderedComponent<ReportManager> cut =
            _ctx.Render<ReportManager>(parameters => parameters.AddCascadingValue(httpContext));

        // Act: 「詳細・対応」ボタンをクリック
        var detailButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("詳細・対応"));
        Assert.NotNull(detailButton);
        detailButton.Click();

        // Assert: エラースナックバーが表示され、ダイアログは開かれないことを検証
        _snackbarMock.Verify(
            s => s.Add("通報が見つかりません。", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);

        _dialogLauncherMock.Verify(
            l => l.ShowAsync(It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()),
            Times.Never);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}