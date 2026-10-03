# Handoff Report: Milestone 1 (Security, Concurrency & Data Integrity)

## 1. Observation
- **SEC-01**: In `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 50-51 before edit):
  ```csharp
  // ログイン中のユーザーIDを優先、なければリクエストボディのUserIdを使用
  string? userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? subscription.UserId;
  ```
  Unauthenticated clients could supply arbitrary `subscription.UserId` in request body, binding push notification subscriptions to victims' accounts. Additionally, `SendNotification` (`[HttpPost("send")]`) had no `[Authorize]` attribute, allowing unauthorized callers to broadcast push notifications.
- **THREAD-01**: In `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs` (lines 58-61 before edit):
  ```csharp
  _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
  HttpResponseMessage response = await _httpClient.GetAsync(new Uri("https://api.line.me/v2/profile"), cancellationToken);
  ```
  Mutating shared `_httpClient.DefaultRequestHeaders` caused a race condition under concurrent requests, potentially leaking or overwriting tokens between different user authentications.
- **DATA-01 & DATA-02**: In `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 242-288 before edit):
  `RightAsset` was inserted and saved with `await dbContext.SaveChangesAsync(cancellationToken)` before saving `JpycDepositTransaction` and `purchaseItem` with a second `SaveChangesAsync`. In failure scenarios between the two saves, `RightAsset` would be minted without recording `JpycDepositTransaction`, allowing replay attacks (double-spending).
  Furthermore, in `SRNSMudApp/Data/ExecutionStrategyExtensions.cs` (lines 20-26 before edit), the documentation erroneously claimed:
  `"トランザクションは内部で管理されるため、明示的な Begin/Commit/Rollback は不要。"`
  In EF Core, `IExecutionStrategy.ExecuteAsync` does NOT create ambient transactions for multi-save operations.

## 2. Logic Chain
1. **Fixing SEC-01 in `PushNotificationController.cs`**:
   - Added `[Authorize(Roles = "Admin")]` to `SendNotification` to restrict broadcast push notifications to admin users.
   - In `Subscribe`, resolved `authenticatedUserId` via `User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value`.
   - If `User?.Identity?.IsAuthenticated == true` and `authenticatedUserId` is non-empty, use `authenticatedUserId`. If unauthenticated, set `userId = null` rather than accepting client-provided `subscription.UserId`.
2. **Fixing THREAD-01 in `ExternalTokenVerificationService.cs`**:
   - Replaced shared `_httpClient.DefaultRequestHeaders.Authorization` mutation with a per-request `HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"))` setting `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken)` and calling `await _httpClient.SendAsync(request, cancellationToken)`.
   - Leaves `_httpClient.DefaultRequestHeaders` untouched and completely thread-safe across concurrent calls.
3. **Fixing DATA-01 & DATA-02 in `RightAssetPurchaseService.cs` & `ExecutionStrategyExtensions.cs`**:
   - In `RightAssetPurchaseService.cs`, wrapped the creation of `RightAsset`, `JpycDepositTransaction`, and `purchaseItem` inside `await dbContext.Database.ExecuteWithStrategyAsync(async () => { await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken); ... await transaction.CommitAsync(cancellationToken); return Result.Ok(newAsset); })`.
   - This ensures atomic commit on SQL Server with retry strategy support. If any step fails, the entire transaction rolls back, preventing partial commits or unrecorded asset minting.
   - In `ExecutionStrategyExtensions.cs`, corrected the XML documentation to clarify that `IExecutionStrategy` only handles retry delegates and does NOT automatically start transactions; multi-save operations must explicitly use `BeginTransactionAsync`/`CommitAsync` inside the delegate.
4. **Verification with Tests**:
   - Added 4 unit tests in `SRNSMudApp.Tests/Push/PushNotificationTests.cs`:
     - `PushNotificationController_SendNotification_HasAdminAuthorizeAttribute`
     - `PushNotificationController_Subscribe_WhenAuthenticated_UsesClaimUserId`
     - `PushNotificationController_Subscribe_WhenAuthenticatedWithSub_UsesSubClaimUserId`
     - `PushNotificationController_Subscribe_WhenUnauthenticated_IgnoresRequestBodyUserIdAndStoresNull`
   - Created new test suite in `SRNSMudApp.Tests/Auth/ExternalTokenVerificationServiceTests.cs` (4 unit tests):
     - `VerifyLineTokenAsync_UsesPerRequestAuthorizationHeaderAndDoesNotMutateDefaultHeaders`
     - `VerifyLineTokenAsync_ConcurrentRequests_DoNotMutateSharedHeadersOrCrossContaminate`
     - `VerifyTokenAsync_UnsupportedProvider_ReturnsFailure`
     - `VerifyLineTokenAsync_WhenLineReturnsError_ReturnsFailure`
   - Added integration test in `SRNSMudApp.Tests/Services/RightAssetPurchaseServiceTests.cs`:
     - `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackRightAssetAtomically` using a custom `SaveChangesInterceptor` to simulate failure during deposit transaction persistence and asserting zero `RightAsset` records survive.
   - Formatted all code cleanly and verified zero warnings via `dotnet format --diagnostics IDE0055 --verify-no-changes`.

## 3. Caveats
- `RightAssetDataProvider.cs` contains 8 existing code analysis warnings (`CA1848`, `CA1873`) which are outside Milestone 1 write scope and assigned to subsequent milestones. No new warnings were introduced in Milestone 1 files.
- The 4 pre-existing test failures in the full test suite (`ProposeContractDialogTests` and `TagNodeWidgetTests`) belong to UI components and were already failing in the baseline run before Milestone 1 changes were made.

## 4. Conclusion
All objectives for Milestone 1 (SEC-01, THREAD-01, DATA-01, DATA-02) have been genuinely and fully implemented, tested, and verified.
- SEC-01: Admin authorization enforced on broadcast; spoofed `userId` rejected for unauthenticated push subscriptions.
- THREAD-01: Per-request `HttpRequestMessage` eliminates token race condition on shared `HttpClient`.
- DATA-01 & DATA-02: `RightAssetPurchaseService` transactions are strictly atomic under `ExecuteWithStrategyAsync` with verified automatic rollback; `ExecutionStrategyExtensions` documentation is accurate.

## 5. Verification Method
Run the following commands from the workspace root:

```bash
# 1. Build verification (0 errors)
dotnet build

# 2. Format verification (0 formatting issues)
dotnet format --diagnostics IDE0055 --verify-no-changes

# 3. Unit & Integration test verification for all Milestone 1 areas
dotnet test --filter "FullyQualifiedName~PushNotificationTests|FullyQualifiedName~PushNotificationIntegrationTests|FullyQualifiedName~RightAssetPurchaseServiceTests|FullyQualifiedName~ExternalTokenVerificationServiceTests|FullyQualifiedName~ExternalLoginCallbackIntegrationTests"
```
Verification result:
- `dotnet build`: Exited with code 0 (0 errors, 0 warnings on modified files).
- `dotnet format`: Exited with code 0 (0 violations).
- `dotnet test`: 47 tests passed, 0 failed, 0 skipped.
