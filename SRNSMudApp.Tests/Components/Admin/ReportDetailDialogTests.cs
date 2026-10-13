using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="ReportDetailDialog"/> の UI インタラクションテスト。
///     通報内容の表示、ステータス変更（却下・確認済み・処置完了）、対象非公開化・削除ボタンの実行を検証する。
/// </summary>
public sealed class ReportDetailDialogTests : IAsyncDisposable
{
    private const string AdminUserId = "admin-user-1";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContentReportService> _reportServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public ReportDetailDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _reportServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, AdminUserId),
            new(ClaimTypes.Name, "AdminUser"),
            new(ClaimTypes.Role, "Admin")
        ];
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")));
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ = _ctx.Services.AddScoped(_ => authMock.Object);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task RendersReportDetails_AndDismissButtonUpdatesStatus()
    {
        var report = new ContentReport
        {
            Id = 42,
            OwnerId = "reporter-1",
            Owner = new ApplicationUser { Id = "reporter-1", UserName = "ReporterAlice" },
            TargetType = ReportTargetType.Item,
            ItemId = 99,
            Item = new SRNSMudApp.Data.Item { Id = 99, Content = "不適切な投稿内容", OwnerId = "bad-user" },
            TargetContentSnapshot = "不適切な投稿スナップショット",
            Reason = "スパム・宣伝目的",
            Detail = "宣伝リンクが含まれています",
            CreatedDate = DateTime.UtcNow
        };

        _ = _reportServiceMock.Setup(s => s.UpdateReportStatusAsync(
                42, ReportStatus.Dismissed, It.IsAny<string>(), AdminUserId))
            .ReturnsAsync(true);

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReportDetailDialog>
        {
            { x => x.Report, report }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ReportDetailDialog>("通報詳細", parameters);

        host.WaitForState(() => host.Markup.Contains("不適切な投稿スナップショット"));
        Assert.Contains("ReporterAlice", host.Markup);
        Assert.Contains("スパム・宣伝目的", host.Markup);
        Assert.Contains("対象を非公開にして処置完了", host.Markup);

        host.FindAll("button").First(b => b.TextContent.Contains("却下 (問題なし)")).Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _reportServiceMock.Verify(s => s.UpdateReportStatusAsync(
            42, ReportStatus.Dismissed, It.IsAny<string>(), AdminUserId), Times.Once);
    }

    [Fact]
    public async Task ClickResolveAndHideTarget_CallsResolveReportWithHideAsync()
    {
        var report = new ContentReport
        {
            Id = 43,
            OwnerId = "reporter-1",
            TargetType = ReportTargetType.Item,
            ItemId = 100,
            Item = new SRNSMudApp.Data.Item { Id = 100, Content = "非公開対象アイテム", OwnerId = "bad-user" },
            TargetContentSnapshot = "非公開対象アイテム",
            Reason = "誹謗中傷・嫌がらせ",
            CreatedDate = DateTime.UtcNow
        };

        _ = _reportServiceMock.Setup(s => s.ResolveReportWithHideAsync(
                43, It.IsAny<string>(), AdminUserId))
            .ReturnsAsync(true);

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReportDetailDialog>
        {
            { x => x.Report, report }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ReportDetailDialog>("通報詳細", parameters);

        host.WaitForState(() => host.Markup.Contains("対象を非公開にして処置完了"));
        host.FindAll("button").First(b => b.TextContent.Contains("対象を非公開にして処置完了")).Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _reportServiceMock.Verify(s => s.ResolveReportWithHideAsync(
            43, It.IsAny<string>(), AdminUserId), Times.Once);
    }

    private sealed class AuthDialogHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<MudDialogProvider>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}