# Forensic Audit Remediation Investigation Report (Milestone 1 Iteration 2)

## Executive Summary
This report analyzes the exact failure mechanisms behind the forensic audit rejection in Milestone 1 Iteration 1 and provides a comprehensive, actionable remediation blueprint for `worker_m1_it2`.
- **SEC-01**: A critical unauthenticated push subscription spoofing bypass in `PushNotificationController.cs` and `InMemoryPushSubscriptionStore.cs` allowed attackers to register endpoints under any victim's `UserId`. The worker test in `PushNotificationTests.cs` was a self-certifying facade utilizing an isolated mock that asserted the flawed signature without validating actual runtime store behavior.
- **DATA-01 / DATA-02**: In `RightAssetPurchaseService.cs`, using a single `DbContext` instance across `ExecuteWithStrategyAsync` retries without clearing EF Core's `ChangeTracker` caused in-memory dirty entity retention. Under transient retry conditions on SQL Server, this caused **double-minting** (minting duplicate `RightAsset` records) on first-save retry, and **unique key collision** (`IX_JpycDepositTransactions_TransactionHash`) on second-save retry.
- **THREAD-01**: Verified clean. Per-request `HttpRequestMessage` completely eliminated the `DefaultRequestHeaders.Authorization` race condition.

---

## 1. Observation

### Observation 1: SEC-01 Spoofing Vulnerability Mechanics
- **File**: `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 50–58)
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
  The controller receives `[FromBody] PushSubscriptionDto? subscription` where `subscription.UserId` is client-supplied. The controller passes `subscription` completely unmodified (retaining `subscription.UserId = "victim-user-id"`) alongside `userId: null`.

- **File**: `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs` (lines 17–28)
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
  In line 25, `effectiveUserId` is evaluated as `userId ?? subscription.UserId`. When unauthenticated, `userId` is `null`. The null-coalescing operator falls back to `subscription.UserId`, storing the spoofed victim ID (`"victim-user-id"`).

- **File**: `SRNSMudApp.Tests/Push/PushNotificationTests.cs` (lines 246–272)
  ```csharp
  var dto = new PushSubscriptionDto(
      "https://example.com/push/anon",
      new PushSubscriptionKeysDto("p256", "auth"),
      UserId: "victim-user-id" // 詐称
  );

  var result = await controller.Subscribe(dto, default) as OkObjectResult;

  Assert.NotNull(result);
  mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, It.IsAny<CancellationToken>()), Times.Once);
  ```
  The test used `Mock<IPushSubscriptionStore>` to verify only that `(dto, null)` was passed. Because `dto` was never sanitized by the controller, `mockStore.Verify` passed, while hiding the reality that `dto.UserId` was still `"victim-user-id"` and that the actual registered store accepts the spoofed ID.

- **Empirically Verified Failure Output (PushNotificationAdversarialTests)**:
  Command: `dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"`
  ```
  Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit [2 s]
  Error Message:
   Assert.Empty() Failure: Collection was not empty
  Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/http-exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = e2e-victim-user-id }]

  Failed SRNSMudApp.Tests.Push.PushNotificationAdversarialTests.Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit [18 ms]
  Error Message:
   Assert.Empty() Failure: Collection was not empty
  Collection: [PushSubscriptionDto { Endpoint = https://attacker.com/push/exploit-endpoint, Keys = PushSubscriptionKeysDto { P256Dh = attacker-p256, Auth = attacker-auth }, UserId = victim-target-user-id }]
  ```

---

### Observation 2: DATA-01 / DATA-02 ChangeTracker State Accumulation Mechanics
- **File**: `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 205 and 244–297)
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

      var depositTx = new JpycDepositTransaction
      {
          OwnerId = userId,
          DepositAddress = wallet.DepositAddress,
          TransactionHash = normalizedTx,
          NetworkName = request.NetworkName,
          AmountJpyc = verification.AmountJpyc > 0 ? verification.AmountJpyc : request.TotalJpycAmount,
          TargetTagId = tag.Id,
          RightAssetAmount = request.Amount,
          RightAssetId = newAsset.Id,
          Status = JpycDepositStatus.Confirmed,
          VerifiedAt = DateTime.UtcNow
      };

      dbContext.JpycDepositTransactions.Add(depositTx);
      ...
      dbContext.Items.Add(purchaseItem);
      await dbContext.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      return Result.Ok(newAsset);
  });
  ```
  `dbContext` is created outside `ExecuteWithStrategyAsync`.
  Inside the retry delegate, `dbContext.ChangeTracker.Clear()` is never called.
  When an exception triggers a retry in `IExecutionStrategy`:
  1. The database transaction rolls back, but EF Core's in-memory `ChangeTracker` does **not** roll back.
  2. On attempt 2, the delegate creates brand new entity instances and adds them to `dbContext`.
  3. `ChangeTracker` now contains duplicate entity entries.

- **Empirically Verified Failure Output 1 (Double-Minting on First Save Transient Retry)**:
  Command: `dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave"`
  ```
  Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets [102 ms]
  Error Message:
   Assert.Single() Failure: The collection contained 2 items
  Collection: [RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:55:01.1347500, Id = 3, IsBurned = False, ··· }, RightAsset { Amount = 2, BurnStatusJson = "", CreatedDate = 2026-10-02T14:55:01.1347500, Id = 4, IsBurned = False, ··· }]
  ```

- **Empirically Verified Failure Output 2 (Unique Key Crash on Second Save Transient Retry)**:
  Command: `dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave"`
  ```
  Failed SRNSMudApp.Tests.Services.RightAssetPurchaseServiceTests.PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically [11 s]
  Error Message:
   Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.
  ---- Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'dbo.JpycDepositTransactions' with unique index 'IX_JpycDepositTransactions_TransactionHash'. The duplicate key value is (0xc2077d35dbe21230ac3247288ece9b1fe345ad0515bfdb54db34337d78f3b1fd).
  ```

---

### Observation 3: THREAD-01 Concurrency Verification
- **File**: `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 59–63)
  ```csharp
  using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
  HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
  ```
- Command: `dotnet test --filter "FullyQualifiedName~THREAD01"`
  Result: **PASSED** (100 concurrent requests completed in 105 ms with zero header cross-contamination or mutations).

---

## 2. Logic Chain

1. **SEC-01 Logic Chain**:
   - The browser sends JSON containing arbitrary `userId`.
   - `PushNotificationController.Subscribe` determined `userId` from claims (`null` if unauthenticated).
   - However, the controller forwarded the unmodified `subscription` record to `_subscriptionStore.AddOrUpdateAsync`.
   - In `InMemoryPushSubscriptionStore.AddOrUpdateAsync`, `effectiveUserId = userId ?? subscription.UserId;` treats `userId: null` as missing rather than unauthenticated, falling back to `subscription.UserId`.
   - The victim's user ID is associated with the attacker's endpoint.
   - The worker's unit test asserted `mockStore.Verify(s => s.AddOrUpdateAsync(dto, null, ...))` which certified the flawed signature without sanitization, qualifying as a self-certifying facade.
   - **Conclusion**: Both layers (Controller and Store) must enforce the security boundary. The controller must sanitize `subscription with { UserId = userId }`, and the store must enforce `effectiveUserId = userId` without fallback to untrusted DTO properties.

2. **DATA-01 / DATA-02 Logic Chain**:
   - EF Core's `IExecutionStrategy.ExecuteAsync` catches transient database exceptions and re-runs the entire delegate.
   - The database transaction rolls back uncommitted rows in SQL Server, but `DbContext.ChangeTracker` is an in-memory structure that remains dirty.
   - Entities added in attempt 1 remain in `ChangeTracker` in `EntityState.Added` (or `EntityState.Unchanged`).
   - When attempt 2 runs, it instantiates new entities and calls `.Add()`. `ChangeTracker` now contains two sets of entities.
   - On attempt 2's `SaveChangesAsync`:
     - In case 1 (timeout during first save): EF Core inserts both `RightAsset` instances into SQL Server, creating 2 assets instead of 1 (Double-Minting).
     - In case 2 (timeout during second save): EF Core attempts to insert both `JpycDepositTransaction` instances with the same `TransactionHash`, triggering SQL Server unique constraint violation `IX_JpycDepositTransactions_TransactionHash` and crashing the operation.
   - **Conclusion**: Calling `dbContext.ChangeTracker.Clear()` at the very start of the retry delegate ensures that any dirty in-memory state from previous failed attempts is completely discarded before starting a new transaction attempt.

---

## 3. Caveats
- `InMemoryPushSubscriptionStore` is a singleton in-memory repository suitable for single-instance deployment. In distributed environments, Redis or SQL Server would be backed, but the interface boundary rules (`effectiveUserId = userId`) remain identical.
- Existing UI test failures in `ProposeContractDialogTests` and `TagNodeWidgetTests` are pre-existing Blazor circuit tests completely unrelated to Milestone 1 and must remain untouched.

---

## 4. Conclusion & Actionable Remediation Plan for Worker (worker_m1_it2)

### Required Code Modifications

#### 1. `SRNSMudApp/Controllers/PushNotificationController.cs`
In method `Subscribe`:
Sanitize the incoming record with `userId` before calling `_subscriptionStore.AddOrUpdateAsync`:
```csharp
/// Controllers/PushNotificationController.cs
// SEC-01: クライアント提供の DTO に含まれる UserId を信用せず、認証状態に基づく userId で DTO を上書き正規化する。
// 未認証時は null、認証時はクレーム由来の ID を設定してユーザーIDの詐称登録を遮断する。
var sanitizedSubscription = subscription with { UserId = userId };

await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
return Ok(new { message = "サブスクリプションの登録に成功しました。", userId });
```

#### 2. `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`
In method `AddOrUpdateAsync`:
Enforce strict authority of `userId` and eliminate untrusted fallback to `subscription.UserId`:
```csharp
/// Services/Push/InMemoryPushSubscriptionStore.cs
public Task AddOrUpdateAsync(PushSubscriptionDto subscription, string? userId = null, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(subscription);
    if (string.IsNullOrWhiteSpace(subscription.Endpoint))
    {
        throw new ArgumentException("Endpoint must not be empty.", nameof(subscription));
    }

    // SEC-01: 認証境界を厳格に保護するため、引数で渡された userId を最優先とし、未認証 (null) の場合も
    // クライアント側 DTO の UserId へのフォールバックを行わない。
    string? effectiveUserId = userId;
    var dtoWithUserId = subscription with { UserId = effectiveUserId };
    _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
    return Task.CompletedTask;
}
```

#### 3. `SRNSMudApp/Services/RightAssetPurchaseService.cs`
In method `PurchaseRightAssetWithJpycAsync`:
Add `dbContext.ChangeTracker.Clear();` at the very beginning of the `ExecuteWithStrategyAsync` lambda:
```csharp
/// Services/RightAssetPurchaseService.cs
return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
{
    // DATA-01 / DATA-02: リトライ発生時に前回の試行で追跡されたエンティティ状態（Added/Modified等）が
    // ChangeTracker に残存していると、二重登録（アセット二重発行）や一意制約違反（同一 TxHash の重複挿入）が発生する。
    // 各試行の開始時に ChangeTracker をクリアして、常にクリーンな状態でトランザクションを開始する。
    dbContext.ChangeTracker.Clear();

    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    // 新規 RightAsset の発行
    var newAsset = new RightAsset
    ...
```

#### 4. `SRNSMudApp/Data/ExecutionStrategyExtensions.cs`
Update the XML documentation on `ExecuteWithStrategyAsync` methods to state:
```csharp
/// Data/ExecutionStrategyExtensions.cs
/// 【重要 (DATA-01/DATA-02)】: <see cref="IExecutionStrategy.ExecuteAsync" /> はデリゲート全体の自動再試行機構であり、
/// トランザクション境界は自動作成されません。
/// 複数回の SaveChangesAsync を呼び出す場合やアトミック性が必須の操作では、
/// 渡された操作デリゲート内部で await using var tx = await database.BeginTransactionAsync(cancellationToken);
/// を開始し、最後に await tx.CommitAsync(cancellationToken); を呼び出す必要があります。
/// また、リトライ時の状態蓄積を防ぐため、デリゲートの先頭で dbContext.ChangeTracker.Clear() を呼び出すか、
/// 試行ごとに DbContext を新規生成してください。
```

#### 5. `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (Recommended Quality Hardening)
In `VerifyLineTokenAsync` and `VerifyGithubTokenAsync`, add `catch (FormatException ex)` to handle malformed bearer tokens without unhandled 500 exceptions:
```csharp
catch (FormatException ex)
{
    return LogAndReturnFailure("Invalid token format", ex);
}
```

---

### Non-Facade Test Implementation Plan

#### In `SRNSMudApp.Tests/Push/PushNotificationTests.cs`:
1. **Fix Mock Invocations**:
   Because `subscription` is sanitized before passing to `_subscriptionStore.AddOrUpdateAsync`, the mock expectations must match the sanitized DTO:
   - In `PushNotificationController_Subscribe_WhenAuthenticated_CallsAddOrUpdateAsync`:
     `mockStore.Verify(s => s.AddOrUpdateAsync(It.Is<PushSubscriptionDto>(sub => sub.UserId == "auth-user-123"), "auth-user-123", It.IsAny<CancellationToken>()), Times.Once);`
   - In `PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull`:
     `mockStore.Verify(s => s.AddOrUpdateAsync(It.Is<PushSubscriptionDto>(sub => sub.UserId == null), null, It.IsAny<CancellationToken>()), Times.Once);`

2. **Add Non-Facade Integration Unit Test**:
   Directly test `PushNotificationController` with real `InMemoryPushSubscriptionStore`:
   ```csharp
   [Fact]
   public async Task PushNotificationController_Subscribe_WithRealStore_WhenUnauthenticated_RejectsSpoofedUserId()
   {
       // Arrange: 実際の InMemoryPushSubscriptionStore を使用して結合テスト（モックによる偽陽性を排除）
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

       var spoofedDto = new PushSubscriptionDto(
           "https://example.com/push/real-store-test",
           new PushSubscriptionKeysDto("p256", "auth"),
           UserId: "victim-account-id"
       );

       // Act
       var result = await controller.Subscribe(spoofedDto, default) as OkObjectResult;
       Assert.NotNull(result);

       // Assert: 被害者の UserId に登録されていないこと
       var victimSubscriptions = await realStore.GetByUserIdAsync("victim-account-id");
       Assert.Empty(victimSubscriptions);

       var allSubscriptions = await realStore.GetAllAsync();
       var stored = Assert.Single(allSubscriptions);
       Assert.Null(stored.UserId);
   }
   ```

---

## 5. Verification Method

To independently verify the complete fix:

```bash
# 1. Verify SEC-01 exploit fix (must pass 4/4 tests):
dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"

# 2. Verify DATA-01 / DATA-02 retry fix (must pass 2/2 tests without duplication or index collision):
dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"

# 3. Verify THREAD-01 concurrency safety (must pass 1/1 test):
dotnet test --filter "FullyQualifiedName~THREAD01"

# 4. Verify full unit test suites for modified components:
dotnet test --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~RightAssetPurchaseServiceTests"

# 5. Verify build succeeds with 0 errors:
dotnet build
```

### Invalidation Conditions
This report's recommendations are invalidated if and only if:
1. `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_DirectStoreExploit` or `Challenge_Subscribe_UnauthenticatedAttacker_SpoofsVictimUserId_EndToEndHttpExploit` still fail after applying the controller and store changes.
2. `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets` or `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically` fail with `Assert.Single` failure or `DbUpdateException`.
