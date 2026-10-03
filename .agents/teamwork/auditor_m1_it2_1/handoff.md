# Forensic Audit Handoff Report: Milestone 1 Iteration 2

## Forensic Audit Report

- **Work Product**: Milestone 1 Iteration 2 Implementation (SEC-01, DATA-01/02, THREAD-01)
- **Profile**: General Project
- **Integrity Mode**: Development (from `ORIGINAL_REQUEST.md`)
- **Verdict**: **CLEAN**

### Phase Results
- **Check 1: Hardcoded Output Detection**: PASS — No hardcoded test outputs, artificial bypasses, or fixed responses detected in source code.
- **Check 2: Facade Detection**: PASS — All previously identified facade implementations (SEC-01 unsanitized DTO passing and store fallback; DATA-01/02 dirty ChangeTracker state on retry) have been genuinely resolved with authentic logic.
- **Check 3: Pre-populated Artifact Detection**: PASS — Workspace contains zero pre-populated test artifacts or fabricated verification logs from this iteration.
- **Check 4: Behavioral Verification (Build & Run)**: PASS — Clean build with 0 errors and zero formatting violations. 45/45 Milestone 1 unit, integration, retry, concurrency, and adversarial security tests executed and passed cleanly.
- **Check 5: Output Verification**: PASS — Adversarial exploits (unauthenticated spoofing, transient retry double-minting, header cross-contamination) all reliably fail to exploit the system. Authenticated claims and transactions are strictly preserved.
- **Check 6: Dependency Audit**: PASS — Standard library and existing EF Core / ASP.NET Core abstractions used appropriately without inappropriate external work delegation.

---

## 1. Observation

### 1.1 SEC-01: Spoofing Elimination in Controller and Store
1. **Controller Layer Sanitization** (`SRNSMudApp/Controllers/PushNotificationController.cs`, lines 50–60):
   ```csharp
   string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
   string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
       ? authenticatedUserId
       : null;

   // SEC-01: クライアントがリクエストボディで指定した UserId を無条件に破棄し、
   // サーバー側で検証した userId（認証済みならクレーム値、未認証なら null）で DTO を無害化して保存する
   var sanitizedSubscription = subscription with { UserId = userId };

   await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
   ```
   The controller completely strips client-supplied `subscription.UserId` and passes a sanitized record with verified `userId`.

2. **Store Layer Fallback Elimination** (`SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`, lines 25–29):
   ```csharp
   // SEC-01: クライアント入力由来の subscription.UserId へのフォールバックを排除し、
   // 呼び出し元から明示的に渡された検証済み userId のみを採用する
   string? effectiveUserId = userId;
   var dtoWithUserId = subscription with { UserId = effectiveUserId };
   _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
   ```
   The fallback `userId ?? subscription.UserId` has been eliminated. The store strictly indexes by the explicit, server-verified `userId`.

3. **Adversarial & Unit Verification**:
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationAdversarialTests"`
     Output: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 2 s`
     Both `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit` (using real store) and `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit` (using real HTTP server) confirmed `victimSubs` is empty.
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests"`
     Output: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 100 ms`
     Includes `PushNotificationController_Subscribe_WhenUnauthenticatedWithSpoofedUserId_RealStoreDoesNotIndexVictim` with direct real-store assertion.

### 1.2 DATA-01 / DATA-02: Retry Idempotency & ChangeTracker Reset
1. **ChangeTracker State Clearing** (`SRNSMudApp/Services/RightAssetPurchaseService.cs`, lines 244–248):
   ```csharp
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       // DATA-01 / DATA-02: 一時的障害によるリトライ発生時、前回試行で失敗・ロールバックされたエンティティが
       // ChangeTracker に残存していると、重複登録（double-minting）や一意キー制約違反が発生する。
       // 各試行の開始時に ChangeTracker をクリアして常にクリーンな状態でトランザクションを再実行する。
       dbContext.ChangeTracker.Clear();

       await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
       ...
   ```
   Invoking `dbContext.ChangeTracker.Clear();` at the beginning of each retry iteration detaches all entities tracked during failed attempts.

2. **Empirical Retry Test Execution on Real SQL Server Fixture**:
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"`
     Output: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 21 s`
     - First save failure: Simulated `TimeoutException` during first `SaveChangesAsync` triggered retry; verified exactly 1 `RightAsset` and 1 `JpycDepositTransaction` committed (zero duplicate assets / no double-minting).
     - Second save failure: Simulated `TimeoutException` during second `SaveChangesAsync` triggered retry; verified zero unique constraint collision on `IX_JpycDepositTransactions_TransactionHash` and exactly 1 `RightAsset` and 1 `JpycDepositTransaction` committed.
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"`
     Output: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 12 s`

3. **Documentation Accuracy** (`SRNSMudApp/Data/ExecutionStrategyExtensions.cs`):
   XML doc accurately reflects retry characteristics and warns callers about explicit `ChangeTracker.Clear()` and `BeginTransactionAsync` / `CommitAsync` requirements.

### 1.3 THREAD-01: Concurrency Safety & Malformed Token Handling
1. **Header Isolation and Exception Guard** (`SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`):
   - Per-request `HttpRequestMessage` created with `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);`.
   - `catch (FormatException ex)` catches malformed tokens (newlines, control characters) and returns handled domain failure `Result.Fail`.
2. **Stress & Concurrency Execution**:
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"`
     Output: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 33 ms` (100 concurrent tasks with random jitter, zero header mutations).
   - `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"`
     Output: `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 33 ms` (includes malformed token formatting tests).

### 1.4 Consolidated Milestone 1 Test Suite
- Command: `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"`
- Output: `Passed! - Failed: 0, Passed: 45, Skipped: 0, Total: 45, Duration: 15 s`

---

## 2. Logic Chain

1. **Prior Defect 1 (SEC-01 Facade)**:
   - In Iteration 1, the worker only modified the controller to pass `userId: null`, but left `subscription.UserId` populated. The store retained `userId ?? subscription.UserId`, which fell back to the client-provided spoofed `UserId`.
   - In Iteration 2, both the controller (`var sanitizedSubscription = subscription with { UserId = userId }`) and the store (`effectiveUserId = userId` without fallback) were hardened.
   - Independent verification with adversarial tests (`Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit` and `EndToEndHttpExploit`) proves empirical remediation without facades.

2. **Prior Defect 2 (DATA-01/02 Retry Hazard)**:
   - In Iteration 1, `ExecuteWithStrategyAsync` retained dirty entities across retry attempts in the shared `dbContext`, causing duplicate `RightAsset` records on first save failure and unique key collisions on second save failure.
   - In Iteration 2, `dbContext.ChangeTracker.Clear();` was added at the start of the retry delegate.
   - Independent execution of transient timeout interceptor tests against the live SQL Server instance confirmed that both scenarios retry safely and commit exactly 1 `RightAsset` and 1 `JpycDepositTransaction`.

3. **Zero Cheats / Workarounds**:
   - Source inspection confirms that production code contains no conditional branches checking for test execution environments, test tokens, or hardcoded return values.
   - Tests do not use self-certifying mocks to mask defects; the critical security and retry tests execute against real store implementations, live ASP.NET Core test servers, and SQL Server databases.

---

## 3. Caveats

1. **Pre-Existing Unrelated UI Tests**:
   - 4 tests in `SRNSMudApp.Tests/Components/` (`ProposeContractDialogTests` and `TagNodeWidgetTests`) fail in the full suite test run. Git inspection confirms these files are untouched and unrelated to Milestone 1 backend services.
2. **Scope Constraint**:
   - Per user instructions (`ORIGINAL_REQUEST.md`), implementation code changes are strictly frozen after Milestone 1. Future milestones remain as recommendations for the final consolidated report.

---

## 4. Conclusion

The work product for Milestone 1 Iteration 2 satisfies all integrity and behavioral criteria. The previous facade implementation and transient retry hazards have been genuinely resolved.

**Verdict: CLEAN**

---

## 5. Verification Method

To independently reproduce this forensic audit:

```bash
# 1. Format check
dotnet format --diagnostics IDE0055 --verify-no-changes

# 2. Build check
dotnet build

# 3. SEC-01 Adversarial & Unit Suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationAdversarialTests"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests"

# 4. DATA-01 / DATA-02 Retry & Atomicity Suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"

# 5. THREAD-01 Concurrency & FormatException Suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"

# 6. Consolidated M1 Suite (45 tests)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"
```
