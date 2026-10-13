using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
///     <see cref="ReportContentDialog"/> の UI インタラクションテスト。
///     通報対象情報の表示、通報理由・詳細入力、送信ボタンクリックによる CreateReportAsync 呼び出しを検証する。
/// </summary>
public sealed class ReportContentDialogTests : IAsyncDisposable
{
    private const string ReporterId = "reporter-user-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IContentReportService> _reportServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public ReportContentDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _reportServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ctx.Services.AddAuthorizationCore();

        Claim[] claims = [new(ClaimTypes.NameIdentifier, ReporterId), new(ClaimTypes.Name, "Reporter")];
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
    public async Task Submit_WithItemTarget_CallsCreateReportAsync_AndClosesDialog()
    {
        _ = _reportServiceMock.Setup(s => s.CreateReportAsync(
                It.IsAny<CreateContentReportDto>(), ReporterId))
            .ReturnsAsync(new ContentReport
            {
                Id = 1,
                OwnerId = ReporterId,
                TargetType = ReportTargetType.Item,
                ItemId = 55,
                Reason = "スパム・宣伝目的",
                TargetContentSnapshot = "対象アイテムの本文"
            });

        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReportContentDialog>
        {
            { x => x.TargetType, ReportTargetType.Item },
            { x => x.ItemId, 55 },
            { x => x.TargetContent, "対象アイテムの本文" },
            { x => x.TargetOwnerName, "BadPoster" }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ReportContentDialog>("コンテンツを通報", parameters);

        host.WaitForState(() => host.Markup.Contains("対象アイテムの本文"));
        Assert.Contains("作成者: BadPoster", host.Markup);

        var textarea = host.Find("textarea");
        textarea.Change("スパムURLが貼られています");

        host.FindAll("button").First(b => b.TextContent.Contains("通報を送信")).Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _reportServiceMock.Verify(s => s.CreateReportAsync(
            It.Is<CreateContentReportDto>(dto =>
                dto.TargetType == ReportTargetType.Item &&
                dto.ItemId == 55 &&
                dto.Reason == "スパム・宣伝目的" &&
                dto.Detail == "スパムURLが貼られています"),
            ReporterId), Times.Once);

        _snackbarMock.Verify(s => s.Add(
            "通報を受け付けました。ご協力ありがとうございます。",
            Severity.Success,
            It.IsAny<Action<SnackbarOptions>?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ClickCancel_CancelsDialog()
    {
        IRenderedComponent<AuthDialogHost> host = _ctx.Render<AuthDialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReportContentDialog>
        {
            { x => x.TargetType, ReportTargetType.Tag },
            { x => x.TagId, 12 },
            { x => x.TargetContent, "不適切タグ" }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ReportContentDialog>("コンテンツを通報", parameters);

        host.WaitForState(() => host.Markup.Contains("不適切タグ"));
        host.FindAll("button").First(b => b.TextContent.Contains("キャンセル")).Click();

        DialogResult? result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.True(result.Canceled);
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