# Review & Adversarial Handoff Report: Milestone 1

## Review Summary
**Verdict**: **REQUEST_CHANGES**

**Overall Risk Assessment**: HIGH

---

## 1. Observation

1. **SEC-01 Incomplete Security Boundary in `PushNotificationController.cs` & `InMemoryPushSubscriptionStore.cs`**:
   In `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 52-57):
   ```csharp
   string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
   string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
       ? authenticatedUserId
       : null;

   await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
   ```
   The unmodified DTO `subscription` (containing `subscription.UserId` provided by the client) is passed to `_subscriptionStore.AddOrUpdateAsync`.
   In `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 25-27):
   ```csharp
   string? effectiveUserId = userId ?? subscription.UserId;
   var dtoWithUserId = subscription with { UserId = effectiveUserId };
   _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
   ```
   When an unauthenticated caller sends `{"endpoint": "https://push...", "keys": {...}, "userId": "victim-user-id"}`, `userId` is `null`. In `InMemoryPushSubscriptionStore`, `userId ?? subscription.UserId` evaluates to `null ?? "victim-user-id"`, which is `"victim-user-id"`. The subscription is recorded under `"victim-user-id"`, allowing an unauthenticated attacker to hijack notifications targeted at the victim.

2. **False-Positive Unit Test Assertion in `PushNotificationTests.cs`**:
   In `SRNSMudApp.Tests/Push/PushNotificationTests.cs` (lines 244-272):
   ```csharp
   [Fact]
   public async Task PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull()
   {
       ...
       var dto = new PushSubscriptionDto(
           "https://example.com/push/anon",
           new PushSubscriptionKeysDto("p256", "auth"),
           UserId: "victim-user-id" // 詐称
       );

       var result = await controller.Subscribe(dto, default) as OkObjectResult;

       Assert.NotNull(result);
       mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);
   }
   ```
   The test uses Moq to verify only that `mockStore.AddOrUpdateAsync(dto, null, ...)` was called. It fails to test the real implementation of `IPushSubscriptionStore` (`InMemoryPushSubscriptionStore`), hiding the fact that `dto.UserId` is still stored as the effective user ID due to `effectiveUserId = userId ?? subscription.UserId`.

3. **ChangeTracker State Retention on Retry in `RightAssetPurchaseService.cs`**:
   In `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 205, 244-258):
   ```csharp
   await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken);
   ...
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
   `dbContext` is instantiated once outside `ExecuteWithStrategyAsync`. If a transient exception triggers a retry in `IExecutionStrategy.ExecuteAsync`, `dbContext.ChangeTracker.Clear()` is not invoked. Because `newAsset` was tracked and assigned a key during the failed attempt, retrying `dbContext.RightAssets.Add(newAsset)` on the dirty context causes EF Core tracking conflicts (`InvalidOperationException`).

4. **THREAD-01 Race Condition Fix in `ExternalTokenVerificationService.cs`**:
   In `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 59-63):
   ```csharp
   using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
   request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

   HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
   ```
   `DefaultRequestHeaders.Authorization` is no longer mutated. Header configuration is strictly per-request.

5. **SEC-01 Broadcast Authorization in `PushNotificationController.cs`**:
   In `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 79-81):
   `[Authorize(Roles = "Admin")]` is applied to `SendNotification([FromBody] PushNotificationPayload? payload, ...)`.

---

## 2. Logic Chain

1. **Security Vulnerability Bypass (SEC-01)**:
   - Observation 1 demonstrates that `PushNotificationController.Subscribe` passes the raw `subscription` containing the client-provided `UserId` to `_subscriptionStore.AddOrUpdateAsync`.
   - `InMemoryPushSubscriptionStore` evaluates `effectiveUserId = userId ?? subscription.UserId`.
   - When unauthenticated, `userId` is `null`. The fallback `?? subscription.UserId` evaluates to the client-provided string.
   - Therefore, an unauthenticated client sending an arbitrary `userId` will successfully register its endpoint under that victim's account in the real in-memory store.
   - Observation 2 shows that the unit test relied on a shallow Moq verification that checked the `null` argument without validating store behavior, creating a false sense of security.

2. **Execution Strategy Fragility (DATA-01/DATA-02)**:
   - Observation 3 shows that while wrapping operations in `BeginTransactionAsync`/`CommitAsync` inside `ExecuteWithStrategyAsync` successfully solved the two-phase commit hazard and ensures database-level rollback (verified by `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically`), the `DbContext` change tracker is not cleared before retries.
   - Under SQL Server transient retries, retrying on the same dirty context causes `InvalidOperationException` due to duplicate key tracking.

3. **Concurrency Safety & Build Integrity (THREAD-01 & SEC-01 Admin Check)**:
   - Observation 4 proves that the shared `HttpClient` race condition is eliminated by using isolated `HttpRequestMessage` instances.
   - Observation 5 confirms that unauthorized users can no longer invoke the broadcast push endpoint.
   - `dotnet build` succeeds with 0 errors, and `dotnet format` reports 0 formatting violations.

---

## 3. Caveats

- `InMemoryPushSubscriptionStore` is an in-memory store. In a production clustered deployment, a distributed store (e.g., Redis or SQL Server) would be used, but in this application `InMemoryPushSubscriptionStore` is the registered singleton service handling all push subscriptions.
- The 4 pre-existing test failures in `SRNSMudApp.Tests` (`ProposeContractDialogTests` and `TagNodeWidgetTests`) are in UI components outside Milestone 1 scope and did not regress as part of these changes.

---

## 4. Conclusion & Findings

### Verdict: **REQUEST_CHANGES**

### Findings

#### [Critical] Finding 1: Unauthenticated Push Subscription Spoofing Bypass (SEC-01)
- **What**: An unauthenticated caller can still associate their push notification subscription with any victim's `userId`.
- **Where**:
  - `SRNSMudApp/Controllers/PushNotificationController.cs:57`
  - `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs:25`
- **Why**:
  `PushNotificationController.Subscribe` passes the original `subscription` DTO to `_subscriptionStore.AddOrUpdateAsync(subscription, userId, ...)`. In `InMemoryPushSubscriptionStore`, `effectiveUserId = userId ?? subscription.UserId;` falls back to `subscription.UserId` when `userId` is `null`.
- **Required Fix**:
  1. In `PushNotificationController.cs`:
     Sanitize the DTO before forwarding to the store so `subscription.UserId` cannot be spoofed:
     ```csharp
     var sanitizedSubscription = subscription with { UserId = userId };
     await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
     ```
  2. In `InMemoryPushSubscriptionStore.cs`:
     Ensure that when `userId` is explicitly passed (even if `null` or when caller provides context), it does not allow untrusted DTO properties to override the caller's decision, or remove the fallback if `userId` is always managed by the controller.
  3. In `PushNotificationTests.cs`:
     Add an assertion or integration test using `InMemoryPushSubscriptionStore` verifying that `store.GetByUserIdAsync("victim-user-id")` returns empty after an unauthenticated subscription request containing `"userId": "victim-user-id"`.

#### [Major] Finding 2: Missing `ChangeTracker.Clear()` on Retry in `RightAssetPurchaseService` (DATA-01/DATA-02)
- **What**: If transient errors trigger a retry in `ExecuteWithStrategyAsync`, dirty entities in `dbContext.ChangeTracker` will cause EF Core tracking conflicts.
- **Where**: `SRNSMudApp/Services/RightAssetPurchaseService.cs:244-247`
- **Why**:
  In EF Core, `IExecutionStrategy` re-runs the delegate from the start. If the first attempt added entities and called `SaveChangesAsync` before failing, those entities remain in the `ChangeTracker`. Adding new entity instances on the next iteration causes `InvalidOperationException`.
- **Required Fix**:
  Reset the change tracker at the start of the retry delegate:
  ```csharp
  return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
  {
      dbContext.ChangeTracker.Clear();
      await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
      ...
  ```

#### [Minor] Finding 3: Unhandled `FormatException` on Malformed Bearer Token in `ExternalTokenVerificationService` (THREAD-01)
- **What**: If an attacker submits a LINE or GitHub token containing newline or non-ASCII characters, `new AuthenticationHeaderValue("Bearer", idToken)` throws `FormatException`, which is not caught in `VerifyLineTokenAsync` or `VerifyGithubTokenAsync`.
- **Where**: `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs:61, 94`
- **Why**:
  `VerifyGoogleTokenAsync` catches general `Exception`, but `VerifyLineTokenAsync` only catches `HttpRequestException` and `JsonException`. An unhandled `FormatException` results in an unhandled 500 error instead of returning `Failure`.
- **Required Fix**:
  Add `catch (FormatException ex)` returning `LogAndReturnFailure("Invalid token format", ex)`.

---

## 5. Adversarial Challenge & Stress Test Results

### Challenge Summary
- **Overall risk assessment**: HIGH
- **Exploitation feasibility**: High for Push Subscription Spoofing (Finding 1). Anyone with curl/Postman can register an unauthenticated subscription with `"userId": "<target_user_guid>"` and receive their notifications.

### Challenges & Stress Test Results

| Test Scenario | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|
| Unauthenticated POST `/api/pushnotification/subscribe` with body `{"endpoint": "...", "keys": {...}, "userId": "victim"}` | Subscription stored with `userId = null` | `InMemoryPushSubscriptionStore` stores subscription with `userId = "victim"` due to fallback | **FAIL (VULNERABILITY CONFIRMED)** |
| 20 concurrent requests to `VerifyLineTokenAsync` with distinct tokens | Headers isolated per request, no cross-contamination | Each request uses isolated `HttpRequestMessage`, `DefaultRequestHeaders` unchanged | **PASS** |
| Simulated failure during `JpycDepositTransaction` persistence in `RightAssetPurchaseService` | Complete rollback of `RightAsset` in DB | Both `RightAsset` and `JpycDepositTransaction` rolled back atomically | **PASS** |
| Replay attack with duplicate `TransactionHash` | Second transaction rejected | Rejected by pre-check and protected by unique index `IsUnique()` on `TransactionHash` | **PASS** |
| Unauthenticated POST to `/api/pushnotification/send` | HTTP 401/403 Forbidden | Blocked by `[Authorize(Roles = "Admin")]` | **PASS** |

---

## 6. Verified Claims

- `dotnet build`: Exited with code 0 (0 errors, 0 warnings on modified files).
- `dotnet format --diagnostics IDE0055 --verify-no-changes`: Exited with code 0.
- `dotnet test` (Milestone 1 suites): 47 passed, 0 failed, 0 skipped.
- THREAD-01 thread safety: Verified per-request message isolation.
- DATA-01 transaction atomicity: Verified atomic rollback on simulated interceptor failure.

---

## 7. Verification Method

Run the following commands from workspace root:
```bash
# 1. Verify build
dotnet build

# 2. Verify format
dotnet format --diagnostics IDE0055 --verify-no-changes

# 3. Verify affected test suites
dotnet test --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationIntegrationTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationServiceTests|FullyQualifiedName~ExternalLoginCallbackIntegrationTests"
```

To reproduce Finding 1:
Inspect `SRNSMudApp/Controllers/PushNotificationController.cs` line 57 and `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` line 25:
`effectiveUserId = userId ?? subscription.UserId;`
Observe that passing `(subscription, null)` where `subscription.UserId != null` stores `effectiveUserId == subscription.UserId`.
