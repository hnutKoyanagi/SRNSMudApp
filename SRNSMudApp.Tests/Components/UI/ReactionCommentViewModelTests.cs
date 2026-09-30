namespace SRNSMudApp.Tests.Components.UI;

using System;
using System.Threading;
using System.Threading.Tasks;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Models;

using Xunit;

public class ReactionCommentViewModelTests
{
    [Fact]
    public void InitialState_IsCorrect()
    {
        var vm = new ReactionCommentViewModel();

        Assert.Equal(10, vm.RemainingSeconds);
        Assert.Null(vm.Comment);
        Assert.False(vm.IsCursorHovered);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void Tick_DecrementsRemainingSecondsAndFiresStateChanged()
    {
        var vm = new ReactionCommentViewModel();
        var stateChangedFired = false;
        vm.StateChanged += () => stateChangedFired = true;

        vm.Tick();

        Assert.Equal(9, vm.RemainingSeconds);
        Assert.True(stateChangedFired);
        Assert.False(vm.IsCompleted);
    }

    [Fact]
    public void Tick_WhenCursorHovered_DoesNotDecrement()
    {
        var vm = new ReactionCommentViewModel();
        vm.OnCursorEnter();

        var stateChangedFired = false;
        vm.StateChanged += () => stateChangedFired = true;

        vm.Tick();

        Assert.Equal(10, vm.RemainingSeconds);
        Assert.False(stateChangedFired);
    }

    [Fact]
    public void Tick_WhenCompleted_DoesNotDecrement()
    {
        var vm = new ReactionCommentViewModel();
        vm.Cancel();

        vm.Tick();

        Assert.Equal(10, vm.RemainingSeconds);
    }

    [Fact]
    public void Tick_WhenReachingZero_CompletesWithTimeout()
    {
        var vm = new ReactionCommentViewModel();
        ReactionCommentDialogResult? completedResult = null;
        vm.Completed += r => completedResult = r;

        for (var i = 0; i < 10; i++)
        {
            vm.Tick();
        }

        Assert.Equal(0, vm.RemainingSeconds);
        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.True(vm.Result.Saved);
        Assert.Null(vm.Result.Comment);
        Assert.Same(vm.Result, completedResult);
    }

    [Fact]
    public void OnCursorEnter_SetsHoveredAndFiresStateChanged()
    {
        var vm = new ReactionCommentViewModel();
        var stateChangedCount = 0;
        vm.StateChanged += () => stateChangedCount++;

        vm.OnCursorEnter();

        Assert.True(vm.IsCursorHovered);
        Assert.Equal(1, stateChangedCount);

        // 再度呼んでも重複発火しない
        vm.OnCursorEnter();
        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public void Save_SetsResultWithCommentAndFiresCompleted()
    {
        var vm = new ReactionCommentViewModel
        {
            Comment = "LGTM!"
        };
        ReactionCommentDialogResult? completedResult = null;
        vm.Completed += r => completedResult = r;

        vm.Save();

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.True(vm.Result.Saved);
        Assert.Equal("LGTM!", vm.Result.Comment);
        Assert.Same(vm.Result, completedResult);

        // 二重呼び出しガード
        vm.Save();
        Assert.Equal("LGTM!", vm.Result.Comment);
    }

    [Fact]
    public void Cancel_SetsResultCancelledAndFiresCompleted()
    {
        var vm = new ReactionCommentViewModel
        {
            Comment = "Nevermind"
        };
        ReactionCommentDialogResult? completedResult = null;
        vm.Completed += r => completedResult = r;

        vm.Cancel();

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.Result);
        Assert.False(vm.Result.Saved);
        Assert.Null(vm.Result.Comment);
        Assert.Same(vm.Result, completedResult);
    }

    [Fact]
    public async Task StartCountdownAsync_CancelsGracefully()
    {
        var vm = new ReactionCommentViewModel();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await vm.StartCountdownAsync(cts.Token);

        Assert.Equal(10, vm.RemainingSeconds);
        Assert.False(vm.IsCompleted);
    }
}