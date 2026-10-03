# Handoff Report: Empirical Challenge & Stress-Testing for DATA-01 & DATA-02

- **Agent**: `challenger_m1_it2_2`
- **Milestone**: Milestone 1 Iteration 2 (Data Integrity, Transaction Boundaries & Retry Mechanism)
- **Target Audience**: Orchestrator Parent (`077cc920-cc9e-40bc-99e6-163a40d89fa9`) & Teamwork Auditors/Reviewers
- **Date**: 2026-10-02T18:23:00Z
- **Type**: Hard Handoff (Task Complete)
- **Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Direct Source Code Observations

1. **`RightAssetPurchaseService.cs` (lines 244–252, 299–302)**:
   In `PurchaseRightAssetWithJpycAsync`:
   ```csharp
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       // DATA-01 / DATA-02: 一時的障害によるリトライ発生時、前回試行で失敗・ロールバックされたエンティティが
       // ChangeTracker に残存していると、重複登録（double-minting）や一意キー制約違反が発生する。
       // 各試行の開始時に ChangeTracker をクリアして常にクリーンな状態でトランザクションを再実行する。
       dbContext.ChangeTracker.Clear();

       await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

       // 新規 RightAsset の発行
       var newAsset = new RightAsset
       {
           TargetTagId = tag.Id,
           OwnerId = userId,
           Amount = request.Amount,
           IsBurned = false
       };

       dbContext.RightAssets.Add(newAsset);
       await dbContext.SaveChangesAsync(cancellationToken);
       ...
       await dbContext.SaveChangesAsync(cancellationToken);

       await transaction.CommitAsync(cancellationToken);

       return Result.Ok(newAsset);
   });
   ```

2. **`ApplicationDbContext.cs` (lines 276–278)**:
   Unique index on `JpycDepositTransaction.TransactionHash`:
   ```csharp
   _ = builder.Entity<JpycDepositTransaction>()
       .HasIndex(t => t.TransactionHash)
       .IsUnique();
   ```

3. **`ExecutionStrategyExtensions.cs` (lines 14–21, 34–43)**:
   Explicit contracts for SQL Server retrying execution strategy:
   ```csharp
   /// 【重要 (DATA-01/DATA-02)】: <see cref="IExecutionStrategy.ExecuteAsync" /> はデリゲート全体の自動再試行機構であり、
   /// トランザクション境界および ChangeTracker の状態ロールバックは自動的には行われません。
   /// 再試行時にエンティティの重複追跡や二重登録（double-minting）を防ぐため、
   /// 複数回の <c>SaveChangesAsync</c> を呼び出す場合やエンティティを追加・変更する操作では、
   /// operation デリゲートの先頭で <c>dbContext.ChangeTracker.Clear();</c> を呼び出し、
   /// 内部で <c>await using var tx = await database.BeginTransactionAsync(cancellationToken);</c>
   /// を開始して最後に <c>await tx.CommitAsync(cancellationToken);</c> を呼び出す必要があります。
   ```

### 1.2 Empirical Test Execution Observations

All commands were executed directly by `challenger_m1_it2_2`:

1. **Transient Failure on First Save (RightAsset duplication check)**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets"`
   - Output: `Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 21 s`
   - Verification: `verifyDb.RightAssets.Where(a => a.OwnerId == userId)` contains **exactly 1 record** (`Assert.Single(assets)` passed). Exactly 0 double-minting occurred.

2. **Transient Failure on Second Save (Unique key collision check)**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically"`
   - Output: `Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 16 s`
   - Verification: Zero unique index collisions on `IX_JpycDepositTransactions_TransactionHash`. Exactly 1 `RightAsset` and exactly 1 `JpycDepositTransaction` committed (`Assert.Single(assets)` and `Assert.Single(transactions)` passed).

3. **Terminal Failure on Second Save (Atomic rollback check)**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"`
   - Output: `Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 20 s`
   - Verification: Threw expected `InvalidOperationException`. Both `verifyDb.RightAssets` and `verifyDb.JpycDepositTransactions` are **empty** (`Assert.Empty(assets)` and `Assert.Empty(transactions)` passed). Complete transactional rollback verified.

4. **Complete `RightAssetPurchaseServiceTests` Suite**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~RightAssetPurchaseServiceTests"`
   - Output: `Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 14 s`

5. **Consolidated Milestone 1 Suite (SEC-01, DATA-01/02, THREAD-01)**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"`
   - Output: `Passed!  - Failed: 0, Passed: 45, Skipped: 0, Total: 45, Duration: 16 s`

6. **Format & Solution Build Verification**:
   - `dotnet format --diagnostics IDE0055 --verify-no-changes` -> Exit Code 0 (0 violations).
   - `dotnet build` -> Exit Code 0 (0 warnings, 0 errors).

---

## 2. Logic Chain

1. *Premise 1*: Under EF Core with `SqlServerRetryingExecutionStrategy`, when a transient failure occurs (such as connection loss or command timeout) during a transaction, EF Core aborts the current SQL transaction and re-invokes the entire delegate supplied to `ExecuteWithStrategyAsync`.
2. *Premise 2*: In-memory `ChangeTracker` does not automatically detach or un-track entities added in a rolled-back transaction. If attempt 1 fails at first save (`RightAsset`) or second save (`JpycDepositTransaction`), attempt 2 will retain those tracked instances unless cleared.
3. *Observation 1.1*: `RightAssetPurchaseService.cs` line 249 executes `dbContext.ChangeTracker.Clear();` as the very first statement inside the execution strategy delegate, followed immediately by `await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);`.
4. *Deduction from 1.1*: Because `ChangeTracker.Clear()` runs on every attempt prior to beginning a new transaction, any entity state accumulated during a previous failed attempt is completely discarded from the context's tracking graph.
5. *Observation 1.2*: Empirical execution of `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets` with an interceptor throwing a transient `TimeoutException` on attempt 1 confirmed that after automatic retry, exactly 1 `RightAsset` exists in the database.
6. *Observation 1.2*: Empirical execution of `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically` with an interceptor throwing a transient `TimeoutException` on attempt 1 of the second save confirmed that after automatic retry, 0 unique index collisions occurred on `IX_JpycDepositTransactions_TransactionHash`, exactly 1 `RightAsset` exists, and exactly 1 `JpycDepositTransaction` exists.
7. *Observation 1.2*: Empirical execution of `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically` confirmed that if the second save permanently fails, the database transaction rolls back, leaving 0 `RightAsset` and 0 `JpycDepositTransaction` records.
8. *Conclusion*: The DATA-01 and DATA-02 remediations guarantee idempotency, atomicity, 0 double-minting, and 0 unique key collisions under transient retry scenarios.

---

## 3. Adversarial Review & Stress Test Summary

### Challenge Summary
- **Overall risk assessment**: **LOW** (Defenses are complete and empirically verified)

### Challenges Evaluated

#### Challenge 1: Entity Graph Pollution on Retrying First Save
- **Assumption challenged**: Calling `dbContext.RightAssets.Add(newAsset)` and having `SaveChangesAsync` fail will leave `newAsset` in `ChangeTracker`, resulting in 2 `RightAsset` rows on attempt 2.
- **Attack scenario**: Inject transient `TimeoutException` on first `SaveChangesAsync` invocation.
- **Empirical result**: `ChangeTracker.Clear()` resets the tracker. Exactly 1 `RightAsset` created. **PASS**.

#### Challenge 2: Unique Index Collision on Retrying Second Save
- **Assumption challenged**: Calling `dbContext.JpycDepositTransactions.Add(depositTx)` with `normalizedTx` and having the second `SaveChangesAsync` fail will cause attempt 2 to attempt adding another entity with identical `TransactionHash` while the first is still tracked, triggering `IX_JpycDepositTransactions_TransactionHash` violation.
- **Attack scenario**: Inject transient `TimeoutException` on second `SaveChangesAsync` invocation.
- **Empirical result**: `ChangeTracker.Clear()` detaches the uncommitted `depositTx`. Attempt 2 cleanly inserts with 0 unique constraint collisions. **PASS**.

#### Challenge 3: Partial Commit / Double-Spending on Unrecoverable Second Save Failure
- **Assumption challenged**: Because there are two sequential `SaveChangesAsync` calls, if the second save throws an unhandled exception, the first save (`RightAsset`) might remain committed.
- **Attack scenario**: Inject fatal `InvalidOperationException` on second `SaveChangesAsync` without retry.
- **Empirical result**: `await using var transaction = await dbContext.Database.BeginTransactionAsync()` ensures full rollback. 0 `RightAsset` rows exist in DB. **PASS**.

#### Challenge 4: Detachment of Scalar Dependencies (`tag`, `wallet`) Outside Delegate
- **Assumption challenged**: `tag` and `wallet` are queried before entering `ExecuteWithStrategyAsync`. Does `ChangeTracker.Clear()` detach them and cause navigation errors?
- **Inspection result**: In lines 256, 269, 273, the service only copies scalar values (`TargetTagId = tag.Id`, `DepositAddress = wallet.DepositAddress`), not navigation objects. Detachment of `tag`/`wallet` has zero side effects on entity persistence. **PASS**.

---

## 4. Caveats

1. **Pre-Existing Unrelated UI Tests**:
   - 4 pre-existing UI tests in `SRNSMudApp.Tests/Components/` (`ProposeContractDialogTests`, `TagNodeWidgetTests`) are known historical failures unrelated to Milestone 1 scope and were not modified per project constraints.
2. **Deferred Milestones**:
   - Per user instructions, Milestones 2 and 3 code changes are deferred to recommendations in the final architecture report.

---

## 5. Conclusion & Explicit Verdict

### **Verdict: APPROVE**

- **DATA-01 (Financial Atomicity)**: Verified. Two-phase commit hazard and double-spending risk are eliminated via single atomic transaction wrapping both `SaveChangesAsync` calls.
- **DATA-02 (Retry Idempotency & Clean Boundary)**: Verified. `dbContext.ChangeTracker.Clear()` at delegate inception guarantees 0 double-minting and 0 unique index collisions during transient retries.
- **Build & Quality**: Clean `dotnet build` (0 warnings, 0 errors) and clean `dotnet format --diagnostics IDE0055 --verify-no-changes` (0 violations).

---

## 6. Verification Method

To independently reproduce the empirical verification:

```bash
# 1. Format verification
dotnet format --diagnostics IDE0055 --verify-no-changes

# 2. Build verification
dotnet build

# 3. DATA-01 / DATA-02 Transient Retry Test on First Save (0 double-minting)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets"

# 4. DATA-01 / DATA-02 Transient Retry Test on Second Save (0 unique index collisions)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically"

# 5. DATA-01 / DATA-02 Rollback Atomicity Test
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"

# 6. Entire RightAssetPurchaseService test suite (22 tests)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~RightAssetPurchaseServiceTests"

# 7. Consolidated Milestone 1 suite (45 tests)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"
```
