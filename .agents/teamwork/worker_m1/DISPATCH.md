# Dispatch: Worker M1 (Security, Concurrency & Data Integrity)

## Objective
Implement critical fixes for SEC-01, THREAD-01, DATA-01, and DATA-02 across SRNSWebApp.

## Exclusive Write Ownership
You own and may edit ONLY the following files:
- `SRNSMudApp/Controllers/PushNotificationController.cs`
- `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`
- `SRNSMudApp/Services/RightAssetPurchaseService.cs`
- `SRNSMudApp/Data/ExecutionStrategyExtensions.cs`
- Related unit tests in `SRNSMudApp.Tests/` (e.g. `ExternalTokenVerificationServiceTests.cs`, `PushNotificationControllerTests.cs`, `RightAssetPurchaseServiceTests.cs`)

## Specific Fix Requirements
1. **SEC-01**: In `SRNSMudApp/Controllers/PushNotificationController.cs`:
   - Add `[Authorize(Roles = "Admin")]` to `SendNotification` (`[HttpPost("send")]`).
   - In `Subscribe`, ensure `userId` is obtained from authenticated claims `User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value`. If unauthenticated or null, do not allow arbitrary spoofed userId without validation.
2. **THREAD-01**: In `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`:
   - In `VerifyLineTokenAsync`: Do NOT mutate `_httpClient.DefaultRequestHeaders.Authorization`. Use a per-request `HttpRequestMessage(HttpMethod.Get, ...)` with `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken)` and call `await _httpClient.SendAsync(request, cancellationToken)`.
3. **DATA-01 & DATA-02**: In `SRNSMudApp/Services/RightAssetPurchaseService.cs`:
   - Ensure `RightAsset` and `JpycDepositTransaction` are committed atomically. Wrap them in a transaction using `await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken); ... await tx.CommitAsync(cancellationToken);` or add both entities before calling `SaveChangesAsync` once.
   - In `Data/ExecutionStrategyExtensions.cs`: Update documentation comments to clarify transaction boundaries.

## Coding Standards & Guidelines
- Japanese comments explaining design rationale.
- Async I/O, C# 15 features, primary constructors where appropriate.
- Run `dotnet build` to ensure 0 errors and no new warnings.
- Run `dotnet test` to ensure existing and updated tests pass.
- Format with `dotnet format --diagnostics IDE0055` if necessary.

## MANDATORY INTEGRITY WARNING
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Deliverables
Write your handoff report to:
`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1/handoff.md`
Report files changed, build results, and test results.


## 2026-10-02T14:17:55Z
You are worker_m1.
Your working directory is: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1
Read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/ORIGINAL_REQUEST.md before starting work.
Also read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/PROJECT.md.
Also read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1/DISPATCH.md.
Also read skills: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/skills/dotnet-best-practices/SKILL.md and /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/skills/dotnet-design-pattern-review/SKILL.md.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Your task is Milestone 1: Security, Concurrency & Data Integrity.
Exclusive write ownership:
- SRNSMudApp/Controllers/PushNotificationController.cs
- SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs
- SRNSMudApp/Services/RightAssetPurchaseService.cs
- SRNSMudApp/Data/ExecutionStrategyExtensions.cs
- Related test files in SRNSMudApp.Tests/

Implement the fixes:
1. SEC-01 in PushNotificationController.cs: Add [Authorize(Roles = "Admin")] on SendNotification; ensure Subscribe validates authenticated userId.
2. THREAD-01 in ExternalTokenVerificationService.cs: Do not mutate _httpClient.DefaultRequestHeaders.Authorization. Use per-request HttpRequestMessage in VerifyLineTokenAsync.
3. DATA-01 / DATA-02 in RightAssetPurchaseService.cs: Ensure atomic persistence of RightAsset and JpycDepositTransaction using an explicit transaction or single SaveChangesAsync. Update ExecutionStrategyExtensions.cs documentation.

Verify your changes:
- Run dotnet build to ensure 0 errors.
- Run dotnet test to ensure all unit and integration tests pass.

Write your handoff report to:
/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/worker_m1/handoff.md
Include modified files, build output, test results, and verification commands.
Send a message to orchestrator parent when complete.
