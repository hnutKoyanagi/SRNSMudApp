namespace SRNSMudApp.Components.UI;

using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models;

/// <summary>
///     ReactionCommentDialog のタイマー制御、コメント入力、結果生成ロジックをカプセル化する ViewModel。
/// </summary>
public sealed class ReactionCommentViewModel
{
    public const int DefaultCountdownSeconds = 10;

    public ReactionCommentViewModel()
    {
        RemainingSeconds = DefaultCountdownSeconds;
    }

    public string? Comment { get; set; }
    public int RemainingSeconds { get; private set; }
    public bool IsCursorHovered { get; private set; }
    public bool IsCompleted { get; private set; }
    public ReactionCommentDialogResult? Result { get; private set; }

    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Blazor callback action pattern")]
    public event Action? StateChanged;

    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Blazor callback action pattern")]
    public event Action<ReactionCommentDialogResult>? Completed;

    /// <summary>
    ///     カーソルがダイアログに入ったときの処理。タイマーを停止する。
    /// </summary>
    public void OnCursorEnter()
    {
        if (IsCursorHovered || IsCompleted)
        {
            return;
        }

        IsCursorHovered = true;
        StateChanged?.Invoke();
    }

    /// <summary>
    ///     カウントダウンの 1 秒進める。
    ///     0 秒に達した場合は自動的にタイムアウト確定（コメントなしで保存）となる。
    /// </summary>
    public void Tick()
    {
        if (IsCursorHovered || IsCompleted)
        {
            return;
        }

        RemainingSeconds--;
        StateChanged?.Invoke();

        if (RemainingSeconds <= 0)
        {
            IsCompleted = true;
            Result = new ReactionCommentDialogResult(true, null);
            Completed?.Invoke(Result);
        }
    }

    /// <summary>
    ///     コメント付きでリアクションを保存する。
    /// </summary>
    public void Save()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Result = new ReactionCommentDialogResult(true, Comment);
        Completed?.Invoke(Result);
    }

    /// <summary>
    ///     コメント入力をキャンセルする。
    /// </summary>
    public void Cancel()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Result = new ReactionCommentDialogResult(false, null);
        Completed?.Invoke(Result);
    }

    /// <summary>
    ///     非同期カウントダウンタイマーループを実行する。
    /// </summary>
    public async Task StartCountdownAsync(CancellationToken ct, TimeProvider? timeProvider = null)
    {
        TimeProvider provider = timeProvider ?? TimeProvider.System;

        try
        {
            while (RemainingSeconds > 0 && !IsCursorHovered && !IsCompleted && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), provider, ct);

                if (IsCursorHovered || IsCompleted || ct.IsCancellationRequested)
                {
                    break;
                }

                Tick();
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルは正常動作
        }
    }
}