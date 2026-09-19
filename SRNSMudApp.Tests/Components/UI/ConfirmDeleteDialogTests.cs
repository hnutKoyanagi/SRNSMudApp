using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UI;

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
/// <see cref="ConfirmDeleteDialog"/> の単体テスト。
/// メッセージ表示、キャンセル、削除確定のダイアログ結果を検証する。
/// </summary>
public sealed class ConfirmDeleteDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConfirmDeleteDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    [Fact]
    public async Task ConfirmDeleteDialog_RendersMessage()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, "テストタグを削除しますか？" }
        };

        // Act
        _ = await dialogService.ShowAsync<ConfirmDeleteDialog>("タグの削除", parameters);
        host.WaitForState(() => host.Markup.Contains("テストタグを削除しますか？"));

        // Assert
        Assert.Contains("テストタグを削除しますか？", host.Markup);
        var submitButton = host.Find("button[data-testid='confirm-delete-button']");
        Assert.NotNull(submitButton);
    }

    private sealed class DialogHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
        }
    }
}