# Remediation Analysis Handoff Report: Milestone 1 Iteration 2

**Author**: `explorer_m1_it2_2`  
**Target Recipient**: Parent Orchestrator (`077cc920-cc9e-40bc-99e6-163a40d89fa9`) & Worker Agents  
**Date**: 2026-10-02  
**Scope**: Remediation of SEC-01 (Push Notification DTO Sanitization & Store Enforcement) and DATA-01 / DATA-02 (`RightAssetPurchaseService` Retry `ChangeTracker.Clear()`)

---

## 1. Observation

### 1.1 SEC-01 Vulnerability & Self-Certifying Mock Flaw

1. **Unsanitized DTO Forwarding in `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 50-59)**:
   ```csharp
   // SEC-01: 認証済みユーザーのIDを取得（ClaimTypes.NameIdentifier または "sub"）
   // 未認証ユーザーの場合はリクエストボディの UserId を任意に信用せず null とする（ユーザーIDのなりすまし登録を防止）
   string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
   string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
       ? authenticatedUserId
       : null;

   await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
   return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
   ```
   - Direct observation: When an unauthenticated caller sends a JSON body with `{"endpoint": "...", "keys": {...}, "userId": "victim-target-user-id"}`, `subscription.UserId` contains `"victim-target-user-id"`. The controller calculates `userId = null`, but passes the **unmodified, unsanitized** `subscription` object to `_subscriptionStore.AddOrUpdateAsync(subscription, userId, ...)`.

2. **Fallback to Untrusted DTO Field in `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 24-28)**:
   ```csharp
   string? effectiveUserId = userId ?? subscription.UserId;
   var dtoWithUserId = subscription with { UserId = effectiveUserId };
   _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
   return Task.CompletedTask;
   ```
   - Direct observation: Because `userId` is `null` for unauthenticated requests, `effectiveUserId = userId ?? subscription.UserId` evaluates to `null ?? "victim-target-user-id"`, which is `"victim-target-user-id"`. The subscription is recorded under the victim's account, allowing notification hijacking.

3. **Verbatim Failure Output of Adversarial Tests**:
   - Command: `dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"`
   ```
   [xUnit.net 00:00:02.42]     SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit [FAIL]
   [xUnit.net 00:00:02.44]     SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit [FAIL]
     Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit [2 s]
     Error Message:
      Assert.Empty() Failure: Collection was not empty
   Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/http-exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = e2e-victim-user-id }]

     Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit [19 ms]
     Error Message:
      Assert.Empty() Failure: Collection was not empty
   Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = victim-target-user-id }]

   Failed!  - Failed:     2, Passed:     2, Skipped:     0, Total:     4, Duration: 2 s - SRNSMudApp.Tests.dll (net11.0)
   ```

4. **Self-Certifying Test Assertions in `SRNSMudApp.Tests/Push/PushNotificationTests.cs` (lines 200-272)**:
   - Line 212: `mockStore.Verify(s => s.AddOrUpdateAsync(dto, "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);`
   - Line 242: `mockStore.Verify(s => s.AddOrUpdateAsync(dto, "sub-user-456", It.IsAny<CancellationToken>()), Times.Once);`
   - Line 271: `mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);`
   - Direct observation: The existing tests assert that the **unsanitized** `dto` instance was passed into `mockStore.AddOrUpdateAsync`. When `PushNotificationController` is updated to pass a sanitized DTO (`var sanitizedSubscription = subscription with { UserId = userId }`), `dto != sanitizedSubscription` because `PushSubscriptionDto` is a record type with value-equality semantics. Consequently, these mock verifications will fail unless updated to match the sanitized DTO.

---

### 1.2 DATA-01 / DATA-02 Retry Pollution in `RightAssetPurchaseService.cs`

1. **Missing `ChangeTracker.Clear()` inside Retry Delegate in `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 205, 244-258)**:
   ```csharp
   await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken);
   ...
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
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
   ```
   - Direct observation: `dbContext` is created once outside `ExecuteWithStrategyAsync`. Inside the retry delegate, `dbContext.ChangeTracker.Clear()` is **never called**.
   - Entities created before `ExecuteWithStrategyAsync` (`tag`) use `.AsNoTracking()`, and `wallet` is fetched in a separate context (`GetOrCreateUserDepositWalletAsync`), meaning `ChangeTracker` is completely empty before `ExecuteWithStrategyAsync`.

2. **Verbatim Failure Output of Retry Tests**:
   - Command: `dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"`
   ```
   [xUnit.net 00:00:12.33]     SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [FAIL]
     Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [92 ms]
     Error Message:
      Assert.Single() Failure: The collection contained 2 items
   Collection: [RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:53:34.7295670, Id = 3, IsBurned = False, ··· }, RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:53:34.7295670, Id = 4, IsBurned = False, ··· }]

     Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically [12 s]
     Error Message:
      Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.
   ---- Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'. The duplicate key value is (0x303dd749875e77395402ba40f11d9e7a5ac8affb2ab7f846e5fd14f490238dbe).

   Failed!  - Failed:     2, Passed:     0, Skipped:     0, Total:     2, Duration: 12 s - SRNSMudApp.Tests.dll (net11.0)
   ```

---

## 2. Logic Chain

1. **SEC-01 Exploit Mechanism & Defense-in-Depth Rationale**:
   - Step 1: An untrusted client submits a JSON payload containing an arbitrary `userId` to `POST /api/pushnotification/subscribe`.
   - Step 2: In `PushNotificationController.Subscribe`, unauthenticated requests calculate `userId = null`.
   - Step 3: If the controller passes the raw `subscription` to `_subscriptionStore.AddOrUpdateAsync`, `subscription.UserId` still contains the spoofed value.
   - Step 4: In `InMemoryPushSubscriptionStore.AddOrUpdateAsync`, `effectiveUserId = userId ?? subscription.UserId` evaluates to the spoofed `subscription.UserId` when `userId` is `null`.
   - Step 5: The store maps the attacker's push endpoint to the victim's `userId`. Any subsequent notifications for the victim are pushed to the attacker.
   - **Remediation**:
     - At the controller boundary: `var sanitizedSubscription = subscription with { UserId = userId };` guarantees that the DTO itself is sanitized before reaching the store or any other downstream layer.
     - At the store layer (defense-in-depth): Set `string? effectiveUserId = userId;` (or ensure `effectiveUserId` strictly adopts the authorized parameter). Even if an external caller calls `store.AddOrUpdateAsync(untrustedDto, null)`, the store will not fall back to `untrustedDto.UserId`.
     - In unit tests: Update `PushNotificationTests.cs` to verify the sanitized DTO (`dto with { UserId = ... }` or `It.Is<PushSubscriptionDto>(...)`) instead of the raw, unsanitized `dto`.

2. **DATA-01 / DATA-02 ExecutionStrategy Retry Corruption Mechanism**:
   - Step 1: `SqlServerRetryingExecutionStrategy` executes the provided delegate upon invoking `ExecuteWithStrategyAsync`.
   - Step 2: During Attempt 1, entities (`RightAsset`, `JpycDepositTransaction`, `Item`) are attached to `dbContext.ChangeTracker` in `EntityState.Added`.
   - Step 3: A transient exception (simulated by test interceptor or caused by transient SQL Server glitch) occurs during `SaveChangesAsync`.
   - Step 4: The database transaction (`await using var transaction`) is aborted and rolled back by SQL Server.
   - Step 5: **Crucial EF Core Behavior**: Transaction rollback does **not** roll back EF Core's in-memory `ChangeTracker`. All entities from Attempt 1 remain tracked in `dbContext.ChangeTracker`.
   - Step 6: When `ExecuteWithStrategyAsync` re-executes the delegate for Attempt 2:
     - On first-save transient failure: Attempt 2 creates and adds a second `RightAsset`. `ChangeTracker` now contains two `RightAsset` entities in `Added` state. `SaveChangesAsync` generates `INSERT` statements for both, resulting in duplicate assets committed to SQL Server (double-minting).
     - On second-save transient failure: `JpycDepositTransaction` from Attempt 1 remains tracked. Attempt 2 creates a new `JpycDepositTransaction` with the same `TransactionHash`. Both are scheduled for insert, causing SQL Server error 2601 on unique index `IX_JpycDepositTransactions_TransactionHash`.
   - **Remediation**:
     - Calling `dbContext.ChangeTracker.Clear()` at the very beginning of the `ExecuteWithStrategyAsync` delegate detaches all entities from previous attempts before beginning a fresh transaction.
     - Because `tag` is queried with `.AsNoTracking()`, clearing the change tracker has zero side effects on existing query state.
     - Each retry attempt begins with an empty `ChangeTracker`, ensuring complete idempotence and identical behavior to Attempt 1.

---

## 3. Caveats

1. **In-Memory Store Scope**: `InMemoryPushSubscriptionStore` is a singleton in-memory store in this application. If a persistent database-backed subscription store is introduced in future milestones, the same parameter enforcement and DTO sanitization guarantees must be maintained.
2. **Pre-existing UI Test Failures**: There are 4 pre-existing test failures in UI components (`ProposeContractDialogTests` and `TagNodeWidgetTests`). These failures are completely unrelated to Milestone 1 backend logic and were present prior to Milestone 1 work.
3. **ExecutionStrategy Scope**: `ExecutionStrategyExtensions.ExecuteWithStrategyAsync` is a general helper method. Callers performing multi-step transactional operations within retry delegates must be mindful of `ChangeTracker` state; documentation in `ExecutionStrategyExtensions.cs` should explicitly highlight this requirement.

---

## 4. Conclusion & Required Code Changes

### Summary of Required Changes

| File | Change | Purpose |
|---|---|---|
| `SRNSMudApp/Controllers/PushNotificationController.cs` | Sanitize `subscription` via `subscription with { UserId = userId }` | SEC-01: Prevent client-supplied `UserId` from escaping controller boundary |
| `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` | Use `string? effectiveUserId = userId;` | SEC-01: Store-level parameter enforcement (Defense in Depth) |
| `SRNSMudApp.Tests/Push/PushNotificationTests.cs` | Update mock verifications to verify sanitized DTO | Fix self-certifying mock assertion mismatch |
| `SRNSMudApp/Services/RightAssetPurchaseService.cs` | Add `dbContext.ChangeTracker.Clear()` at start of retry lambda | DATA-01/02: Prevent entity accumulation and unique key collisions across retries |
| `SRNSMudApp/Data/ExecutionStrategyExtensions.cs` | Update XML doc comments | Document `ChangeTracker.Clear()` requirement for multi-save operations |
| `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` | Catch `FormatException` in LINE/GitHub token verification | THREAD-01: Prevent unhandled 500 when malformed tokens are supplied |

---

### Detailed Code Patches

#### 1. `SRNSMudApp/Controllers/PushNotificationController.cs`
**Lines 50-60**:
```csharp
<<<< CURRENT
        string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
        string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
            ? authenticatedUserId
            : null;

        await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
        return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
==== PROPOSED
        // SEC-01: 認証済みユーザーのIDを取得（ClaimTypes.NameIdentifier または "sub"）
        // 未認証ユーザーの場合はリクエストボディの UserId を任意に信用せず null とする（ユーザーIDのなりすまし登録を防止）
        string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
        string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
            ? authenticatedUserId
            : null;

        // クライアントから送信された DTO の UserId を認証コンテキストで決定した userId にサニタイズして保存
        var sanitizedSubscription = subscription with { UserId = userId };
        await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
        return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
>>>>
```

#### 2. `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`
**Lines 24-29**:
```csharp
<<<< CURRENT
        string? effectiveUserId = userId ?? subscription.UserId;
        var dtoWithUserId = subscription with { UserId = effectiveUserId };
        _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
        return Task.CompletedTask;
==== PROPOSED
        // SEC-01: 認可済みコンテキストから渡された userId を厳格に適用（未指定時は null、DTO側の値を信用しない）
        string? effectiveUserId = userId;
        var dtoWithUserId = subscription with { UserId = effectiveUserId };
        _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
        return Task.CompletedTask;
>>>>
```

#### 3. `SRNSMudApp.Tests/Push/PushNotificationTests.cs`
**Lines 210-213, 240-243, 269-272**:
```csharp
<<<< CURRENT (Line 212)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(dto, "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);
==== PROPOSED (Line 212)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(It.Is<PushSubscriptionDto>(sub => sub.UserId == "auth-user-123"), "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);
>>>>

<<<< CURRENT (Line 242)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(dto, "sub-user-456", It.IsAny<CancellationToken>()), Times.Once);
==== PROPOSED (Line 242)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(It.Is<PushSubscriptionDto>(sub => sub.UserId == "sub-user-456"), "sub-user-456", It.IsAny<CancellationToken>()), Times.Once);
>>>>

<<<< CURRENT (Line 271)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);
==== PROPOSED (Line 271)
        Assert.NotNull(result);
        mockStore.Verify(s => s.AddOrUpdateAsync(It.Is<PushSubscriptionDto>(sub => sub.UserId == null), null, It.IsAny<CancellationToken>()), Times.Once);
>>>>
```

#### 4. `SRNSMudApp/Services/RightAssetPurchaseService.cs`
**Lines 244-248**:
```csharp
<<<< CURRENT
        return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // 新規 RightAsset の発行
==== PROPOSED
        return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
        {
            // DATA-01/DATA-02: リトライ時に前回失敗時の追跡中エンティティをデタッチし、二重登録・キー競合を防止する
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // 新規 RightAsset の発行
>>>>
```

#### 5. `SRNSMudApp/Data/ExecutionStrategyExtensions.cs`
**Lines 14-20**:
```csharp
///     【重要 (DATA-01/DATA-02)】: <see cref="IExecutionStrategy.ExecuteAsync" /> はデリゲート全体の自動再試行機構であり、
///     トランザクション境界は自動作成されません。
///     複数回の <c>SaveChangesAsync</c> を呼び出す場合やアトミック性が必須の操作では、
///     渡された操作デリゲート内部で <c>await using var tx = await database.BeginTransactionAsync(cancellationToken);</c>
///     を開始し、最後に <c>await tx.CommitAsync(cancellationToken);</c> を呼び出す必要があります。
///     また、リトライ時に追跡状態の累積による二重挿入やキー衝突を防ぐため、デリゲートの冒頭で
///     <c>dbContext.ChangeTracker.Clear();</c> を呼び出して状態をリセットしてください。
```

#### 6. `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`
**Lines 71-79 and 105-113**:
Add `catch (FormatException ex) { return LogAndReturnFailure("Invalid token format", ex); }` to both `VerifyLineTokenAsync` and `VerifyGithubTokenAsync`.

---

## 5. Verification Method

### 5.1 Verification Commands

Execute the following commands from the workspace root (`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp`):

```bash
# 1. Verify SEC-01 Push Notification fixes (Adversarial Tests)
dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"

# 2. Verify DATA-01 / DATA-02 ExecutionStrategy retry fixes (Transient Timeout Tests)
dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"

# 3. Verify Push Notification unit and integration tests (Non-regression)
dotnet test --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationIntegrationTests"

# 4. Verify Baseline atomic rollback without retry
dotnet test --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"

# 5. Verify THREAD-01 concurrency safety (Stress Test)
dotnet test --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak"

# 6. Verify Solution Build & Code Formatting
dotnet build
dotnet format --diagnostics IDE0055 --verify-no-changes
```

### 5.2 Invalidation Conditions
The remediation is considered successful if and only if:
1. `PushNotificationAdversarialTests` exits with code 0 (0 failed, 4 passed).
2. `PurchaseRightAssetWithJpycAsync_WhenTransientFailure*` tests exit with code 0 (0 failed, 2 passed).
3. `PushNotificationTests` exits with code 0 (all mock and direct assertions pass).
4. `dotnet build` succeeds with 0 errors and no new warnings.
