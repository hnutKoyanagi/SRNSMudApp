# Dispatch: Worker M1 Iteration 2 (Remediation Implementation)

## Objective
Implement forensic remediations for SEC-01, DATA-01/DATA-02, and THREAD-01 hardening per `teamwork_preview_explorer_m1_it2_3` handoff blueprint.

## Exclusive Write Ownership
You own and may edit ONLY the following files:
- `SRNSMudApp/Controllers/PushNotificationController.cs`
- `SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`
- `SRNSMudApp/Services/RightAssetPurchaseService.cs`
- `SRNSMudApp/Data/ExecutionStrategyExtensions.cs`
- `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`
- `SRNSMudApp.Tests/Push/PushNotificationTests.cs`
- `SRNSMudApp.Tests/Auth/ExternalTokenVerificationServiceTests.cs`
- `SRNSMudApp.Tests/Services/RightAssetPurchaseServiceTests.cs`

## Blueprint Reference
Read the exact before/after instructions in:
`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_m1_it2_3/handoff.md`

## Specific Fixes
1. **SEC-01**:
   - In `PushNotificationController.cs`: sanitize DTO before forwarding:
     `var sanitizedSubscription = subscription with { UserId = userId };`
     `await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);`
   - In `InMemoryPushSubscriptionStore.cs`: set `string? effectiveUserId = userId;` without falling back to `subscription.UserId`.
   - In `PushNotificationTests.cs`: update mock expectations to assert sanitized DTO, and add non-mocked behavioral test using real `InMemoryPushSubscriptionStore`.
2. **DATA-01 / DATA-02**:
   - In `RightAssetPurchaseService.cs`: add `dbContext.ChangeTracker.Clear();` at the start of the `ExecuteWithStrategyAsync` retry delegate.
   - In `ExecutionStrategyExtensions.cs`: update documentation on `ChangeTracker.Clear()` and transaction requirements.
3. **THREAD-01**:
   - In `ExternalTokenVerificationService.cs`: catch `FormatException` and return `LogAndReturnFailure("Invalid ... token format", ex)`.
   - In `ExternalTokenVerificationServiceTests.cs`: add tests verifying `FormatException` returns `Failure`.

## Verification Commands
Ensure ALL pass:
```bash
dotnet format --diagnostics IDE0055 --verify-no-changes
dotnet build
dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"
dotnet test --filter "FullyQualifiedName~PushNotificationTests"
dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"
dotnet test --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"
dotnet test --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"
dotnet test --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"
```

## MANDATORY INTEGRITY WARNING
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Write your handoff report to:
`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1_it2/handoff.md`


## 2026-10-02T14:57:14Z
You are worker_m1_it2.
Your working directory is: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1_it2
Read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/ORIGINAL_REQUEST.md before starting work.
Also read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/PROJECT.md.
Also read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1_it2/DISPATCH.md.
Also read the detailed remediation blueprint in: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_m1_it2_3/handoff.md.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Implement the exact remediations:
1. SEC-01:
   - In PushNotificationController.cs: Sanitize incoming DTO before passing to store:
     var sanitizedSubscription = subscription with { UserId = userId };
     await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
   - In InMemoryPushSubscriptionStore.cs: Eliminate untrusted fallback:
     string? effectiveUserId = userId;
   - In PushNotificationTests.cs: Update mock expectations to match sanitized DTO, and add non-mocked integration unit test using real InMemoryPushSubscriptionStore.
2. DATA-01 / DATA-02:
   - In RightAssetPurchaseService.cs: Add dbContext.ChangeTracker.Clear(); at the very beginning of the ExecuteWithStrategyAsync delegate.
   - In ExecutionStrategyExtensions.cs: Update xmldoc comments explaining ChangeTracker.Clear() requirement.
3. THREAD-01:
   - In ExternalTokenVerificationService.cs: Catch FormatException on bearer token parsing and return domain Failure.
   - In ExternalTokenVerificationServiceTests.cs: Add unit tests verifying FormatException handling.

Verify your changes:
- Run dotnet format --diagnostics IDE0055 --verify-no-changes
- Run dotnet build
- Run all adversarial and unit test suites:
  dotnet test --filter "FullyQualifiedName~PushNotificationAdversarialTests"
  dotnet test --filter "FullyQualifiedName~PushNotificationTests"
  dotnet test --filter "FullyQualifiedName~PurchaseRightAssetWithJpycAsync_WhenTransientFailure"
  dotnet test --filter "FullyQualifiedName~WhenDepositTransactionSaveFails"
  dotnet test --filter "FullyQualifiedName~THREAD01_StressTest_HighConcurrency_LineTokenVerification"
  dotnet test --filter "FullyQualifiedName~ExternalTokenVerificationServiceTests"

Write your handoff report to:
/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1_it2/handoff.md
Send a message to orchestrator parent when complete.
