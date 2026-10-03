# Handoff Report: Challenger M1-2 (Transaction & Data Integrity Verification)

## 1. Observation

### Observation 1: Implementation in `RightAssetPurchaseService.cs`
In `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 205-298):
```csharp
205: await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken);
...
244: return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
245: {
246:     await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
247: 
248:     // 新規 RightAsset の発行
249:     var newAsset = new RightAsset
250:     {
251:         TargetTagId = tag.Id,
252:         OwnerId = userId,
253:         Amount = request.Amount,
254:         IsBurned = false
255:     };
256: 
257:     dbContext.RightAssets.Add(newAsset);
258:     await dbContext.SaveChangesAsync(cancellationToken);
259: 
260:     // JpycDepositTransaction レコードの保存（生成された RightAsset.Id を紐付け）
261:     var depositTx = new JpycDepositTransaction
262:     {
...
270:         RightAssetId = newAsset.Id,
...
273:     };
274: 
275:     dbContext.JpycDepositTransactions.Add(depositTx);
...
289:     dbContext.Items.Add(purchaseItem);
290: 
291:     await dbContext.SaveChangesAsync(cancellationToken);
292: 
293:     await transaction.CommitAsync(cancellationToken);
294: 
295:     return Result.Ok(newAsset);
296: });
```
Direct observation:
1. `dbContext` is instantiated at line 205, **outside** the `ExecuteWithStrategyAsync` retry delegate.
2. Inside `ExecuteWithStrategyAsync` (lines 244-296), `dbContext.ChangeTracker.Clear()` is **never invoked**, nor is a fresh `DbContext` created per attempt.
3. Two separate `SaveChangesAsync` calls (lines 258 and 291) exist within the transaction.

### Observation 2: Unique Index on `JpycDepositTransactions.TransactionHash`
In `SRNSMudApp/Data/ApplicationDbContext.cs` (lines 276-278):
```csharp
_ = builder.Entity<JpycDepositTransaction>()
    .HasIndex(t => t.TransactionHash)
    .IsUnique();
```
`TransactionHash` is configured with a strict unique index in SQL Server.

### Observation 3: Rollback on Non-Transient Failure Works
The worker's test `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically` was executed:
```bash
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"
```
Output:
```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 11 s - SRNSMudApp.Tests.dll (net11.0)
```
When an unhandled non-transient exception is thrown on the second save, `BeginTransactionAsync` / `await using var transaction` correctly rolls back uncommitted changes. Zero `RightAsset` records survive.

### Observation 4: Verbatim Failure 1 — Double-Minting on First-Save Transient Retry
When a transient failure (e.g., transient network glitch or `TimeoutException`) occurs on the first `SaveChangesAsync` (line 258) under `SqlServerRetryingExecutionStrategy`:
Command:
```bash
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave"
```
Verbatim test failure output:
```
[xUnit.net 00:00:19.30] SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [FAIL]
  Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [108 ms]
  Error Message:
   Assert.Single() Failure: The collection contained 2 items
Collection: [RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:42:39.8697040, Id = 3, IsBurned = False, ··· }, RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:42:39.8697040, Id = 4, IsBurned = False, ··· }]
```

### Observation 5: Verbatim Failure 2 — Unique Key Crash on Second-Save Transient Retry
When a transient failure occurs on the second `SaveChangesAsync` (line 291) under `SqlServerRetryingExecutionStrategy`:
Command:
```bash
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave"
```
Verbatim test failure output:
```
  Error Message:
   Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.
---- Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'. The duplicate key value is (0xa64368de20477a1cefcdc4247d417c245c74ac656c799c98e7a64a4063a0e08e).
```

---

## 2. Logic Chain

1. **State Preservation across Retries**:
   In Entity Framework Core, when an operation executed via `IExecutionStrategy.ExecuteAsync` fails due to a transient error, the strategy re-runs the entire delegate.
   However, EF Core's `ChangeTracker` does **not** roll back in-memory entity states when a database transaction rolls back. Any entity added to the `DbContext` remains in `EntityState.Added` (or `EntityState.Unchanged` if a prior `SaveChangesAsync` executed before transaction abort). (Observation 1)

2. **Mechanism of Failure 1 (Asset Duplication / Double-Minting)**:
   - On attempt 1, `newAsset` is added to `dbContext.RightAssets` (`EntityState.Added`).
   - Line 258 experiences a transient failure. The database transaction rolls back, but `newAsset` remains tracked in `dbContext.ChangeTracker`.
   - On attempt 2, the delegate runs again and executes `dbContext.RightAssets.Add(newAsset)` with a newly created instance.
   - `dbContext.ChangeTracker` now contains **two** distinct `RightAsset` entities in `EntityState.Added`.
   - When line 258 executes on attempt 2, EF Core generates `INSERT` statements for **both** instances.
   - Result: Both rows are committed to SQL Server, creating 2 `RightAsset` records for a single purchase. The user receives 2x the requested assets. (Observation 4)

3. **Mechanism of Failure 2 (Duplicate Key Crash on Retry)**:
   - On attempt 1, `newAsset` is saved, then `depositTx` (with `TransactionHash`) and `purchaseItem` are added (`EntityState.Added`).
   - Line 291 experiences a transient failure. The database transaction rolls back, but `depositTx` remains tracked in `dbContext.ChangeTracker`.
   - On attempt 2, the delegate runs again and instantiates a second `JpycDepositTransaction` with the identical `TransactionHash`, adding it to `dbContext.JpycDepositTransactions`.
   - `dbContext.ChangeTracker` now tracks **two** `JpycDepositTransaction` entries with the same `TransactionHash`.
   - When `SaveChangesAsync` is invoked, EF Core attempts to insert both instances into SQL Server.
   - SQL Server rejects the second insert due to unique index `IX_JpycDepositTransactions_TransactionHash`.
   - Result: Instead of gracefully recovering, the retry crashes with `DbUpdateException` / `SqlException` 2601. (Observation 2, Observation 5)

4. **Conclusion from Logic Chain**:
   While single-failure rollback without retry works as designed (Observation 3), `RightAssetPurchaseService` is **not safe or idempotent under ExecutionStrategy retries**. Re-attempting execution corrupts data integrity (double-minting) or crashes due to ChangeTracker pollution.

---

## 3. Caveats

- In test environments where `EnableRetryOnFailure` is omitted from `DbContextOptionsBuilder`, `ExecuteWithStrategyAsync` uses `SqlServerExecutionStrategy` which executes delegates only once, masking this bug entirely.
- In production (`Program.cs` lines 133-136, 147-150), `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30))` is explicitly configured, meaning this vulnerability is active in production.
- No caveats regarding reproducibility: both failure modes were empirically reproduced and confirmed on the project's actual SQL Server container instance.

---

## 4. Conclusion & Verdict

### Explicit Verdict: **REJECT**

While the worker successfully implemented transaction rollback for unhandled non-retried exceptions, the implementation **fails the critical requirement of retry safety and idempotence under `ExecutionStrategy`**.

### Detailed Defect Summary

| Issue ID | Severity | Impact | Description |
|---|---|---|---|
| **DATA-M1-BUG-1** | **CRITICAL** | Financial / Asset Duplication | Transient error on first save causes `RightAsset` to be minted twice upon retry (double-minting). |
| **DATA-M1-BUG-2** | **CRITICAL** | Service Outage / Retry Failure | Transient error on second save crashes retry with unique key collision on `TransactionHash`. |

### Recommended Mitigations (for Worker)

1. **Clear ChangeTracker inside the retry lambda**:
   At the very beginning of the `ExecuteWithStrategyAsync` delegate in `RightAssetPurchaseService.cs` (line 246):
   ```csharp
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       dbContext.ChangeTracker.Clear();
       await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
       ...
   ```
   Or better yet, instantiate a fresh `DbContext` per attempt:
   ```csharp
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       await using var opContext = await _dbFactory.CreateDbContextAsync(cancellationToken);
       await using var transaction = await opContext.Database.BeginTransactionAsync(cancellationToken);
       ...
   ```
2. **Update `ExecutionStrategyExtensions.cs` documentation**:
   Explicitly warn callers that multi-save or entity-creating delegates executed under `ExecuteWithStrategyAsync` must invoke `ChangeTracker.Clear()` or recreate the context to guarantee idempotency across retry attempts.

---

## 5. Verification Method

To independently reproduce the failures:

```bash
# 1. Reproduce Asset Duplication on First Save Transient Retry:
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave"

# 2. Reproduce Unique Key Crash on Second Save Transient Retry:
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave"

# 3. Verify Baseline Rollback (Passes):
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"
```

**Invalidation Conditions**:
The rejection is invalidated if and only if both retry stress tests pass cleanly, verifying that:
1. Exactly one `RightAsset` record is created after a transient failure and subsequent retry.
2. The retry successfully completes without unique index collisions or unhandled exceptions.
