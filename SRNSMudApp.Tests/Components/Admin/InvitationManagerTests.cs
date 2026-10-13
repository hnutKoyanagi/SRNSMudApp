using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="InvitationManager"/> コンポーネントの UI インタラクションテスト。
///     招待リンクの生成ボタンクリック、削除ボタンクリックによるサービス連携とスナックバー通知を検証する。
/// </summary>
public class InvitationManagerTests : IAsyncDisposable
{
    private const string AdminUserId = "admin-user-123";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IAdminDataProvider> _adminDataMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly BunitAuthorizationContext _authContext;

    public InvitationManagerTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _ctx.Services.RemoveAll<IAdminDataProvider>();
        _ctx.Services.AddScoped(_ => _adminDataMock.Object);

        _ctx.Services.RemoveAll<ISnackbar>();
        _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.Services.AddTransient<InvitationManagerViewModel>();

        _authContext = _ctx.AddAuthorization();
        _authContext.SetAuthorized("AdminUser");
        _authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, AdminUserId),
            new Claim(ClaimTypes.Name, "AdminUser"),
            new Claim(ClaimTypes.Role, "Admin"));

        _ = _ctx.Render<MudPopoverProvider>();

        _adminDataMock
            .Setup(d => d.GetInvitationsAsync())
            .ReturnsAsync([]);
    }

    [Fact]
    public void ClickingGenerateButton_WithValidEmail_CallsCreateAndShowsSnackbar()
    {
        // Arrange
        _adminDataMock
            .Setup(d => d.CreateInvitationAsync(It.IsAny<Invitation>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<InvitationManager> cut = _ctx.Render<InvitationManager>();

        // Act: メールアドレスを入力
        var input = cut.Find("input");
        input.Change("test@example.com");

        // Act: Generate Invite Link ボタンをクリック
        var buttons = cut.FindAll("button");
        var generateButton = buttons.FirstOrDefault(b => b.TextContent.Contains("Generate Invite Link"));
        Assert.NotNull(generateButton);
        generateButton.Click();

        // Assert: AdminDataProvider の作成処理が呼ばれ、スナックバーが表示されたことを検証
        _adminDataMock.Verify(
            d => d.CreateInvitationAsync(It.Is<Invitation>(i => i.Email == "test@example.com" && i.OwnerId == AdminUserId)),
            Times.Once);

        _snackbarMock.Verify(
            s => s.Add("Invitation created successfully.", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void ClickingDeleteButton_CallsDeleteAndShowsSnackbar()
    {
        // Arrange: 1件招待が存在する状態で描画
        var existingInvitation = new Invitation
        {
            Id = 99,
            Email = "target@example.com",
            InvitationCode = "INV-999",
            CreatedDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(7),
            OwnerId = AdminUserId
        };

        _adminDataMock
            .Setup(d => d.GetInvitationsAsync())
            .ReturnsAsync([existingInvitation]);

        _adminDataMock
            .Setup(d => d.DeleteInvitationAsync(It.IsAny<Invitation>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<InvitationManager> cut = _ctx.Render<InvitationManager>();

        // Act: 削除ボタン (Action 列のボタン) をクリック
        var deleteButton = cut.Find("td[data-label='Action'] button");
        Assert.NotNull(deleteButton);
        deleteButton.Click();

        // Assert: 削除処理が呼び出され、スナックバーが表示されたことを検証
        _adminDataMock.Verify(
            d => d.DeleteInvitationAsync(It.Is<Invitation>(i => i.Id == 99)),
            Times.Once);

        _snackbarMock.Verify(
            s => s.Add("Invitation deleted.", Severity.Info, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}