# Handoff Report: Milestone 1 Iteration 2 Empirical Security & Concurrency Challenge

- **Agent**: `challenger_m1_it2_1`
- **Roles**: critic, specialist
- **Target Audience**: Orchestrator Parent (`077cc920-cc9e-40bc-99e6-163a40d89fa9`)
- **Date**: 2026-10-02T18:22:00Z
- **Verdict**: **APPROVE**

---

## 1. Observation

Direct empirical observations and execution results collected independently:

### 1.1 Source Code Verification

1. **SEC-01 (Sanitization & Dual-Layer Defense)**:
   - `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 50–63):
     ```csharp
     // SEC-01: 認証済みユーザーのIDを取得（ClaimTypes.NameIdentifier または "sub"）
     // 未認証ユーザーの場合はリクエストボディの UserId を任意に信用せず null とする（ユーザーIDのなりすまし登録を防止）
     string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
     string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
         ? authenticatedUserId
         : null;

     // SEC-01: クライアントがリクエストボディで指定した UserId を無条件に破棄し、
     // サーバー側で検証した userId（認証済みならクレーム値、未認証なら null）で DTO を無害化して保存する
     var sanitizedSubscription = subscription with { UserId = userId };

     await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
     return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
     ```
   - `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 83–86):
     ```csharp
     [HttpPost("send")]
     [Authorize(Roles = "Admin")]
     public async Task<IActionResult> SendNotification([FromBody] PushNotificationPayload? payload, CancellationToken cancellationToken)
     ```
   - `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 25–30):
     ```csharp
     // SEC-01: クライアント入力由来の subscription.UserId へのフォールバックを排除し、
     // 呼び出し元から明示的に渡された検証済み userId のみを採用する
     string? effectiveUserId = userId;
     var dtoWithUserId = subscription with { UserId = effectiveUserId };
     _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
     return Task.CompletedTask;
     ```

2. **THREAD-01 (Per-Request Header Isolation & Format Exception Handling)**:
   - `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 59–74):
     ```csharp
     // THREAD-01: 共有 HttpClient の DefaultRequestHeaders を変更せず、リクエスト単位の HttpRequestMessage で認証ヘッダーを設定する
     using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

     HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
     ...
     catch (FormatException ex)
     {
         return LogAndReturnFailure("Invalid LINE token format", ex);
     }
     ```
   - Lines 97–113: Identical per-request isolation and `FormatException` handler implemented for `VerifyGithubTokenAsync`.

### 1.2 Independent Tool Commands and Test Results

1. **Compilation Check**:
   - Command: `dotnet build`
   - Result: Exit code 0 (0 errors, 8 pre-existing logger warnings in unchanged files).

2. **Format Check**:
   - Command: `dotnet format --diagnostics IDE0055 --verify-no-changes`
   - Result: Exit code 0 (0 formatting violations).

3. **SEC-01 Adversarial & Unit Test Suites**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationAdversarialTests"`
     - Result: **Passed: 4, Failed: 0, Skipped: 0, Total: 4** (Duration: 2s).
     - Verified tests:
       - `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit`: PASSED (`victimSubs` is empty).
       - `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit`: PASSED (`victimSubs` is empty via real HTTP pipeline).
       - `Challenge_SendNotification_UnauthenticatedCaller_ReturnsUnauthorizedOrForbidden`: PASSED (401/403/Redirect response).
       - `PushNotificationController_SendNotification_RequiresAdminRoleSpecifically`: PASSED (`[Authorize(Roles = "Admin")]` strictly present).
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests"`
     - Result: **Passed: 12, Failed: 0, Skipped: 0, Total: 12** (Duration: 123ms).

4. **THREAD-01 Concurrency & Resilience Test Suites**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"`
     - Result: **Passed: 1, Failed: 0, Skipped: 0, Total: 1** (Duration: 56ms).
     - Verified: 100 concurrent requests with random jitter delays (1–15ms). Pre- and post-call assertions on `httpClient.DefaultRequestHeaders.Authorization == null` yielded 0 violations. Provider key token validation confirmed zero cross-contamination.
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"`
     - Result: **Passed: 6, Failed: 0, Skipped: 0, Total: 6** (Duration: 43ms).
     - Verified tests including:
       - `VerifyLineTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure`: PASSED (returns `Failure` instead of unhandled 500 error).
       - `VerifyGithubTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure`: PASSED.
       - `VerifyLineTokenAsync_ConcurrentRequests_DoNotMutateSharedHeadersOrCrossContaminate`: PASSED.

5. **Consolidated Milestone 1 Test Suite**:
   - Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"`
   - Result: **Passed: 45, Failed: 0, Skipped: 0, Total: 45** (Duration: 18s).

---

## 2. Logic Chain

1. **SEC-01 Attack Resistance**:
   - *Attack Scenario*: An unauthenticated adversary sends a POST request to `/api/pushnotification/subscribe` specifying an arbitrary victim ID in the request body (`UserId: "victim-target-user-id"`).
   - *Observation Support*: Lines 52–61 of `PushNotificationController.cs` enforce that for unauthenticated callers (`User.Identity.IsAuthenticated == false`), `userId` is set to `null` and a sanitized record with `UserId = null` is passed. Furthermore, `InMemoryPushSubscriptionStore.cs` lines 27–29 store `effectiveUserId = userId`, eliminating any fallback to the payload's `UserId`.
   - *Empirical Confirmation*: Both unit/integration (`DirectStoreExploit`) and full HTTP pipeline (`EndToEndHttpExploit`) tests verify that `realStore.GetByUserIdAsync("victim-target-user-id")` yields an empty list. Spoofing is rendered impossible.
   - *Broadcast Protection*: `PushNotificationController.SendNotification` is guarded by `[Authorize(Roles = "Admin")]`, verified through both endpoint HTTP invocation and reflection attributes.

2. **THREAD-01 Concurrency & Injection Robustness**:
   - *Concurrency Stress*: When multiple threads access `ExternalTokenVerificationService.VerifyLineTokenAsync` concurrently, mutating `httpClient.DefaultRequestHeaders` would leak authorization tokens between requests.
   - *Observation Support*: `ExternalTokenVerificationService.cs` allocates a fresh `HttpRequestMessage` per invocation and scopes `request.Headers.Authorization` exclusively to that instance. `_httpClient.DefaultRequestHeaders` remains completely unmutated (`null`).
   - *Empirical Confirmation*: 100 concurrent tasks executed against `JitterDelayHttpMessageHandler` with random delays completed with 0 header violations and 0 cross-token responses.
   - *Input Injection / Malformed Tokens*: When tokens containing illegal characters (such as CRLF headers) are supplied, `AuthenticationHeaderValue` throws `FormatException`. Both `VerifyLineTokenAsync` and `VerifyGithubTokenAsync` intercept `FormatException` and return `Failure("Invalid ... token format")`, preventing unhandled 500 crashes.

---

## 3. Caveats

- **Scope Boundary**: As specified in the user instruction update (`ORIGINAL_REQUEST.md`), code changes are restricted to Milestone 1. Milestones 2 and 3 remain deferred to the final architecture report.
- **Store Implementation**: Tests validated the in-memory store (`InMemoryPushSubscriptionStore`) and HTTP controller endpoints. Any future persistent database store for push subscriptions must replicate the same strict parameter precedence (`effectiveUserId = userId`).
- **Pre-existing UI Tests**: Four unrelated component tests in `SRNSMudApp.Tests/Components/` (`ProposeContractDialogTests`, `TagNodeWidgetTests`) remain in their pre-existing state per scope boundaries.

---

## 4. Conclusion

**Verdict: APPROVE**

The implementations for SEC-01 and THREAD-01 completely satisfy all security, concurrency, and robustness requirements:
- Unauthenticated user spoofing in push subscriptions is completely eliminated across controller and store layers.
- Admin role authorization on push broadcasting is confirmed.
- 100-request high concurrency stress testing on `ExternalTokenVerificationService` succeeds with 0 violations and zero token cross-contamination.
- Formatting compliance (`IDE0055`) and compilation succeed with zero errors.
- 100% pass rate (45/45) across all Milestone 1 test suites.

Milestone 1 is ready to proceed to Milestone 4 (Final Verification & Consolidated Report).

---

## 5. Verification Method

To independently reproduce the empirical verification:

```bash
# 1. Format check
dotnet format --diagnostics IDE0055 --verify-no-changes

# 2. Build check
dotnet build

# 3. SEC-01: 4/4 Adversarial Push Tests
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationAdversarialTests"

# 4. SEC-01: 12/12 Push Notification Unit Tests
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests"

# 5. THREAD-01: 100-Request High Concurrency Stress Test
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"

# 6. THREAD-01: Token Verification Unit Tests (including FormatException handling)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"

# 7. All Milestone 1 Tests (45 total)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"
```
