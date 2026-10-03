# Remediation Plan & Verification Test Matrix: Milestone 1 Iteration 2

**Author**: `explorer_m1_it2_3`  
**Milestone**: Milestone 1 (Security, Concurrency & Data Integrity) — Iteration 2  
**Target Audience**: Orchestrator Parent & `worker_m1_it2`  
**Date**: 2026-10-02  

---

## Executive Summary

Milestone 1 Iteration 1 was evaluated by three independent assessment agents:
- `auditor_m1_1`: **INTEGRITY VIOLATION** (Check 2 Facade FAIL, Check 4 Behavioral FAIL, Check 5 Output FAIL).
- `reviewer_m1_2`: **REQUEST_CHANGES** (High Risk: unauthenticated push subscription hijacking, dirty ChangeTracker state on retry).
- `challenger_m1_2`: **REJECT** (Critical Bugs: double-minting on transient retry, unique key crash on second save retry).

This investigation reconciles all findings, verifies the exact code defects and test failures across the codebase, and establishes an authoritative **Step-by-Step Remediation Plan** and **Verification Test Matrix** for `worker_m1_it2` to ensure a 100% passing rate across all baseline and adversarial test suites.

---

## 1. Observation

### 1.1 SEC-01: Incomplete DTO Sanitization & Store Fallback Bypass

1. **Vulnerability in `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 50–58)**:
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
   **Observation**: The incoming parameter `[FromBody] PushSubscriptionDto? subscription` contains the untrusted, client-supplied `subscription.UserId` (e.g., `"victim-user-id"`). The controller passes this raw `subscription` instance unmodified to `_subscriptionStore.AddOrUpdateAsync`, alongside `userId = null` for anonymous callers.

2. **Bypass in `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 25–27)**:
   ```csharp
   string? effectiveUserId = userId ?? subscription.UserId;
   var dtoWithUserId = subscription with { UserId = effectiveUserId };
   _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
   ```
   **Observation**: When an unauthenticated caller sends `{"endpoint": "...", "keys": {...}, "userId": "victim-user-id"}`, `userId` is `null`. The null-coalescing expression `userId ?? subscription.UserId` evaluates to `subscription.UserId` (`"victim-user-id"`). The subscription is persisted under the victim's account, allowing notification interception.

3. **Self-Certifying Mock in `SRNSMudApp.Tests/Push/PushNotificationTests.cs` (lines 267–272)**:
   ```csharp
   var result = await controller.Subscribe(dto, default) as OkObjectResult;

   // Assert: 未認証時は null が渡されること
   Assert.NotNull(result);
   mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);
   ```
   **Observation**: The mock test checked only that `(dto, null)` was passed to the interface method. Because `dto` still contained `UserId: "victim-user-id"`, the mock hid both the unsanitized DTO passing and the real store's fallback vulnerability.

4. **Verbatim Adversarial Test Failures**:
   ```
   Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit
   Error Message:
     Assert.Empty() Failure: Collection was not empty
     Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/exploit-endpoint, Keys = ..., UserId = victim-target-user-id }]

   Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit
   Error Message:
     Assert.Empty() Failure: Collection was not empty
     Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/http-exploit-endpoint, Keys = ..., UserId = e2e-victim-user-id }]
   ```

---

### 1.2 DATA-01 / DATA-02: ChangeTracker Pollution on ExecutionStrategy Retry

1. **Defect in `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 205, 244–297)**:
   ```csharp
   await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken);
   ...
   return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
   {
       await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

       var newAsset = new RightAsset { TargetTagId = tag.Id, OwnerId = userId, Amount = request.Amount, IsBurned = false };
       dbContext.RightAssets.Add(newAsset);
       await dbContext.SaveChangesAsync(cancellationToken);

       var depositTx = new JpycDepositTransaction { ..., RightAssetId = newAsset.Id, TransactionHash = normalizedTx, ... };
       dbContext.JpycDepositTransactions.Add(depositTx);

       var purchaseItem = new Item { OwnerId = userId, Content = itemContent };
       dbContext.Items.Add(purchaseItem);
       await dbContext.SaveChangesAsync(cancellationToken);

       await transaction.CommitAsync(cancellationToken);
       return Result.Ok(newAsset);
   });
   ```
   **Observation**: `dbContext` is instantiated at line 205 outside the `ExecuteWithStrategyAsync` lambda. In the retry delegate, `dbContext.ChangeTracker.Clear()` is **never invoked**.

2. **Entity Duplication on First-Save Transient Failure**:
   When `TransientTimeoutOnFirstSaveInterceptor` triggers a transient exception on the first `SaveChangesAsync` (line 258), the database transaction rolls back, but `newAsset` remains tracked in `dbContext.ChangeTracker` (`EntityState.Added`).
   On attempt 2, the delegate instantiates and adds another `newAsset`. The change tracker now contains two `RightAsset` instances. When `SaveChangesAsync` executes, SQL Server commits **both** assets.
   **Verbatim Test Output**:
   ```
   Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets
   Error Message:
     Assert.Single() Failure: The collection contained 2 items
     Collection: [RightAsset { Id = 3, Amount = 2, ... }, RightAsset { Id = 4, Amount = 2, ... }]
   ```

3. **Unique Index Collision on Second-Save Transient Failure**:
   When `TransientTimeoutOnSecondSaveInterceptor` triggers a transient exception on the second `SaveChangesAsync` (line 292), the database transaction rolls back, but `depositTx` (holding `normalizedTx`) remains in `ChangeTracker`.
   On attempt 2, another `depositTx` with identical `TransactionHash` is added. When `SaveChangesAsync` executes, EF Core attempts to insert duplicate keys into table `JpycDepositTransactions`, violating unique index `IX_JpycDepositTransactions_TransactionHash`.
   **Verbatim Test Output**:
   ```
   Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically
   Error Message:
     Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes.
     ---- Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'.
   ```

---

### 1.3 THREAD-01: Concurrency Verification & Format Robustness

1. **Verified Concurrency Safety**:
   In `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 58–63):
   ```csharp
   using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
   request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
   HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
   ```
   `THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak` executes 100 concurrent requests with random micro-delays (1–15ms). It **PASSED** with 0 header mutations and 0 cross-contaminations.
2. **Defect / Hardening Opportunity**:
   If a client provides an invalid Bearer token string containing illegal characters (newlines, non-ASCII, spaces), `new AuthenticationHeaderValue("Bearer", idToken)` throws `FormatException`. In `VerifyLineTokenAsync` and `VerifyGithubTokenAsync`, `FormatException` is unhandled, triggering an unhandled 500 error instead of a handled domain `Failure`.

---

## 2. Logic Chain

1. **SEC-01 Incomplete Fix Causality**:
   - `PushNotificationController.Subscribe` accepted `[FromBody] PushSubscriptionDto? subscription` containing attacker-supplied `UserId`.
   - The controller determined `userId = null` for anonymous callers, but did not sanitize `subscription`.
   - `InMemoryPushSubscriptionStore` used `effectiveUserId = userId ?? subscription.UserId`.
   - Result: `null ?? "victim-user-id" == "victim-user-id"`. The attacker's endpoint is stored under the victim's account.
   - The unit test passed only because Moq verified method argument matching (`dto, null`) rather than verifying DTO sanitization or end-to-end store behavior.
   - **Remediation**: Controller must create `var sanitizedSubscription = subscription with { UserId = userId };` before forwarding to the store, and store must enforce that caller-provided `userId` takes precedence over DTO properties.

2. **DATA-01 / DATA-02 Retry Hazard Causality**:
   - `SqlServerRetryingExecutionStrategy` re-runs the provided lambda delegate when a transient SQL exception occurs.
   - EF Core's `ChangeTracker` tracks entity instances in memory and does **not** roll back state upon SQL transaction abort.
   - Entities added in failed attempts accumulate in memory across retry attempts.
   - Subsequent `SaveChangesAsync` calls generate SQL for accumulated entities, creating duplicate financial assets (double-minting) or crashing on unique indexes.
   - **Remediation**: The delegate must invoke `dbContext.ChangeTracker.Clear();` at the beginning of each attempt, detaching all dirty state from failed previous attempts.

3. **THREAD-01 Malformed Token Handling**:
   - `AuthenticationHeaderValue` strictly validates header parameter syntax per RFC 7235. Malformed inputs throw `FormatException`.
   - Adding `catch (FormatException ex) => LogAndReturnFailure("Invalid token format", ex)` makes the service robust against malformed or malicious inputs.

---

## 3. Caveats

1. **Database Test Environment**: The retry integration tests (`PurchaseRightAssetWithJpycAsync_WhenTransientFailure*`) require the active SQL Server test container (`SRNSMudApp.Tests/TestContainers` / `SharedDatabaseFixture`). If the SQL Server container is unreachable or starting up, tests will report connectivity timeouts.
2. **Pre-Existing Failures Outside M1 Scope**: Four pre-existing UI component tests (`ProposeContractDialogTests` and `TagNodeWidgetTests`) are known historical failures unrelated to Milestone 1 and must be excluded from regression gating.
3. **API Contract Preservation**: The public method signatures of `IPushSubscriptionStore.AddOrUpdateAsync`, `PushNotificationController.Subscribe`, and `IExternalTokenVerificationService.VerifyTokenAsync` must remain strictly backwards-compatible.

---

## 4. Conclusion & Step-by-Step Guidance for Worker M1 Iteration 2

### 4.1 Implementation Roadmap

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Milestone 1 Iteration 2                         │
├────────────────────────┬───────────────────────┬───────────────────────┤
│ 1. SEC-01 Fix          │ 2. DATA-01/02 Fix     │ 3. THREAD-01 Harden   │
│ - PushNotificationCtrl │ - RightAssetPurchase  │ - ExternalTokenService│
│ - InMemoryPushStore    │ - ExecutionStrategy   │ - Add FormatEx tests  │
│ - PushNotificationTest │   Docs                │                       │
└────────────────────────┴───────────────────────┴───────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        Full Verification Suite                         │
│ 1. PushNotificationAdversarialTests (4/4 PASS)                         │
│ 2. RightAssetPurchaseService retry tests (2/2 PASS)                    │
│ 3. ExternalTokenVerification concurrency & unit tests (6/6 PASS)       │
│ 4. Build & Format validation (0 errors, 0 warnings)                    │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 4.2 Exact Code Remediation Tasks

#### Task 1: Fix SEC-01 in `SRNSMudApp/Controllers/PushNotificationController.cs`
**Target**: Lines 50–58  
**Action**: Sanitize the incoming `subscription` DTO with the resolved `userId` before passing to `_subscriptionStore.AddOrUpdateAsync`.

```csharp
// Before:
string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
    ? authenticatedUserId
    : null;

await _subscriptionStore.AddOrUpdateAsync(subscription, userId, cancellationToken);
return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });

// After:
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

---

#### Task 2: Update Unit & Behavioral Tests in `SRNSMudApp.Tests/Push/PushNotificationTests.cs`
**Target**: Lines 201–273  
**Action**:
1. Update mock expectations in `PushNotificationTests.cs` so they assert the **sanitized** DTO with expected `UserId`.
2. Add a non-mocked behavioral test using real `InMemoryPushSubscriptionStore` to prevent mock regressions.

```csharp
// Update Line 212:
var expectedDto = dto with { UserId = "auth-user-123" };
mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);

// Update Line 242:
var expectedDto = dto with { UserId = "sub-user-456" };
mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, "sub-user-456", It.IsAny<CancellationToken>()), Times.Once);

// Update Line 271:
var expectedDto = dto with { UserId = null };
mockStore.Verify(s => s.AddOrUpdateAsync(expectedDto, null, It.IsAny<CancellationToken>()), Times.Once);

// Add behavioral assertion test with real store:
[Fact]
public async Task PushNotificationController_Subscribe_WhenUnauthenticatedWithSpoofedUserId_RealStoreDoesNotIndexVictim()
{
    var realStore = new InMemoryPushSubscriptionStore();
    var mockPush = new Mock<IWebPushNotificationService>();
    var options = Options.Create(new VapidOptions());
    var controller = new PushNotificationController(realStore, mockPush.Object, options)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        }
    };

    var attackerDto = new PushSubscriptionDto(
        "https://example.com/push/exploit-test",
        new PushSubscriptionKeysDto("p256", "auth"),
        UserId: "victim-account-id"
    );

    var result = await controller.Subscribe(attackerDto, default) as OkObjectResult;
    Assert.NotNull(result);

    var victimSubs = await realStore.GetByUserIdAsync("victim-account-id");
    Assert.Empty(victimSubs);

    var allSubs = await realStore.GetAllAsync();
    var stored = Assert.Single(allSubs);
    Assert.Null(stored.UserId);
}
```

---

#### Task 3: Fix DATA-01 / DATA-02 in `SRNSMudApp/Services/RightAssetPurchaseService.cs`
**Target**: Lines 244–248  
**Action**: Add `dbContext.ChangeTracker.Clear();` at the very start of the `ExecuteWithStrategyAsync` retry delegate.

```csharp
// Before:
return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
{
    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    ...

// After:
return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
{
    // DATA-01 / DATA-02: 一時的障害によるリトライ発生時、前回試行で失敗・ロールバックされたエンティティが
    // ChangeTracker に残存していると、重複登録（double-minting）や一意キー制約違反が発生する。
    // 各試行の開始時に ChangeTracker をクリアして常にクリーンな状態でトランザクションを再実行する。
    dbContext.ChangeTracker.Clear();

    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    ...
```

---

#### Task 4: Update Documentation in `SRNSMudApp/Data/ExecutionStrategyExtensions.cs`
**Target**: Class summary (lines 14–20) and method doc comments (lines 26–28)  
**Action**: Explicitly document the `ChangeTracker.Clear()` requirement for multi-save operations executed under `ExecuteWithStrategyAsync`.

```csharp
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
```

---

#### Task 5: Harden `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`
**Target**: Lines 55–80 (`VerifyLineTokenAsync`) and Lines 89–115 (`VerifyGithubTokenAsync`)  
**Action**: Catch `FormatException` on Bearer token parsing and return domain `Failure`.

```csharp
// In VerifyLineTokenAsync:
catch (FormatException ex)
{
    return LogAndReturnFailure("Invalid LINE token format", ex);
}
catch (HttpRequestException ex)
{
    return LogAndReturnFailure("HTTP Error verifying LINE token", ex);
}
catch (JsonException ex)
{
    return LogAndReturnFailure("JSON Error verifying LINE token", ex);
}

// In VerifyGithubTokenAsync:
catch (FormatException ex)
{
    return LogAndReturnFailure("Invalid GitHub token format", ex);
}
catch (HttpRequestException ex)
{
    return LogAndReturnFailure("HTTP Error verifying GitHub token", ex);
}
catch (JsonException ex)
{
    return LogAndReturnFailure("JSON Error verifying GitHub token", ex);
}
```

---

#### Task 6: Add FormatException Unit Tests in `SRNSMudApp.Tests/Auth/ExternalTokenVerificationServiceTests.cs`
**Target**: End of file  
**Action**: Add tests verifying that malformed tokens with newline/control characters return handled `Failure`.

```csharp
[Fact]
public async Task VerifyLineTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure()
{
    var httpClient = new HttpClient();
    var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

    // Act: 改行文字などヘッダーとして不正な文字列を含むトークン
    var result = await service.VerifyTokenAsync("LINE", "invalid\r\ntoken");

    // Assert: 例外でクラッシュせず Failure が返ること
    var failure = Assert.IsType<Failure>(result);
    Assert.Contains("Invalid LINE token format", failure.ErrorMessage);
}

[Fact]
public async Task VerifyGithubTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure()
{
    var httpClient = new HttpClient();
    var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

    // Act
    var result = await service.VerifyTokenAsync("GITHUB", "invalid\ntoken");

    // Assert
    var failure = Assert.IsType<Failure>(result);
    Assert.Contains("Invalid GitHub token format", failure.ErrorMessage);
}
```

---

## 5. Comprehensive Verification Test Matrix

| # | Test Suite | Test Identifier | Category | Target Verification | Expected Result | Verification Command |
|---|---|---|---|---|---|---|
| **V1** | `PushNotificationAdversarialTests` | `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit` | SEC-01 Security | Direct store check for spoofed victim ID | **PASS** (Victim subs empty, stored UserId is null) | `dotnet test --filter "FullyQualifiedName~Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit"` |
| **V2** | `PushNotificationAdversarialTests` | `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit` | SEC-01 Security | End-to-end HTTP anonymous subscription exploit | **PASS** (HTTP 200 returned, store query for victim ID returns empty) | `dotnet test --filter "FullyQualifiedName~Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit"` |
| **V3** | `PushNotificationAdversarialTests` | `Challenge_SendNotification_UnauthenticatedCaller_ReturnsUnauthorizedOrForbidden` | SEC-01 Security | Anonymous broadcast push authorization guard | **PASS** (HTTP 401/403/302 returned) | `dotnet test --filter "FullyQualifiedName~Challenge_SendNotification_UnauthenticatedCaller"` |
| **V4** | `PushNotificationAdversarialTests` | `PushNotificationController_SendNotification_RequiresAdminRoleSpecifically` | SEC-01 Security | Role reflection check for `[Authorize(Roles = "Admin")]` | **PASS** (Role is exactly `"Admin"`) | `dotnet test --filter "FullyQualifiedName~SendNotification_RequiresAdminRoleSpecifically"` |
| **V5** | `PushNotificationTests` | `PushNotificationController_Subscribe_WhenAuthenticated_UsesClaimUserId` | SEC-01 Functional | Sanitized DTO forwarded with NameIdentifier claim | **PASS** (Mock verifies sanitized DTO with `"auth-user-123"`) | `dotnet test --filter "FullyQualifiedName~Subscribe_WhenAuthenticated_UsesClaimUserId"` |
| **V6** | `PushNotificationTests` | `PushNotificationController_Subscribe_WhenAuthenticatedWithSub_UsesSubClaimUserId` | SEC-01 Functional | Sanitized DTO forwarded with sub claim | **PASS** (Mock verifies sanitized DTO with `"sub-user-456"`) | `dotnet test --filter "FullyQualifiedName~Subscribe_WhenAuthenticatedWithSub_UsesSubClaimUserId"` |
| **V7** | `PushNotificationTests` | `PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull` | SEC-01 Functional | Sanitized DTO forwarded with null userId | **PASS** (Mock verifies sanitized DTO with `null`) | `dotnet test --filter "FullyQualifiedName~Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull"` |
| **V8** | `RightAssetPurchaseServiceTests` | `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets` | DATA-01 / DATA-02 Integrity | Transient timeout on first save retry idempotency | **PASS** (Exactly 1 `RightAsset` created, zero duplicates) | `dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave"` |
| **V9** | `RightAssetPurchaseServiceTests` | `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically` | DATA-01 / DATA-02 Integrity | Transient timeout on second save retry idempotency | **PASS** (0 unique key collisions on `TransactionHash`, exactly 1 asset & 1 tx saved) | `dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave"` |
| **V10** | `RightAssetPurchaseServiceTests` | `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically` | DATA-01 Atomicity | Rollback verification on unhandled save error | **PASS** (0 `RightAsset` and 0 `JpycDepositTransaction` survive in database) | `dotnet test --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"` |
| **V11** | `ExternalTokenVerificationConcurrencyTests` | `THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak` | THREAD-01 Concurrency | 100 concurrent requests with random micro-delay jitter | **PASS** (0 header violations, `DefaultRequestHeaders.Authorization` remains null) | `dotnet test --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"` |
| **V12** | `ExternalTokenVerificationServiceTests` | `VerifyLineTokenAsync_UsesPerRequestAuthorizationHeaderAndDoesNotMutateDefaultHeaders` | THREAD-01 Functional | Header isolation on single request | **PASS** (Request header has Bearer, DefaultRequestHeaders is null) | `dotnet test --filter "FullyQualifiedName~VerifyLineTokenAsync_UsesPerRequestAuthorizationHeader"` |
| **V13** | `ExternalTokenVerificationServiceTests` | `VerifyLineTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure` | THREAD-01 Robustness | Malformed bearer token input handling | **PASS** (Returns Failure with format error message, no unhandled exception) | `dotnet test --filter "FullyQualifiedName~VerifyLineTokenAsync_WhenTokenContainsInvalidFormat"` |
| **V14** | Code Hygiene | `dotnet format --diagnostics IDE0055 --verify-no-changes` | Hygiene | Whitespace & style formatting rules | **PASS** (Exit code 0, 0 formatting violations) | `dotnet format --diagnostics IDE0055 --verify-no-changes` |
| **V15** | Compiler Integrity | `dotnet build` | Build | Compilation check | **PASS** (0 errors, 0 warnings on modified files) | `dotnet build` |

---

## 6. Verification Method

To execute the entire verification suite in order:

```bash
# Step 1: Format and compilation check
dotnet format --diagnostics IDE0055 --verify-no-changes
dotnet build

# Step 2: Execute SEC-01 adversarial and unit suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationAdversarialTests"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests"

# Step 3: Execute DATA-01 / DATA-02 retry and rollback suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"

# Step 4: Execute THREAD-01 concurrency and token verification suites
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"

# Step 5: Consolidated Milestone 1 run (all M1 suites)
dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationAdversarialTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests|FullyQualifiedName~ExternalTokenVerificationServiceTests"
```

### Invalidation Conditions
This remediation plan is invalidated if:
1. `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit` or `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit` fails due to `victimSubs` not being empty.
2. `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets` produces more than 1 `RightAsset` record in SQL Server.
3. `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically` triggers `DbUpdateException` / `SqlException 2601` on unique index `IX_JpycDepositTransactions_TransactionHash`.
4. Any new compiler warnings or IDE0055 format violations are introduced.
