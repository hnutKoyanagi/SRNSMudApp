using Bunit;
using Bunit.TestDoubles;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.User;
using SRNSMudApp.Tests.TestSupport;

namespace SRNSMudApp.Tests.Components.User;

/// <summary>
/// <see cref="MakeMeAdmin"/> 画面のコンポーネントテスト。
/// 開発環境判定、現在の権限表示（Admin/一般ユーザー）、トグルスイッチのレンダリングおよび操作を検証する。
/// </summary>
public sealed class MakeMeAdminTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly BunitAuthorizationContext _authContext;

    public MakeMeAdminTests()
    {
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddLogging();
        _ = _ctx.Services.AddSingleton(_envMock.Object);
        _authContext = _ctx.AddAuthorization();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    [Fact]
    public void MakeMeAdmin_InNonDevelopment_ShowsErrorAlert()
    {
        // Arrange
        _ = _envMock.Setup(e => e.EnvironmentName).Returns("Production");
        _authContext.SetAuthorized("user-1");

        // Act
        var cut = _ctx.Render<MakeMeAdmin>();

        // Assert
        var alert = cut.FindComponent<MudAlert>();
        Assert.NotNull(alert);
        Assert.Contains("このページは開発環境でのみ利用可能です", cut.Markup);
        Assert.Empty(cut.FindComponents<MudSwitch<bool>>());
    }

    [Fact]
    public void MakeMeAdmin_WhenUserIsNormalUser_RendersToggleSwitchOff()
    {
        // Arrange
        _ = _envMock.Setup(e => e.EnvironmentName).Returns("Development");
        _authContext.SetAuthorized("user-1"); // 一般ユーザー

        // Act
        var cut = _ctx.Render<MakeMeAdmin>();

        // Assert
        Assert.Contains("一般ユーザー (User)", cut.Markup);

        var mudSwitch = cut.FindComponent<MudSwitch<bool>>();
        Assert.NotNull(mudSwitch);
        var input = (AngleSharp.Html.Dom.IHtmlInputElement)mudSwitch.Find("input[type='checkbox']");
        Assert.False(input.IsChecked);

        var hiddenInput = cut.Find("input[name='enableAdmin']");
        Assert.NotNull(hiddenInput);
        Assert.Equal("true", hiddenInput.GetAttribute("value")); // 次のトグル先（Admin付与）

        // スイッチを操作すると、JS経由で送信ボタンがクリックされる
        input.Change(true);
        var invocations = _ctx.JSInterop.Invocations["eval"];
        Assert.NotEmpty(invocations);
    }

    [Fact]
    public void MakeMeAdmin_WhenUserIsAdmin_RendersToggleSwitchOn()
    {
        // Arrange
        _ = _envMock.Setup(e => e.EnvironmentName).Returns("Development");
        _authContext.SetAuthorized("admin-1");
        _authContext.SetRoles("Admin"); // 管理者

        // Act
        var cut = _ctx.Render<MakeMeAdmin>();

        // Assert
        Assert.Contains("管理者 (Admin)", cut.Markup);

        var mudSwitch = cut.FindComponent<MudSwitch<bool>>();
        Assert.NotNull(mudSwitch);
        var input = (AngleSharp.Html.Dom.IHtmlInputElement)mudSwitch.Find("input[type='checkbox']");
        Assert.True(input.IsChecked);

        var hiddenInput = cut.Find("input[name='enableAdmin']");
        Assert.NotNull(hiddenInput);
        Assert.Equal("false", hiddenInput.GetAttribute("value")); // 次のトグル先（Admin解除）

        // スイッチを操作すると、JS経由で送信ボタンがクリックされる
        input.Change(false);
        var invocations = _ctx.JSInterop.Invocations["eval"];
        Assert.NotEmpty(invocations);
    }
}