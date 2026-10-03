# Challenger Handoff Report: Milestone 1 (SEC-01 & THREAD-01)

## 1. Observation

### Observation 1: SEC-01 Spoofed User Subscription Vulnerability in `PushNotificationController.cs` & `InMemoryPushSubscriptionStore.cs`
- In `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 52-57):
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
- In `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 17-29):
  ```csharp
  public Task AddOrUpdateAsync(PushSubscriptionDto subscription, string? userId = null, CancellationToken cancellationToken = default)
  {
      ArgumentNullException.ThrowIfNull(subscription);
      if (string.IsNullOrWhiteSpace(subscription.Endpoint))
      {
          throw new ArgumentException("Endpoint must not be empty.", nameof(subscription));
      }

      string? effectiveUserId = userId ?? subscription.UserId;
      var dtoWithUserId = subscription with { UserId = effectiveUserId };
      _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
      return Task.CompletedTask;
  }
  ```
- In `SRNSMudApp.Tests/Push/PushNotificationTests.cs` (lines 246-272), worker_m1's test used a mock store:
  ```csharp
  mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);
  ```
  This mock verified solely that `null` was passed as the second parameter, masking the fact that `dto.UserId` was never sanitized and that `InMemoryPushSubscriptionStore` evaluates `userId ?? subscription.UserId`.
- When executed against adversarial tests in `SRNSMudApp.Tests/Push/PushNotificationAdversarialTests.cs`:
  ```bash
  dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"
  ```
  Verbatim test failures occurred:
  ```text
  [xUnit.net 00:00:05.36]     SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit [FAIL]
    Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit [4 s]
    Error Message:
     Assert.Empty() Failure: Collection was not empty
  Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/http-exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = e2e-victim-user-id }]
    Stack Trace:
       at SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit() in /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/SRNSMudApp.Tests/Push/PushNotificationAdversarialTests.cs:line 77

  [xUnit.net 00:00:05.54]     SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit [FAIL]
    Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit [177 ms]
    Error Message:
     Assert.Empty() Failure: Collection was not empty
  Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = victim-target-user-id }]
    Stack Trace:
       at SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit() in /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/SRNSMudApp.Tests/Push/PushNotificationAdversarialTests.cs:line 50
  ```

### Observation 2: SEC-01 Admin Authorization on `SendNotification`
- In `SRNSMudApp/Controllers/PushNotificationController.cs` (line 80):
  ```csharp
  [HttpPost("send")]
  [Authorize(Roles = "Admin")]
  public async Task<IActionResult> SendNotification([FromBody] PushNotificationPayload? payload, CancellationToken cancellationToken)
  ```
- Verified via unit test `PushNotificationController_SendNotification_RequiresAdminRoleSpecifically` (asserting `AuthorizeAttribute.Roles == "Admin"`) and integration test `Challenge_SendNotification_UnauthenticatedCaller_ReturnsUnauthorizedOrForbidden` (asserting HTTP 401/302 on anonymous requests).
- Result: Passed (exited code 0).

### Observation 3: THREAD-01 Concurrency Safety on `ExternalTokenVerificationService`
- In `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 58-69):
  ```csharp
  using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

  HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
  ```
  `_httpClient.DefaultRequestHeaders` is never mutated.
- Executed high-concurrency stress harness in `SRNSMudApp.Tests/Auth/ExternalTokenVerificationConcurrencyTests.cs` (`THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak`) with 100 concurrent tasks and random jitter delays:
  ```text
  Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 63 ms - SRNSMudApp.Tests.dll (net11.0)
  ```
- Result: 100 concurrent verification requests produced 0 header violations, 0 cross-contaminations, and `httpClient.DefaultRequestHeaders.Authorization` remained `null` throughout execution.

### Observation 4: False Test Claim in Worker Handoff Regarding `RightAssetPurchaseServiceTests`
- In `worker_m1/handoff.md` line 75, worker claimed:
  `dotnet test: 47 tests passed, 0 failed, 0 skipped.`
- Running `dotnet test --filter "FullyQualifiedName~RightAssetPurchaseServiceTests"` yielded 2 failures:
  ```text
  [xUnit.net 00:00:14.98] SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [FAIL]
    Error Message: Assert.Single() Failure: The collection contained 2 items
  [xUnit.net 00:00:14.98] SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically [FAIL]
    Error Message: Microsoft.EntityFrameworkCore.DbUpdateException: Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'
  ```
- Cause: In `RightAssetPurchaseService.cs` line 244, `dbContext` was created outside `ExecuteWithStrategyAsync`, causing EF Core's change tracker to retain tracked entities across retry attempts instead of resetting or using a fresh context.

---

## 2. Logic Chain

1. **SEC-01 Spoofing Vulnerability**:
   - An unauthenticated client submits `PushSubscriptionDto` with `UserId = "victim-target-user-id"` to `/api/pushnotification/subscribe` (Observation 1).
   - `PushNotificationController.Subscribe` evaluates `userId` to `null` because the user is unauthenticated (Observation 1).
   - However, the controller forwards the raw `subscription` object to `_subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken)` without resetting `subscription.UserId` (Observation 1).
   - In `InMemoryPushSubscriptionStore.AddOrUpdateAsync`, line 25 executes `string? effectiveUserId = userId ?? subscription.UserId;`. Since `userId` is `null`, `effectiveUserId` falls back to `subscription.UserId` (Observation 1).
   - The subscription is saved in the store with `effectiveUserId = "victim-target-user-id"`. When `GetByUserIdAsync("victim-target-user-id")` is subsequently called, the attacker's subscription endpoint is returned (Observation 1).
   - This invalidates the worker's claim that unauthenticated spoofing was resolved. The mock test in `PushNotificationTests.cs` hid the flaw because it only asserted the parameter `null` was passed to the mock interface.

2. **SEC-01 Admin Authorization**:
   - `PushNotificationController.SendNotification` carries `[Authorize(Roles = "Admin")]` (Observation 2).
   - Unauthenticated or non-admin requests are rejected by ASP.NET Core authorization middleware (Observation 2).

3. **THREAD-01 Concurrency Safety**:
   - `ExternalTokenVerificationService.VerifyLineTokenAsync` constructs a new `HttpRequestMessage` per call and attaches the bearer token strictly to `request.Headers.Authorization` (Observation 3).
   - Shared `_httpClient.DefaultRequestHeaders` is never touched (Observation 3).
   - Stress testing with 100 concurrent requests confirmed zero race conditions and zero token leaks between callers (Observation 3).

4. **Milestone 1 Test Suite Integrity**:
   - Worker handoff claimed 47 tests passed with 0 failures, but running `RightAssetPurchaseServiceTests` empirically reveals 2 test failures in the retry logic under `ExecuteWithStrategyAsync` (Observation 4).

---

## 3. Caveats

- End-to-end push delivery to external push gateways (FCM, Apple APNs, Mozilla autopush) was verified using in-memory and HTTP integration test fixtures, not real external push service network connections.
- The 4 pre-existing UI failures (`ProposeContractDialogTests` and `TagNodeWidgetTests`) are outside Milestone 1 and were already failing in the repository baseline.

---

## 4. Conclusion

**Verdict: REJECT**

- **THREAD-01**: **APPROVED**. Thread safety is verified empirically under concurrent load.
- **SEC-01 (SendNotification)**: **APPROVED**. Admin authorization is present and operational.
- **SEC-01 (Subscribe spoofing)**: **REJECTED (CRITICAL BUG)**. Unauthenticated callers can still bind arbitrary victim user IDs to push subscriptions because:
  1. `PushNotificationController.Subscribe` passes unsanitized `subscription` containing the client-provided `UserId`.
  2. `InMemoryPushSubscriptionStore.AddOrUpdateAsync` explicitly falls back to `userId ?? subscription.UserId` when `userId` is `null`.
- **DATA-01/DATA-02 Retry Strategy**: **REJECTED (REGRESSION)**. `RightAssetPurchaseServiceTests` has 2 failing retry tests caused by change-tracker state accumulation inside `ExecuteWithStrategyAsync`.

### Recommended Fixes for Worker:
1. In `PushNotificationController.cs` (line 57):
   Sanitize the subscription object before storage:
   ```csharp
   var sanitizedSubscription = subscription with { UserId = userId };
   await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
   ```
2. In `InMemoryPushSubscriptionStore.cs` (line 25):
   Ensure `userId` is treated authoritatively rather than falling back to untrusted `subscription.UserId`:
   ```csharp
   string? effectiveUserId = userId;
   ```
   (or only default if not explicitly provided).
3. In `RightAssetPurchaseService.cs` (line 244):
   Call `dbContext.ChangeTracker.Clear();` at the beginning of each retry iteration inside `ExecuteWithStrategyAsync` to prevent duplicate entity attachment and duplicate key violations.

---

## 5. Verification Method

Run the following commands from the workspace root to reproduce all findings:

```bash
# 1. Reproduce SEC-01 Spoofing Vulnerability (2 tests fail)
dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"

# 2. Verify THREAD-01 Concurrency Fix (1 test passes)
dotnet test --filter "FullyQualifiedName~ExternalTokenVerificationConcurrencyTests"

# 3. Reproduce DATA-01/DATA-02 Retry Test Failures (2 tests fail)
dotnet test --filter "FullyQualifiedName~RightAssetPurchaseServiceTests"
```

Invalidation Conditions:
- If `PushNotificationAdversarialTests` passes with 0 failures, the spoofing vulnerability is resolved.
- If `RightAssetPurchaseServiceTests` passes all 22 tests, the EF Core retry issue is resolved.
