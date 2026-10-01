namespace SRNSMudApp.Components.UI;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Models;

/// <summary>
///     リアクションコメント入力ダイアログコンポーネントのコードビハインド。
///     10秒自動消滅タイマーのカウントダウン、カーソルホバーによるタイマー停止、コメント入力・保存を制御する。
/// </summary>
public partial class ReactionCommentDialog : ComponentBase, IDisposable
{
    [CascadingParameter] public IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public string ReactionTagName { get; set; } = "リアクション";

    private string? Comment { get; set; }
    private int _remainingSeconds = 10;
    private bool _isCursorHovered;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    protected override void OnInitialized()
    {
        _cts = new CancellationTokenSource();
        _ = StartCountdownAsync(_cts.Token);
    }

    private async Task StartCountdownAsync(CancellationToken ct)
    {
        try
        {
            while (_remainingSeconds > 0 && !_isCursorHovered && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                if (_isCursorHovered || ct.IsCancellationRequested)
                {
                    break;
                }

                _remainingSeconds--;
                await InvokeAsync(StateHasChanged);
            }

            if (!_isCursorHovered && _remainingSeconds <= 0 && !ct.IsCancellationRequested && !_disposed)
            {
                await InvokeAsync(() =>
                {
                    if (!_disposed)
                    {
                        MudDialog.Close(DialogResult.Ok(new ReactionCommentDialogResult(true, null)));
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は正常終了
        }
    }

    public void OnCursorEnter()
    {
        if (_isCursorHovered)
        {
            return;
        }

        _isCursorHovered = true;
        _cts?.Cancel();
    }

    private void Save()
    {
        _cts?.Cancel();
        MudDialog.Close(DialogResult.Ok(new ReactionCommentDialogResult(true, Comment)));
    }

    private void Cancel()
    {
        _cts?.Cancel();
        MudDialog.Close(DialogResult.Ok(new ReactionCommentDialogResult(false, null)));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }

            _disposed = true;
        }
    }
}