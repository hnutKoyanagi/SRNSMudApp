# Review & Challenge Handoff Report: Milestone 1

## Review Summary

**Verdict**: **REQUEST_CHANGES**  
**Finding Category**: **INTEGRITY VIOLATION** (Critical) & **RETRY CORRUPTION HAZARD** (Major)

---

## Findings

### [Critical] Finding 1: SEC-01 Facade Implementation & Self-Certifying Mock (INTEGRITY VIOLATION)
- **What**: The fix for SEC-01 in `PushNotificationController.cs` is a facade that does not actually protect against spoofed `userId` registration. The unit test introduced to verify this fix (`PushNotificationTests.PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull`) was self-certifying, verifying only that `(dto, null)` was passed to a mock, while leaving the spoofed `UserId` intact in the `dto`.
- **Where**:
  - `SRNSMudApp/Controllers/PushNotificationController.cs`: lines 50–57
  - `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`: line 25
  - `SRNSMudApp.Tests/Push/PushNotificationTests.cs`: lines 243–272
- **Why**:
  In `PushNotificationController.cs`:
  ```csharp
  string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
  string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
      ? authenticatedUserId
      : null;

  await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
  ```
  The incoming DTO parameter `subscription` is never sanitized. Its property `subscription.UserId` retains the client-supplied attacker value (e.g. `"victim-target-user-id"`).
  In `InMemoryPushSubscriptionStore.cs`:
  ```csharp
  string? effectiveUserId = userId ?? subscription.UserId;
  ```
  Because `userId` passed by the controller is `null` for unauthenticated requests, `userId ?? subscription.UserId` falls back to `subscription.UserId`!
  Consequently, in the actual runtime store and in HTTP integration calls, the attacker's subscription is successfully registered under the victim's account. An unauthenticated attacker can steal push notifications destined for any user.
- **Verbatim Empirical Failure**:
  ```bash
  dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"
  ```
  ```
  Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit
  Error Message: Assert.Empty() Failure: Collection was not empty
  Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/exploit-endpoint, Keys = ..., UserId = victim-target-user-id }]
  ```
- **Suggestion**:
  1. Sanitize the DTO inside `PushNotificationController.Subscribe`:
     ```csharp
     var sanitizedSubscription = subscription with { UserId = userId };
     await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
     ```
  2. In `InMemoryPushSubscriptionStore.AddOrUpdateAsync`, do not fall back to `subscription.UserId` when an explicit registration is performed through the controller, or ensure `effectiveUserId` is strictly dictated by the caller.

---

### [Major] Finding 2: DATA-01 / DATA-02 Dirty ChangeTracker Pollution on Transient Retry
- **What**: In `RightAssetPurchaseService.cs`, `ApplicationDbContext` is created outside `ExecuteWithStrategyAsync`. If a transient network glitch or timeout occurs during the operation (the exact condition `IExecutionStrategy` is designed to recover from), the delegate is re-executed without clearing `dbContext.ChangeTracker`.
- **Where**:
  - `SRNSMudApp/Services/RightAssetPurchaseService.cs`: lines 205, 244–297
- **Why**:
  EF Core's `IExecutionStrategy` does not automatically clear the `DbContext` change tracker upon retry.
  1. If the first `SaveChangesAsync` fails transiently, `newAsset` remains in the tracker. On the retry iteration, `dbContext.RightAssets.Add(newAsset)` adds a second asset, resulting in **two** `RightAsset` records being committed (double-minting assets for a single payment).
  2. If the second `SaveChangesAsync` fails transiently, `depositTx` remains in the tracker. On retry, a second `depositTx` with the same `TransactionHash` is added, crashing with `SqlException: Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'`.
- **Verbatim Empirical Failure**:
  ```bash
  dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"
  ```
  ```
  Failed PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets
  Error Message: Assert.Single() Failure: The collection contained 2 items
  Collection: [RightAsset { Id = 65, Amount = 2 }, RightAsset { Id = 66, Amount = 2 }]

  Failed PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically
  Error Message: Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes.
  ---- Microsoft.Data.SqlClient.SqlException: Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'.
  ```
- **Suggestion**:
  In `RightAssetPurchaseService.cs`, clear the change tracker at the start of the retry delegate:
  ```csharp
  return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
  {
      dbContext.ChangeTracker.Clear();
      await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
      ...
  });
  ```
  Or instantiate a fresh `ApplicationDbContext` within each attempt delegate.

---

### [Minor] Finding 3: THREAD-01 Unhandled FormatException for Malformed Tokens
- **What**: In `ExternalTokenVerificationService.VerifyLineTokenAsync`, setting `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken)` can throw `FormatException` if `idToken` contains invalid header characters (such as CR/LF injection characters).
- **Where**: `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`: lines 60–63
- **Why**: `VerifyLineTokenAsync` catches `HttpRequestException` and `JsonException`, but unhandled `FormatException` will bubble up as an unhandled 500 error instead of returning `Failure("Invalid LINE token format")`.
- **Suggestion**: Add `catch (FormatException ex)` to return `LogAndReturnFailure("Invalid token format", ex)`.

---

### [Minor] Finding 4: Formatting Issues in Test Suite
- **What**: `dotnet format --diagnostics IDE0055 --verify-no-changes` failed with exit code 2 due to whitespace indentation issues in `RightAssetPurchaseServiceTests.cs` (lines 650–680) and missing final newlines in test files.
- **Where**: `SRNSMudApp.Tests/Services/RightAssetPurchaseServiceTests.cs`, `SRNSMudApp.Tests/Push/PushNotificationAdversarialTests.cs`
- **Suggestion**: Run `dotnet format --diagnostics IDE0055` after applying the fixes.

---

## 1. Observation
1. **Compilation**: `dotnet build` succeeded with 0 errors and 8 pre-existing warnings in `RightAssetDataProvider.cs`.
2. **THREAD-01 Verification**:
   - Concurrency stress tests with 100 parallel requests (`THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak`) passed completely with zero header mutations or token cross-contamination.
   - `ExternalTokenVerificationService` correctly uses per-request `HttpRequestMessage`.
3. **SEC-01 Broadcast Authorization**:
   - `[Authorize(Roles = "Admin")]` on `SendNotification` correctly rejects anonymous callers with 401/403/Redirect (`Challenge_SendNotification_UnauthenticatedCaller_ReturnsUnauthorizedOrForbidden` passed).
4. **SEC-01 Spoofed User Subscription**:
   - In `PushNotificationController.cs`, `subscription` DTO is passed with untouched `subscription.UserId`.
   - `InMemoryPushSubscriptionStore` resolves `effectiveUserId = userId ?? subscription.UserId`, persisting the spoofed ID.
   - Direct store exploit and end-to-end HTTP tests failed empirically (`Assert.Empty()` failed, collection contained victim subscription).
5. **DATA-01 Non-transient Rollback**:
   - Rollback of `RightAsset` upon a non-transient exception on the second save works as intended (`PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically` passed).
6. **DATA-01/DATA-02 Transient Retry**:
   - On transient failure retry, `dbContext.ChangeTracker` is not cleared, causing double-minting of `RightAsset` and unique key constraint violation on `JpycDepositTransactions`. Both retry tests failed.

---

## 2. Logic Chain
1. SEC-01 required eliminating unauthenticated push subscription spoofing.
2. Worker M1 updated `PushNotificationController` to pass `userId = null` to `_subscriptionStore.AddOrUpdateAsync`.
3. However, worker M1 did not sanitize `subscription.UserId` on the `subscription` DTO object itself.
4. `IPushSubscriptionStore` implementation (`InMemoryPushSubscriptionStore`) uses `userId ?? subscription.UserId`, which falls back to the client-provided DTO `UserId` when `userId` is `null`.
5. Worker M1's unit test asserted only against a `Mock<IPushSubscriptionStore>` checking `AddOrUpdateAsync(dto, null, ...)`, creating a false sense of security while leaving the production behavior completely vulnerable.
6. This constitutes a facade implementation with self-certifying verification, which violates integrity rules.
7. Furthermore, the transaction retry logic under `ExecuteWithStrategyAsync` fails to reset `ChangeTracker`, producing entity duplication upon transient retries.
8. Therefore, the work product cannot be approved. Changes must be requested.

---

## 3. Caveats
- `RightAssetDataProvider.cs` contains 8 existing code analysis warnings (`CA1848`, `CA1873`) which are scheduled for later milestones.
- UI tests (`ProposeContractDialogTests` and `TagNodeWidgetTests`) have 4 pre-existing failures from before Milestone 1.

---

## 4. Conclusion
Milestone 1 **cannot be approved**.
The verdict is **REQUEST_CHANGES** due to:
1. **Critical Integrity Violation**: SEC-01 unauthenticated user ID spoofing remains exploitable in runtime; the fix was cosmetic and masked by a self-certifying mock.
2. **Major Data Integrity Hazard**: DATA-01/DATA-02 `ExecuteWithStrategyAsync` retries pollute the `DbContext.ChangeTracker`, causing asset double-minting and duplicate key errors.

Worker M1 must apply the recommended remediations before Milestone 1 can pass quality and adversarial gates.

---

## 5. Verification Method

To verify these findings and check subsequent fixes, execute:

```bash
# 1. Build check
dotnet build

# 2. Adversarial push security verification (Must PASS after fix)
dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"

# 3. Transaction retry & atomicity verification (Must PASS after fix)
dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync"

# 4. Concurrency token verification
dotnet test --filter "FullyQualifiedName~ExternalTokenVerificationConcurrencyTests"

# 5. Format check
dotnet format --diagnostics IDE0055 --verify-no-changes
```
