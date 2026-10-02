using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
/// <see cref="ReactionCommentDialog"/> の単体テスト。
/// 10秒タイマー、マウスホバーによるタイマー停止、コメント保存およびキャンセルボタン非表示を検証する。
/// </summary>
public sealed class ReactionCommentDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ReactionCommentDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    [Fact]
    public async Task ReactionCommentDialog_RendersTitleAndCountdown()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReactionCommentDialog>
        {
            { x => x.ReactionTagName, "真実" }
        };

        // Act
        _ = await dialogService.ShowAsync<ReactionCommentDialog>("リアクションを追加", parameters);
        host.WaitForState(() => host.Markup.Contains("真実 リアクションにコメントを追加"));

        // Assert
        Assert.Contains("真実 リアクションにコメントを追加", host.Markup);
        Assert.Contains("10秒以内にカーソルを合わせると永久表示になります", host.Markup);
    }

    [Fact]
    public async Task ReactionCommentDialog_Hover_StopsTimer()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReactionCommentDialog>
        {
            { x => x.ReactionTagName, "善" }
        };

        // Act
        _ = await dialogService.ShowAsync<ReactionCommentDialog>("リアクションを追加", parameters);
        host.WaitForState(() => host.Markup.Contains("善 リアクションにコメントを追加"));

        // ダイアログ最上位コンテナを検索して mouseenter を発火
        var container = host.Find(".reaction-comment-dialog-container");
        container.MouseEnter();

        host.WaitForState(() => host.Markup.Contains("タイマーが停止しました"));

        // Assert
        Assert.Contains("タイマーが停止しました", host.Markup);
        Assert.DoesNotContain("10秒以内にカーソルを合わせると永久表示になります", host.Markup);
    }

    [Fact]
    public async Task ReactionCommentDialog_Save_ReturnsCommentResult()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReactionCommentDialog>
        {
            { x => x.ReactionTagName, "美" }
        };

        // Act
        IDialogReference dialogRef = await dialogService.ShowAsync<ReactionCommentDialog>("リアクションを追加", parameters);
        host.WaitForState(() => host.Markup.Contains("美 リアクションにコメントを追加"));

        // コメント入力
        var input = host.Find("textarea");
        input.Input("これは素晴らしい美です");

        // 保存ボタンをクリック
        var saveButton = host.FindAll("button").First(b => b.TextContent.Contains("保存"));
        saveButton.Click();

        DialogResult? result = await dialogRef.Result;

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.IsType<ReactionCommentDialogResult>(result.Data);
        var dialogResult = (ReactionCommentDialogResult)result.Data!;
        Assert.True(dialogResult.Saved);
        Assert.Equal("これは素晴らしい美です", dialogResult.Comment);
    }

    [Fact]
    public async Task ReactionCommentDialog_DoesNotRenderCancelButton()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ReactionCommentDialog>
        {
            { x => x.ReactionTagName, "真実" }
        };

        // Act
        _ = await dialogService.ShowAsync<ReactionCommentDialog>("リアクションを追加", parameters);
        host.WaitForState(() => host.Markup.Contains("真実 リアクションにコメントを追加"));

        // Assert: キャンセルボタンが存在しないことを検証
        Assert.DoesNotContain(host.FindAll("button"), b => b.TextContent.Contains("キャンセル"));
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