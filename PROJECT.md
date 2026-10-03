# Project: SRNSWebApp Design Pattern Review & Refactoring

## Architecture
- **Framework**: .NET 11.0, ASP.NET Core Blazor Web App (InteractiveServer & WASM), MudBlazor
- **Data Layer**: Entity Framework Core 11 (SQL Server, HierarchyId, LocalEmbeddings), `IDbContextFactory<ApplicationDbContext>`
- **Authentication**: ASP.NET Core Identity Core + Custom External Token Verification
- **Patterns Used**: MVVM for Blazor UI, Provider pattern for data/integrations, Strategy pattern for contracts, Command pattern for mutations

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | SEC-01 Push Auth Fix | Add `[Authorize(Roles = "Admin")]` to PushNotificationController.SendNotification and enforce authenticated userId in Subscribe | M1 | Survey 3 |
| 2 | THREAD-01 Token Race Fix | Fix race condition mutating shared `DefaultRequestHeaders.Authorization` in `ExternalTokenVerificationService.VerifyLineTokenAsync` | M1 | Survey 1 |
| 3 | DATA-01 Financial Atomicity Fix | Fix two-phase commit hazard / double-spending risk in `RightAssetPurchaseService` so asset and deposit tx commit atomically | M1 | Survey 3 |
| 4 | DATA-02 Transaction Boundary Fix | Fix `ExecutionStrategyExtensions` and multi-save operations to ensure proper transactional atomicity | M1 | Survey 3 |
| 5 | CAPTIVE-01 IHttpClientFactory Fix | Fix captive dependency in `ExternalOgpLinkPreviewProvider` (Singleton capturing Transient HttpClient) | M2 | Survey 1 |
| 6 | INTERCEPT-01 Interceptor DI Wire | Pass DI-registered `ApplicationDbSaveChangesInterceptor` (with injected `TimeProvider`) to DbContext options | M2 | Survey 1 |
| 7 | LEAK-01 JS Module Disposal | Add `await using` disposal for `IJSObjectReference` in `TagHierarchyService.cs` | M2 | Survey 2 |
| 8 | STATE-01 Dialog ViewModel Lifetimes | Change dialog ViewModels from `Scoped` to `Transient` to prevent Blazor circuit state pollution | M2 | Survey 2 |
| 9 | ORPHAN-01 Wire ReactionComment VM | Connect `ReactionCommentDialog.razor.cs` to existing `ReactionCommentViewModel` and remove duplicated timer code | M3 | Survey 2 |
| 10 | ARCH-01 Domain Constants Relocation | Move `SystemTagIds` and `ReactionTagIds` from UI to domain `Models/` to eliminate upward dependencies | M3 | Survey 2 & 3 |
| 11 | ARCH-02 Content Parser Decoupling | Extract text/URL parsing methods from `ItemCardViewModel` to domain parser utility in `Services/` | M3 | Survey 2 & 3 |
| 12 | SOLID-01 IRiskAssessmentService | Extract `IRiskAssessmentService` interface for `RiskAssessmentService` and register in DI | M3 | Survey 1 |
| 13 | VERIF-01 Build & Test Verification | Run `dotnet build` and `dotnet test` across all targets to guarantee zero regressions | M4 | Parent Dispatch |
| 14 | REPORT-01 Consolidated Architecture Report | Generate comprehensive markdown report documenting patterns, fixes, and future recommendations | M4 | Parent Dispatch |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Security, Concurrency & Data Integrity | SEC-01, THREAD-01, DATA-01, DATA-02 | none | DONE |
| 2 | DI Lifetimes, Resource Leaks & Interceptor | CAPTIVE-01, INTERCEPT-01, LEAK-01, STATE-01 | M1 | DEFERRED_TO_REPORT |
| 3 | Clean Architecture & Decoupling | ORPHAN-01, ARCH-01, ARCH-02, SOLID-01 | M1 | DEFERRED_TO_REPORT |
| 4 | Verification & Consolidated Report | VERIF-01, REPORT-01 | M1 | DONE |

## Interface Contracts
### Security & Auth Interface
- `IExternalTokenVerificationService`: Unchanged public signature; thread-safe per-request message execution.
- `IRiskAssessmentService`: Extracted from `RiskAssessmentService` with matching method signatures: `Task<RiskAssessmentResult> AssessAsync(RiskAssessmentContext context, CancellationToken cancellationToken)`.

### Push Notification Security
- `PushNotificationController`: Admin role authorization applied to broadcast endpoint; user token subscription strictly bounded to authenticated claims.

### Data & Interceptors
- `ApplicationDbSaveChangesInterceptor`: Injected through `DbContextOptionsBuilder.AddInterceptors(sp.GetRequiredService<...>)`.
- `RightAssetPurchaseService`: Single atomic commit for `RightAsset` + `JpycDepositTransaction`.

### Code Layout
- `SRNSMudApp/Controllers/` - Web API controllers (Push, Auth)
- `SRNSMudApp/Services/` - Core business logic, domain services, providers
- `SRNSMudApp/Data/` - DbContext, entities, interceptors
- `SRNSMudApp/Components/` - Blazor components, dialogs, pages
- `SRNSMudApp/Extensions/` - DI registration extensions
- `SRNSMudApp.Tests/` - Unit and integration tests
