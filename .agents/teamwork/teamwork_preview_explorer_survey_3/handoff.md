# Comprehensive Audit Report: Data Access, DbContext Lifetimes, Repositories, Controllers, Models & Clean Architecture

**Author**: Teamwork Explorer (Survey Subagent 3)  
**Date**: 2026-10-02  
**Target Solution**: `SRNSWebApp` (`SRNSMudApp`, `SRNSMudApp.Client`, `SRNSMudApp.Tests`)  
**Scope**: `Data/`, `Controllers/`, `Models/`, `Services/`, DbContext lifetimes, EF Core concurrency, Query efficiency, Clean Architecture & SOLID boundaries.

---

## Executive Summary

An in-depth architectural and code audit of data access, concurrency, domain models, controllers, and Clean Architecture boundaries was conducted across `SRNSWebApp`. The solution utilizes .NET 11 preview, ASP.NET Core Blazor Web App (Interactive Server & WASM), MudBlazor, and Entity Framework Core 11 with SQL Server (`HierarchyId` and `LocalEmbeddings`).

While the application has successfully adopted `IDbContextFactory<ApplicationDbContext>` across many UI DataProviders, the audit identified **3 Critical severity issues**, **5 High severity issues**, **6 Medium severity issues**, and **2 Low severity issues**. Notably:
1. **False Transaction Atomicity (Critical)**: `ExecutionStrategyExtensions.cs` mistakenly claims that execution strategy manages transactions automatically; multiple `SaveChangesAsync` calls inside `ExecuteWithStrategyAsync` lack explicit transactions, leading to partial commits and data corruption during transient failure retries.
2. **Financial Double-Spending / Transaction Separation (Critical)**: `RightAssetPurchaseService.cs` mints and commits `RightAsset` in a separate `SaveChangesAsync` before saving `JpycDepositTransaction`, allowing unrecorded asset minting upon failure.
3. **Unauthenticated Public Push Broadcast (Critical)**: `PushNotificationController.cs` exposes `[HttpPost("send")]` without any authentication or authorization, permitting anonymous arbitrary push notifications to all users.
4. **Clean Architecture Inversion (High)**: Multiple domain services in `Services/` directly import and depend on UI ViewModels and components in `Components/UI/` and `Components/Pages/`.
5. **N+1 and Massive Table Scans on High-Frequency Hot Paths (High)**: Notification badges invoke 15 sequential database round-trips for a single unread count, and search/tag suggestion services load all database tags with embedding arrays into memory on autocomplete keystrokes.

---

## 1. Observations

### 1.1 DbContext Lifetimes, Transactions & Concurrency

#### Observation 1.1.1: Misleading Execution Strategy and Multi-Save Partial Commits
- **File**: `SRNSMudApp/Data/ExecutionStrategyExtensions.cs` (lines 20–36, 43–52)
```csharp
/// <param name="operation">実行する操作。トランザクションは内部で管理されるため、明示的な Begin/Commit/Rollback は不要。</param>
public static async Task<TResult> ExecuteWithStrategyAsync<TResult>(
    this DatabaseFacade database,
    Func<Task<TResult>> operation)
{
    ArgumentNullException.ThrowIfNull(database);
    ArgumentNullException.ThrowIfNull(operation);

    var strategy = database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync(operation);
}
```
- **Files using this pattern without transactions**:
  - `SRNSMudApp/Data/ApplicationDbContextTagExtensions.cs` (lines 93–165): calls `context.Database.ExecuteWithStrategyAsync` containing 3 separate `SaveChangesAsync()` calls (lines 114, 126, 164) with no `BeginTransactionAsync()`.
  - `SRNSMudApp/Services/ItemTagService.cs` (lines 96–117): calls `ExecuteWithStrategyAsync` containing 2 separate `SaveChangesAsync()` calls (lines 112, 116) with no `BeginTransactionAsync()`.
  - `SRNSMudApp/Services/TagEdgeService.cs` (lines 145–187): calls `ExecuteWithStrategyAsync` containing 2 separate `SaveChangesAsync()` calls (lines 164, 184) with no `BeginTransactionAsync()`.
  - `SRNSMudApp/Services/TagRelationService.cs` (lines 38–80, 140–191): calls `ExecuteWithStrategyAsync` containing multiple `SaveChangesAsync()` calls (lines 48, 58, 86; lines 171, 191) with no `BeginTransactionAsync()`.
  - `SRNSMudApp/Services/TagTreeDataProvider.cs` (lines 66–174): calls `ExecuteWithStrategyAsync` containing 2 separate `SaveChangesAsync()` calls (lines 163, 167) with no `BeginTransactionAsync()`.

#### Observation 1.1.2: Two-Phase Commit Hazard in RightAsset JPYC Purchase
- **File**: `SRNSMudApp/Services/RightAssetPurchaseService.cs` (lines 242–285)
```csharp
// 新規 RightAsset の発行
var newAsset = new RightAsset
{
    TargetTagId = tag.Id,
    OwnerId = userId,
    Amount = request.Amount,
    IsBurned = false
};

dbContext.RightAssets.Add(newAsset);
await dbContext.SaveChangesAsync(cancellationToken); // Save 1: Commits asset!

// JpycDepositTransaction レコードの保存
var depositTx = new JpycDepositTransaction
{
    OwnerId = userId,
    DepositAddress = wallet.DepositAddress,
    TransactionHash = normalizedTx,
    ...
};

dbContext.JpycDepositTransactions.Add(depositTx);
...
await dbContext.SaveChangesAsync(cancellationToken); // Save 2: Commits transaction record!
```
- If Save 2 fails or `cancellationToken` cancels, `newAsset` has already been saved to the database. `depositTx` is not saved, so the tx hash remains unused in `JpycDepositTransactions`.

#### Observation 1.1.3: Blazor Circuit-Scoped `UserManager` & DbContext Concurrency Conflict
- **File**: `SRNSMudApp/Program.cs` (lines 127–140, 163–166)
```csharp
_ = builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, ...),
    ServiceLifetime.Scoped, // Scoped for Identity
    ServiceLifetime.Singleton);

builder.Services.AddIdentityCore<ApplicationUser>(...)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
```
- **Files**: `SRNSMudApp/Components/User/UserDetailViewModel.cs` (lines 16–24) and `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs` (lines 184–189):
  `services.AddScoped(sp => new UserDetailViewModel(sp.GetRequiredService<IUserDataProvider>(), sp.GetService<UserManager<ApplicationUser>>()));`
- In Blazor Server, `Scoped` services stay alive for the circuit. `UserManager` uses the circuit-scoped `ApplicationDbContext`, while DataProviders create ephemeral contexts via `_dbFactory.CreateDbContextAsync()`. Concurrent async operations on `UserManager` within the circuit cause `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.

#### Observation 1.1.4: Blocking Synchronous I/O in Async EF SaveChanges Interceptor
- **File**: `SRNSMudApp/Data/Interceptors/ApplicationDbSaveChangesInterceptor.cs` (lines 37–54, 198–245, 56–78, 80–171)
```csharp
public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
{
    ...
    ValidateRootTagConstraint(context); // Calls synchronous LINQ:
    // context.Tags.Where(...).Select(...).OrderByDescending(...).FirstOrDefault()
    ...
}
```
- Line 832 in `ApplicationDbContext.OnConfiguring` instantiates `new ApplicationDbSaveChangesInterceptor()` directly with no parameters, ignoring the DI-registered `TimeProvider`.

---

### 1.2 Query Efficiency, N+1 Queries & Memory Pressures

#### Observation 1.2.1: Notifications 15-Query Avalanche on Every Unread Badge Fetch
- **File**: `SRNSMudApp/Services/NotificationService.cs` (lines 50–54):
```csharp
public async Task<int> GetUnreadCountAsync(string userId)
{
    IReadOnlyList<NotificationDto> notifications = await GetUserNotificationsAsync(userId);
    return notifications.Count(n => !n.IsRead);
}
```
- **File**: `SRNSMudApp/Services/NotificationsDataProvider.cs` (lines 67–240):
  `GetNotificationRawDataAsync` executes 15 distinct database queries with deep eager `.Include()` / `.ThenInclude()` graphs across 8 tables (`TaggingRequestEntity`, `Item`, `NotificationReadState`, `ContentReport`, `ItemSplitRequest`, `TagContentProposal`, `TagNameProposal`, `TagRelation`), materializes all raw entities into memory, and only then computes the unread count in C#.

#### Observation 1.2.2: Unbounded Vector Table Loads on Search Keystroke
- **File**: `SRNSMudApp/Services/ItemListDataProvider.cs` (lines 196–205):
```csharp
List<Tag> vectorTags = await context.Tags
    .Where(t => t.Embedding != null)
    .AsNoTracking()
    .ToListAsync(token);
```
- **File**: `SRNSMudApp/Services/TagSuggestionService.cs` (lines 55–58):
```csharp
List<Data.Tag> candidates = await dbContext.Tags
    .AsNoTracking()
    .Where(t => t.Embedding != null && t.Name != Data.Tag.RootTagName)
    .ToListAsync(cancellationToken);
```
- **File**: `SRNSMudApp/Services/TagHierarchyService.cs` (lines 55–60):
```csharp
List<Tag> allTags = await db.Tags
    .Where(t => !Tag.VoteTagNames.Contains(t.Name) &&
                !Tag.ReactionTagNames.Contains(t.Name) &&
                t.Name != Tag.RootTagName)
    .AsNoTracking()
    .ToListAsync(cancellationToken);
```
- **File**: `SRNSMudApp/Services/TagSearchQueryService.cs` (lines 56–62):
```csharp
List<Tag> vectorTags = await dbContext.Tags.Where(x => x.Embedding != null).AsNoTracking().ToListAsync().ConfigureAwait(false);
```
Every text suggestion or autocomplete keystroke loads every tag and its 384-element float array across the entire table into server memory.

#### Observation 1.2.3: N+1 Database Connections in Batch Tag Deletion
- **File**: `SRNSMudApp/Services/TagTreeDataProvider.cs` (lines 83–103):
```csharp
foreach (var id in authorizedIds)
{
    var isLocked = await _tagLockService.IsTagOrSiblingLockedAsync(id);
    ...
}
```
- Inside `TagLockService.IsTagOrSiblingLockedAsync` (`TagLockService.cs` lines 105–151), each call spawns a new `ApplicationDbContext` and executes 2–3 SQL queries. Deleting 50 tags causes 150+ round-trips in a serial loop.

#### Observation 1.2.4: Read-Only Queries Lacking `AsNoTracking()`
- **Files**:
  - `SRNSMudApp/Services/AdminDataProvider.cs` line 104 (`GetInvitationsAsync`)
  - `SRNSMudApp/Services/ContentReportService.cs` lines 164–168 (`GetReportsAsync`), line 187 (`GetReportByIdAsync`)
  - `SRNSMudApp/Services/ContractDataProvider.cs` lines 77–83, 86–92 (`GetContractsAsync`), line 100 (`GetAvailableRightAssetsAsync`), lines 111–115 (`SearchItemsAsync`), lines 124–128 (`SearchTagsByNameAsync`), line 136 (`GetActiveBountiesAsync`)
  - `SRNSMudApp/Services/TagCardDataProvider.cs` line 52 (`GetUserVoteRelationsAsync`)
  - `SRNSMudApp/Services/ItemListDataProvider.cs` line 176 (`FindTagByNameAsync`)
  - `SRNSMudApp/Services/TagSearchQueryService.cs` line 75 (`FindTagByNameAsync`)
  - `SRNSMudApp/Services/Providers/ItemLinkPreviewProvider.cs` lines 47–51 (`GetPreviewAsync`)

#### Observation 1.2.5: Non-Sargable String Query on JSON Column
- **File**: `SRNSMudApp/Services/NotificationsDataProvider.cs` (lines 95, 200–201, 211–212):
```csharp
!i.ItemKindJson.Contains("TagPermissionRequestPayload")
i.ItemKindJson.Contains("RequestedTagId")
```
- Translates to SQL `LIKE '%...%'` on an unbounded column, preventing index seeks and forcing table scans across the `Items` table.

#### Observation 1.2.6: Missing `CancellationToken` Propagation
- `IItemListDataProvider.LoadItemsByAncestorTagAsync`, `GetTagsByIdsAsync`, `GetTagsByNamesAsync`, `FindTagByNameAsync`
- `ITagCardDataProvider.GetUserVoteRelationsAsync`, `ToggleTagVoteAsync`, `AddTagToTagAsync`, `RemoveRelationAsync`, `UpdateRelationWeightAsync`, `SetRelationWeightAsync`
- `IAdminDataProvider.ImportItemsWithTagsAsync`, `GetInvitationsAsync`, `CreateInvitationAsync`, `DeleteInvitationAsync`
- `ITagTreeDataProvider.DeleteTagsAsync`
- `ITagRelationService.LinkTagToItemAsync`, `AllocateRightAssetToItemTagAsync`
- `ApplicationDbContextTagExtensions.CanUserAttachTagDirectlyAsync`, `CreateFreeTagRelationAsync`

---

### 1.3 Controller DTO Boundaries & Entity Leakage

#### Observation 1.3.1: Anonymous Public Push Broadcast Endpoint
- **File**: `SRNSMudApp/Controllers/PushNotificationController.cs` (lines 74–90):
```csharp
[HttpPost("send")]
public async Task<IActionResult> SendNotification([FromBody] PushNotificationPayload? payload, CancellationToken cancellationToken)
{
    if (payload == null || string.IsNullOrWhiteSpace(payload.Title) || string.IsNullOrWhiteSpace(payload.Body))
    {
        return BadRequest(new { message = "タイトルと本文は必須です。" });
    }

    var result = await _pushService.SendNotificationToAllAsync(payload, cancellationToken);
    return Ok(new { ... });
}
```
- Neither the endpoint nor the controller has `[Authorize]`. Anyone can trigger broadcast push notifications to all users.
- In line 50: `string? userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? subscription.UserId;` allows unauthenticated clients to register push tokens with any spoofed `subscription.UserId`.

#### Observation 1.3.2: Entity Leakage in DataProviders and ViewModels
- `IUserDataProvider`: `UserDetailPageData` exposes raw `ApplicationUser? User`, `IReadOnlyList<Tag> UserTags`, `IReadOnlyList<Item> UserItems`, `IReadOnlyList<ApplicationUser> FollowingUsers`. Exposes `IdentityUser` sensitive fields (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`).
- `IItemListDataProvider`: returns `IReadOnlyList<Item>`.
- `IAdminDataProvider`: returns `List<Invitation>`.
- `INotificationsDataProvider`: returns `NotificationRawData` holding raw EF entity lists.
- `IContentReportService`: returns `List<ContentReport>` and `ContentReport?`.
- Disconnected entity bug: `AdminDataProvider.DeleteInvitationAsync(invitation)` receives a detached entity from an earlier disposed DbContext and calls `context.Invitations!.Remove(invitation)` on a fresh context.

---

### 1.4 Domain Model Encapsulation & Data Layer Architecture

#### Observation 1.4.1: Anemic Entities and CA2227 Suppressions
- **Files**: `SRNSMudApp/Data/Item.cs`, `SRNSMudApp/Data/Tag.cs`, `SRNSMudApp/Data/TagRelation.cs`, `SRNSMudApp/Data/RightAsset.cs`.
- All collection properties suppress `CA2227: Collection properties should be read only` with public setters (`public ICollection<...> ... { get; set; } = [];`), allowing callers to replace collections and breaking EF Core relationship tracking.
- Entities are bags of getters/setters without invariants.

#### Observation 1.4.2: Repeated Deserialization in Entity Properties
- **Files**: `SRNSMudApp/Data/TaggingRequestEntity.cs` (lines 107–111, 127–134) and `SRNSMudApp/Data/RightAsset.cs` (lines 20–25).
```csharp
[NotMapped]
public ContractPayload Payload
{
    get => string.IsNullOrEmpty(ContractPayloadJson)
        ? new EmptyPayload()
        : JsonSerializer.Deserialize<ContractPayload>(ContractPayloadJson, PayloadJsonOptions);
    set => ContractPayloadJson = JsonSerializer.Serialize(value, PayloadJsonOptions);
}
```
Every property access deserializes JSON afresh; no internal memoization or EF value conversion is used.

#### Observation 1.4.3: Mutable HashCode in `BaseEntity`
- **File**: `SRNSMudApp/Data/BaseEntity.cs` (lines 51):
```csharp
public override int GetHashCode() => Id == 0 ? base.GetHashCode() : Id.GetHashCode();
```
Before insert, `Id == 0` (uses object identity hash). Once saved, `Id > 0` (uses `Id` hash). Storing transient entities in a `HashSet<BaseEntity>` or `Dictionary` key prior to save breaks lookup after save.

#### Observation 1.4.4: Redundant Duplicate Entity Configuration in `OnModelCreating`
- **File**: `SRNSMudApp/Data/ApplicationDbContext.cs` (lines 708–724, 728–744):
  `ConversationOptOut` fluent configuration and index are copy-pasted 3 times.

---

### 1.5 Clean Architecture & SOLID Boundaries

#### Observation 1.5.1: Clean Architecture Inversion (Services Importing UI/Components)
- **Files**:
  - `SRNSMudApp/Services/InternalLinkConversionService.cs` line 11: `using SRNSMudApp.Components.UI;` (calls `ItemCardViewModel.GetContentSegments(content)`).
  - `SRNSMudApp/Services/ItemListExportService.cs` line 6: `using SRNSMudApp.Components.UI;` (calls `ItemCardViewModel.ExtractUrls(content)`).
  - `SRNSMudApp/Services/HomeDataProvider.cs` line 5: `using SRNSMudApp.Components.Pages;` (returns `HomeTimelinePage(IReadOnlyList<TimelineFeedGroup> Groups, ...)` where `TimelineFeedGroup` is defined in `Components/Pages/HomeModels.cs`).
  - `SRNSMudApp/Services/SystemTagEnsurer.cs` line 1: `using SRNSMudApp.Components.UI;` (returns `SystemTagIds`, `ReactionTagIds` defined in `Components.UI`).
  - `SRNSMudApp/Services/ItemCardSplitCoordinator.cs` line 7: `using SRNSMudApp.Components.UI;`, `ISnackbar`, `IJSRuntime`.
  - `SRNSMudApp/Services/ItemCardTagCoordinator.cs` lines 3–4: `using SRNSMudApp.Components.Contract;`, `using SRNSMudApp.Components.Tag;`.
  - `SRNSMudApp/Services/ItemCardVoteCoordinator.cs` line 1: `using MudBlazor;`.

#### Observation 1.5.2: SRP Violation in DataProviders
- `TagCardDataProvider` (lines 57–126), `RightAssetDataProvider` (lines 400–520), and `AdminDataProvider` (lines 31–99) perform complex transactional write operations (minting assets, transferring balances, mutating relation weights, logging timeline events), violating the Provider (Read/Query) contract and duplicating domain logic found in `TagRelationService` and `RightAssetPurchaseService`.

---

## 2. Logic Chain

```
[Observation 1.1.1] 
ExecutionStrategyExtensions claims "transactions managed internally". EF Core docs state ExecuteAsync only retries delegates; it does NOT open transactions.
   ↓
Multiple SaveChangesAsync calls run in independent auto-transactions.
   ↓
If Save 2 or 3 fails, Save 1 is already committed. If a transient error triggers a retry, the delegate executes from the start, re-inserting already-committed rows.
   ↓
[Conclusion: CRITICAL] Silent data corruption, broken atomicity, and primary key/duplicate errors on retries across 5 core services.

[Observation 1.1.2]
RightAssetPurchaseService saves RightAsset in SaveChangesAsync #1, then saves JpycDepositTransaction in SaveChangesAsync #2 without a transaction.
   ↓
If Save #2 fails or is cancelled, RightAsset is committed while JpycDepositTransaction remains null/unconfirmed.
   ↓
Anti-replay check only checks JpycDepositTransaction.Status == Confirmed.
   ↓
[Conclusion: CRITICAL] User can re-submit the same transaction hash and mint duplicate assets (Double-Spending).

[Observation 1.3.1]
PushNotificationController [HttpPost("send")] lacks [Authorize].
   ↓
Any unauthenticated HTTP client on the internet can POST to /api/PushNotification/send.
   ↓
[Conclusion: CRITICAL] Malicious users can send arbitrary push spam to all registered application users.

[Observation 1.1.3]
UserManager<ApplicationUser> is registered Scoped via AddIdentityCore and injected into Blazor circuit-scoped ViewModels.
   ↓
Blazor Server circuit keeps Scoped services alive across user interactions; EF Core DbContext is strictly single-threaded.
   ↓
Concurrent user interactions or background refreshes share the single circuit-scoped DbContext.
   ↓
[Conclusion: HIGH] Concurrency exceptions (InvalidOperationException) during concurrent operations.

[Observation 1.2.1]
NotificationService.GetUnreadCountAsync calls GetUserNotificationsAsync -> NotificationsDataProvider.GetNotificationRawDataAsync.
   ↓
15 sequential SQL queries with eager includes across 8 tables are executed on every page/badge render.
   ↓
[Conclusion: HIGH] Massive server and database workload for a simple badge number.

[Observation 1.2.2]
Tag autocomplete/suggestion methods query all tags with embeddings into memory.
   ↓
Entire table deserialized into float[] arrays over network on every keystroke.
   ↓
[Conclusion: HIGH] Memory explosion, high GC pressure, and database bottleneck as tag count grows.

[Observation 1.5.1]
Services in Services/ import Components.UI and Components.Pages.
   ↓
Domain services depend directly on UI ViewModels and MudBlazor components.
   ↓
[Conclusion: HIGH] Violation of Clean Architecture Dependency Inversion Principle.
```

---

## 3. Categorized Findings & Recommendations

| ID | Severity | Category | Target File(s) & Lines | Description / Pattern Violated | Concrete Fix Recommendation |
|---|---|---|---|---|---|
| **SEC-01** | **Critical** | Security / Controller | `Controllers/PushNotificationController.cs:74-90` | **Broken Access Control**: `[HttpPost("send")]` lacks `[Authorize]`. Anonymous clients can broadcast push notifications to all users. | Add `[Authorize(Roles = "Admin")]` to `[HttpPost("send")]`. In `Subscribe`, enforce `userId` from authenticated claims. |
| **DATA-01** | **Critical** | Data Access / Concurrency | `Data/ExecutionStrategyExtensions.cs:20-52`, `Data/ApplicationDbContextTagExtensions.cs:93-165`, `Services/ItemTagService.cs:96-117`, `Services/TagEdgeService.cs:145-187`, `Services/TagRelationService.cs:38-80, 140-191`, `Services/TagTreeDataProvider.cs:66-174` | **False Atomicity / Broken Transaction Boundary**: `ExecuteWithStrategyAsync` does not begin transactions. Multiple `SaveChangesAsync` inside delegate lead to partial commits and duplicate insertions upon retry. | Open explicit transactions inside `ExecuteWithStrategyAsync`: `await using var tx = await context.Database.BeginTransactionAsync(); ... await tx.CommitAsync();` or consolidate into a single `SaveChangesAsync`. |
| **DATA-02** | **Critical** | Data Access / Financial | `Services/RightAssetPurchaseService.cs:243-286` | **Double-Spending / Partial Commit**: `RightAsset` is committed in SaveChanges #1 before `JpycDepositTransaction` is committed in SaveChanges #2 without a transaction. | Enclose both operations in a single `BeginTransactionAsync` transaction scope, or perform both `Add` calls before a single `SaveChangesAsync`. |
| **ARCH-01** | **High** | Clean Architecture / SOLID | `Services/InternalLinkConversionService.cs:11`, `Services/ItemListExportService.cs:6`, `Services/HomeDataProvider.cs:5`, `Services/SystemTagEnsurer.cs:1`, `Services/ItemCardSplitCoordinator.cs:7`, `Services/ItemCardTagCoordinator.cs:3-4` | **Dependency Inversion Violation**: Domain services in `Services/` directly import and depend on `Components.UI` and `Components.Pages`. | Move shared models (`TimelineFeedGroup`, `SystemTagIds`) to `Models/`. Move text parsing utilities to a domain helper (`ContentParser.cs`). Relocate UI coordinators (`*Coordinator`) to presentation layer. |
| **PERF-01** | **High** | Query Efficiency | `Services/NotificationsDataProvider.cs:67-240`, `Services/NotificationService.cs:50-54` | **Query Avalanche**: 15 sequential SQL queries with eager includes are run to count unread notifications on every badge render. | Add a dedicated `GetUnreadCountAsync` method in `INotificationsDataProvider` that executes an efficient SQL `COUNT` query across read states. |
| **PERF-02** | **High** | Query Efficiency / Memory | `Services/ItemListDataProvider.cs:196-205`, `Services/TagSuggestionService.cs:55-58`, `Services/TagHierarchyService.cs:55-60`, `Services/TagSearchQueryService.cs:56-62` | **Full Table In-Memory Vector Scan**: Loads all tags and 384-element float embeddings into RAM on every search keystroke. | Cache tag embeddings in an in-memory index service (`ITagEmbeddingIndexService` using `IMemoryCache` / singleton), and push predicates (`GetLevel() >= 2`) down to SQL. |
| **PERF-03** | **High** | Query Efficiency | `Services/TagTreeDataProvider.cs:83-104`, `Services/TagLockService.cs:105-151` | **N+1 Database Connections**: Deleting N tags calls `IsTagOrSiblingLockedAsync` in a loop, spawning N fresh contexts and 3N queries. | Implement a batched method `GetLockedTagIdsAsync(IEnumerable<int> tagIds)` in `ITagLockService` executing a single `WHERE Id IN (...)` query. |
| **DATA-03** | **High** | Concurrency / Thread-Safety | `Program.cs:127-140, 163-166`, `Components/User/UserDetailViewModel.cs:16-24` | **Blazor Circuit DbContext Concurrency**: `UserManager` uses the circuit-scoped `ApplicationDbContext` directly in ViewModels. | Use `IServiceScopeFactory.CreateAsyncScope()` or inject an abstraction that accesses `UserManager` within short-lived scopes (matching `IdentityRevalidatingAuthenticationStateProvider`). |
| **DATA-04** | **High** | EF Core / Async | `Data/Interceptors/ApplicationDbSaveChangesInterceptor.cs:37-54, 56-78, 198-245` | **Sync-over-Async in SaveChangesAsync**: Interceptor invokes synchronous queries (`context.Tags.Where(...).FirstOrDefault()`, `context.TaggableTargets.FirstOrDefault()`) inside `SavingChangesAsync`. | Implement async equivalents (`ValidateRootTagConstraintAsync`, etc.) and invoke them inside `SavingChangesAsync`. Inject `TimeProvider` properly in `ApplicationDbContext.OnConfiguring`. |
| **SEC-02** | **Medium** | DTO Boundary / Security | `Services/UserDataProvider.cs:20-29, 56-59`, `Components/User/UserDetailViewModel.cs:31-40` | **Identity Entity Leakage**: `ApplicationUser` (containing password hashes, security stamps, tokens) is returned to ViewModels and UI. | Map `ApplicationUser` to a sanitized `UserProfileDto` or `UserSummaryDto` containing only safe public attributes. |
| **PERF-04** | **Medium** | Query Efficiency | Multiple providers (`AdminDataProvider:104`, `ContentReportService:164, 187`, `ContractDataProvider:77, 86, 100, 111, 124, 136`, `TagCardDataProvider:52`, `ItemListDataProvider:176`, `TagSearchQueryService:75`) | **Missing AsNoTracking() on Read-Only Queries**: Read queries in ephemeral DbContexts track entities that are discarded immediately. | Append `.AsNoTracking()` to all read-only LINQ queries across providers. |
| **PERF-05** | **Medium** | Query Efficiency | `Services/NotificationsDataProvider.cs:95, 200-201, 211-212` | **Non-Sargable Query**: Uses `ItemKindJson.Contains(...)` causing SQL `LIKE '%...%'` full table scans on `Items`. | Map first-class relational columns or discriminator types instead of inspecting JSON text in SQL WHERE clauses. |
| **API-01** | **Medium** | API Design | `Controllers/AuthController.cs`, `Controllers/PushNotificationController.cs` | **Inconsistent Response Formats**: Controllers return a mix of raw strings, arbitrary anonymous objects, and inconsistent HTTP status codes without RFC 7807 `ProblemDetails`. | Standardize API responses using `ProblemDetails` for errors and typed DTO envelopes (`ApiResponse<T>`) for successes. |
| **DATA-05** | **Medium** | Domain Encapsulation | `Data/Item.cs:20-56`, `Data/Tag.cs:59-96`, `Data/TagRelation.cs:8-36`, `Data/RightAsset.cs:12-25` | **CA2227 Suppressions / Anemic Collections**: Navigation collections have public setters (`{ get; set; } = []`), allowing external overwrite of collection instances. | Make collection properties read-only (`{ get; init; }` or private setter) and expose encapsulated mutation methods on aggregates. |
| **DATA-06** | **Medium** | Entity Equality | `Data/BaseEntity.cs:51` | **Mutable GetHashCode Anti-Pattern**: Hash code shifts from `base.GetHashCode()` to `Id.GetHashCode()` after database persistence. | Implement a stable hash code strategy or guideline to avoid using transient entities as hash keys prior to persistence. |
| **DATA-07** | **Low** | Code Quality | `Data/ApplicationDbContext.cs:708-724, 728-744` | **Copy-Paste Duplication**: `ConversationOptOut` fluent configuration is duplicated 3 times in `OnModelCreating`. | Delete duplicate configuration blocks in `OnModelCreating`. |
| **PERF-06** | **Low** | Resource Management | `IItemListDataProvider`, `ITagCardDataProvider`, `IAdminDataProvider`, `ITagTreeDataProvider` | **Missing CancellationToken**: Database operations do not accept cancellation tokens, keeping orphaned queries running on client cancel. | Add `CancellationToken cancellationToken = default` across all public DataProvider interfaces and propagate to EF Core calls. |

---

## 4. Caveats

1. **Test Environment Differences**: In unit tests, `DbContextFactoryStub` often configures In-Memory or SQLite databases, which do not reproduce SQL Server execution strategy retry behavior or table lock concurrency. Concurrency and transaction isolation issues can only be reproduced on SQL Server instances.
2. **Third-Party Identity Store Constraints**: `AddIdentityCore` internally assumes `UserStore<ApplicationUser>` accesses `ApplicationDbContext`. Migrating Identity entirely away from EF Core scoped DbContext requires custom Identity stores or scoped service isolation wrappers.
3. **No Migration History Alteration**: All entity recommendations preserve existing database table schemas, column types, and foreign key constraints to ensure backward compatibility with existing migrations.

---

## 5. Conclusion

The application demonstrates strong architectural intent through the adoption of `IDbContextFactory<ApplicationDbContext>`, MVVM separation, and MudBlazor components. However, several critical vulnerabilities and design flaws undermine system reliability:
1. **Transaction Integrity**: The misunderstanding of `IExecutionStrategy` in `ExecutionStrategyExtensions` has created an illusion of transaction safety across 5 critical service areas where atomic multi-step mutations actually run without transactions.
2. **Security**: `PushNotificationController.cs` has an unauthenticated global broadcast vulnerability that requires immediate remediation with `[Authorize(Roles = "Admin")]`.
3. **Layer Separation**: The domain/service layer has leaked dependencies upward into the UI (`SRNSMudApp.Components`), and DataProviders have absorbed heavy business mutation logic while returning raw database entities (including sensitive Identity properties).
4. **Performance Scalability**: Key user paths (unread badge counters and tag search autocomplete) suffer from N+1 query proliferation and whole-table in-memory loading that will degrade as data volume grows.

Implementing the concrete fixes outlined in the findings table will restore Clean Architecture boundaries, ensure transactional atomicity, prevent double-spending, and protect the system against concurrency and security failures.

---

## 6. Verification Method

To independently verify these findings:

1. **Verify Transaction Omission**:
   Inspect `SRNSMudApp/Data/ApplicationDbContextTagExtensions.cs` lines 93–165. Observe that `context.Database.ExecuteWithStrategyAsync` executes three consecutive `SaveChangesAsync()` calls with no `BeginTransactionAsync()`. Induce a simulated exception before the third save to verify that the first two saves commit permanently to the database without rollback.
2. **Verify Push Notification Authorization Gap**:
   Inspect `SRNSMudApp/Controllers/PushNotificationController.cs` line 74. Confirm absence of `[Authorize]`. Send an unauthenticated HTTP POST request to `/api/PushNotification/send` with a dummy payload; observe `200 OK` response.
3. **Verify Clean Architecture Violations**:
   Run grep for UI namespace imports in Services:
   ```bash
   git grep "using SRNSMudApp.Components" SRNSMudApp/Services
   ```
   Confirm occurrences in `InternalLinkConversionService.cs`, `ItemListExportService.cs`, `HomeDataProvider.cs`, `SystemTagEnsurer.cs`, `ItemCardSplitCoordinator.cs`, and `ItemCardTagCoordinator.cs`.
4. **Verify Notification Avalanche**:
   Inspect `SRNSMudApp/Services/NotificationService.cs:50-54` and trace into `NotificationsDataProvider.cs:67-240`. Count the 15 distinct `await context.*.ToListAsync()` query invocations triggered by a single `GetUnreadCountAsync` call.
5. **Verify Missing AsNoTracking**:
   Inspect `SRNSMudApp/Services/AdminDataProvider.cs:104` and `SRNSMudApp/Services/ContentReportService.cs:164-168` to verify omission of `.AsNoTracking()`.
