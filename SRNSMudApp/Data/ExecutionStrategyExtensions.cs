using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace SRNSMudApp.Data;

/// <summary>
///     <see cref="DatabaseFacade" /> の拡張メソッド。
///     SQL Server 再実行戦略 (<see cref="SqlServerRetryingExecutionStrategy" />) 対応の
///     実行ヘルパーを提供する。
///     【重要 (DATA-01/DATA-02)】: <see cref="IExecutionStrategy.ExecuteAsync" /> はデリゲート全体の自動再試行機構であり、
///     トランザクション境界および ChangeTracker の状態ロールバックは自動的には行われません。
///     再試行時にエンティティの重複追跡や二重登録（double-minting）を防ぐため、
///     複数回の <c>SaveChangesAsync</c> を呼び出す場合やエンティティを追加・変更する操作では、
///     operation デリゲートの先頭で <c>dbContext.ChangeTracker.Clear();</c> を呼び出し、
///     内部で <c>await using var tx = await database.BeginTransactionAsync(cancellationToken);</c>
///     を開始して最後に <c>await tx.CommitAsync(cancellationToken);</c> を呼び出す必要があります。
/// </summary>
public static class ExecutionStrategyExtensions
{
    /// <summary>
    ///     再実行戦略を使用して、データベース操作を実行する。
    ///     一時的な接続障害発生時にデリゲート全体が再試行される。
    ///     注意: 複数回の SaveChangesAsync を含む処理をアトミックに実行する場合は、
    ///     operation デリゲートの内部で明示的に BeginTransactionAsync / CommitAsync を使用してください。
    /// </summary>
    /// <typeparam name="TResult">戻り値の型。</typeparam>
    /// <param name="database">対象の <see cref="DatabaseFacade" />。</param>
    /// <param name="operation">実行する操作デリゲート。複数保存を伴う場合は内部でトランザクションを開始してください。</param>
    /// <returns>操作の結果。</returns>
    public static async Task<TResult> ExecuteWithStrategyAsync<TResult>(
        this DatabaseFacade database,
        Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(operation);
    }

    /// <summary>
    ///     再実行戦略を使用して、データベース操作を実行する（戻り値なし）。
    ///     一時的な接続障害発生時にデリゲート全体が再試行される。
    ///     注意: 複数回の SaveChangesAsync を含む処理をアトミックに実行する場合は、
    ///     operation デリゲートの内部で明示的に BeginTransactionAsync / CommitAsync を使用してください。
    /// </summary>
    /// <param name="database">対象の <see cref="DatabaseFacade" />。</param>
    /// <param name="operation">実行する操作デリゲート。複数保存を伴う場合は内部でトランザクションを開始してください。</param>
    public static async Task ExecuteWithStrategyAsync(
        this DatabaseFacade database,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(operation);
    }

    /// <summary>
    ///     状態を保持しながら再実行戦略で操作を実行する（高度なシナリオ用）。
    ///     状態オブジェクトは再試行時にも同じインスタンスが渡されるため、ミュータブルな状態を持たせる場合は注意が必要。
    ///     注意: 複数回の SaveChangesAsync を含む処理をアトミックに実行する場合は、
    ///     operation デリゲートの内部で明示的に BeginTransactionAsync / CommitAsync を使用してください。
    /// </summary>
    /// <typeparam name="TState">状態の型。</typeparam>
    /// <typeparam name="TResult">戻り値の型。</typeparam>
    /// <param name="database">対象の <see cref="DatabaseFacade" />。</param>
    /// <param name="state">状態オブジェクト。</param>
    /// <param name="operation">実行する操作デリゲート。</param>
    /// <returns>操作の結果。</returns>
    public static async Task<TResult> ExecuteWithStrategyAsync<TState, TResult>(
        this DatabaseFacade database,
        TState state,
        Func<TState, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(state, operation);
    }
}