# Comprehensive Blazor UI, ViewModels, Lifecycle & Resource Disposal Audit Report

**Survey Agent**: teamwork_preview_explorer_survey_2  
**Date**: 2026-10-02  
**Target Solution**: SRNSWebApp (`SRNSMudApp`, `SRNSMudApp.Client`)  
**Scope**: `Components/` (Pages, Layouts, Shared, Dialogs, UI), `ViewModels/`, Lifecycle Management, Event/Resource Disposal, MVVM Architecture, and `AGENTS.md` Rule Compliance.

---

## 1. Observation

Direct observations from source code inspection and static analysis across the solution:

### 1.1 Unmanaged Resource & JS Object Reference Leak
- **File**: `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/SRNSMudApp/Services/TagHierarchyService.cs`
- **Lines 105–118**:
  ```csharp
  IJSObjectReference module = await jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", cancellationToken, "./js/tagHierarchy.js");

  List<string> promptCandidates = [newTagName, .. candidateNames];
  JsonElement result = await module.InvokeAsync<JsonElement>(
      "determineHierarchyLocal", cancellationToken, promptCandidates);

  Tag? matched = FindMatchedTagFromLlmResult(result, candidateTags, newTagName);
  if (matched != null)
  {
      return matched;
  }
  ```
  `module` is an `IJSObjectReference` imported on each execution of `SuggestParentTagAsync`. It is never disposed (`await module.DisposeAsync()` is never called, nor is `await using` used). In addition, `module` is not cleaned up if an exception occurs or when falling back.

### 1.2 DI Lifetime Inconsistencies for Dialog ViewModels (Circuit State Pollution)
- **File**: `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/SRNSMudApp/Extensions/ServiceCollectionExtensions.cs`
- **Lines 176–245**: Over 20 ViewModels are registered with `AddScoped`, whereas other ViewModels are registered with `AddTransient`:
  - **Registered as `Scoped` (Lines 176–220)**:
    - Dialog ViewModels: `TagAddViewModel` (193), `TagEditViewModel` (206), `ItemEditViewModel` (180), `ProposeContractViewModel` (190), `PurchaseRightAssetViewModel` (207), `ReportContentViewModel` (210), `UserGroupCreateEditViewModel` (211), `BountyCreateViewModel` (213), `FulfillBountyViewModel` (214), `TriggerPublicOfferViewModel` (199), `CreatePublicOfferViewModel` (200), `QuoteItemViewModel` (202), `TagResolutionViewModel` (204), `TagLinkReplaceViewModel` (205), `TagNameProposalViewModel` (209), `TagContentProposalViewModel` (208), `AttachTagToEdgeViewModel` (216), `UserSearchViewModel` (195), `UserGroupMembersViewModel` (212), `ReportDetailActionViewModel` (196).
    - Page ViewModels: `ItemDetailViewModel` (176), `TagDetailViewModel` (197).
  - **Registered as `Transient` (Lines 221–245)**:
    - `AddTagPageViewModel` (224), `ImportTagViewModel` (225), `GenericTagEditorViewModel` (226), `ImportTaggingViewModel` (227), `TagListViewModel` (228), `TagSearchPageViewModel` (229), `HomeViewModel` (233), `TagAutocompleteViewModel` (236), `ItemListViewModel` (239), `TagTreeViewModel` (241), `NotificationsViewModel` (242), `TagDiagramPageViewModel` (243), `NavMenuViewModel` (244).
- In `TagAddViewModel.cs:33–42`:
  ```csharp
  public IReadOnlyList<Data.Tag> AllTags { get; private set; } = [];
  public string SearchText { get; set; } = string.Empty;
  public IReadOnlyList<Data.Tag>? SearchResults { get; private set; }
  public bool IsSearching { get; private set; }
  public Data.Tag? SelectedTag { get; set; }
  public Data.Tag? ParentTag { get; set; }
  public string NewTagName { get; set; } = string.Empty;
  public string? NewTagContent { get; set; }
  ```
  `InitializeAsync()` (line 48) only reloads `AllTags`, leaving `SearchText`, `SearchResults`, `SelectedTag`, `NewTagName`, `NewTagContent` with previous values across dialog open/close sessions within the same Blazor circuit.

### 1.3 Orphaned / Disconnected ViewModels
1. **`ReactionCommentViewModel`**:
   - Registered in `ServiceCollectionExtensions.cs:221` (`AddTransient<ReactionCommentViewModel>()`).
   - Thoroughly tested in `SRNSMudApp.Tests/Components/UI/ReactionCommentViewModelTests.cs`.
   - **`ReactionCommentDialog.razor.cs:17–66`**: Does NOT inject `ReactionCommentViewModel`. Instead, it reimplements its own countdown timer with `_remainingSeconds = 10`, `_isCursorHovered`, `_cts = new CancellationTokenSource()`, and `StartCountdownAsync()`.
2. **`TagDiagramCanvasViewModel`**:
   - Registered in `ServiceCollectionExtensions.cs:191` (`AddScoped<TagDiagramCanvasViewModel>()`).
   - Tested in `SRNSMudApp.Tests/Components/Diagram/TagDiagramCanvasViewModelTests.cs`.
   - **`TagDiagramCanvas.razor:18–64`**: Does NOT inject `TagDiagramCanvasViewModel`. It operates purely via parameters and event callbacks.
3. **`CreateEdgeViewModel`**:
   - Implemented in `SRNSMudApp/Components/Diagram/CreateEdgeViewModel.cs`.
   - Tested in `SRNSMudApp.Tests/Components/Diagram/CreateEdgeViewModelTests.cs`.
   - Never registered in `ServiceCollectionExtensions.cs` and NOT injected in `CreateEdgeDialog.razor.cs:19–85` (which duplicates tag filtering directly).

### 1.4 Clean Architecture Layer Inversion (Services depending on UI Components)
1. **`TaggingRequestActions.cs:95`** (in `Services/`):
   ```csharp
   IDialogReference dialog = await _dialogLauncher.ShowAsync<Components.UI.RejectRequestDialog>("リクエストを却下", options);
   ```
   A domain service in `Services/` directly depends on and launches a Razor component `Components.UI.RejectRequestDialog`.
2. **`ItemCardSplitCoordinator.cs:22–25`** and **`ItemCardTagCoordinator.cs:16–18`** (in `Services/`):
   Inject `IDialogLauncher`, `ISnackbar`, `IJSRuntime` and launch `TagAddDialog`.
3. **Services calling UI ViewModels**:
   - `InternalLinkConversionService.cs:59`: calls `ItemCardViewModel.GetContentSegments(content)`
   - `TagDiagramDataProvider.cs:39, 91`: calls `ItemCardViewModel.InternalLinkRegex()`
   - `ItemListExportService.cs:174`: calls `ItemCardViewModel.ExtractUrls(content)`
4. **Domain Types declared in UI ViewModels**:
   - `SystemTagIds` and `ReactionTagIds` are declared in `/Components/UI/ResourceListViewModel.cs:11, 18`.
   - `ISystemTagEnsurer` (in `Services/SystemTagEnsurer.cs:1`) is forced to include `using SRNSMudApp.Components.UI;`.

### 1.5 MVVM Coupling & Presentation Leakage into ViewModels
1. **Direct `ISnackbar` injection into ViewModels**:
   - `RequestTagPermissionViewModel.cs:24`
   - `UserManagementViewModel.cs:25`
   - `TagAddViewModel.cs:19`
   - `ReportDetailActionViewModel.cs:17`
   - `ItemCardActionViewModel.cs:27`
   - `PushNotificationPromptViewModel.cs:17`
2. **Direct JS `eval` DOM execution in ViewModel**:
   - `MakeMeAdminViewModel.cs:49`:
     ```csharp
     await jsRuntime.InvokeVoidAsync("eval", "document.getElementById('toggleAdminSubmitBtn').click()");
     ```
3. **ViewModels launching Dialogs / referencing Razor Dialog types**:
   - `RightAssetOverviewViewModel.cs:122`: `await _dialogLauncher.ShowAsync<PurchaseRightAssetDialog>(...)`
   - `ImportTaggingViewModel.cs:167, 253`: `await _dialogLauncher.ShowAsync<TagResolutionDialog>(...)`, `ShowAsync<TagLinkReplaceDialog>(...)`
   - `UserGroupListViewModel.cs:87, 98, 111`: creates `DialogParameters` using `nameof(UserGroupCreateEditDialog.CurrentUserId)`, etc.
   - `BountyBoardViewModel.cs:89`: defines method returning `DialogParameters<FulfillBountyDialog>`.

### 1.6 Component Lifecycle & Disposal Observations
1. **`NotificationBadge.razor.cs:32–39`**:
   `Dispose` unsubscribes `NavigationManager.LocationChanged` and `ViewModel.StateChanged`, but does not call `ViewModel.Dispose()`. Because `NotificationBadgeViewModel` is `Scoped`, its subscription `_notificationService.NotificationsChanged += OnNotificationsChanged` remains alive in the circuit even if the component is disposed.
2. **`ResourceList.razor.cs:159–163`**:
   Implements `IAsyncDisposable`, but `DisposeAsync()` is a no-op returning `ValueTask.CompletedTask`.
3. **`MainLayout.razor:4–5` vs `MudProviders.razor:1`**:
   `<MudThemeProvider/>` is rendered in `MainLayout.razor` line 4, and again inside `MudProviders.razor` line 1.
4. **`Home.razor:30–31`**:
   `JsonSerializer.Deserialize<TimelineTarget>(group.TimelineTargetJson)` is executed inside the render loop of `<Virtualize>` for every item on every render pass.
5. **`Home.razor:26`**:
   `ItemComparer="new TimelineFeedGroupComparer()"` allocates a new comparer instance on each component render.
6. **`ItemEditDialog.razor.cs:58`**:
   `await JS.InvokeVoidAsync("eval", $"document.getElementById('edit-item-textarea').innerHTML = {JsonSerializer.Serialize(initialHtml)}");` uses JS `eval` to assign HTML.

### 1.7 AGENTS.md Rule Compliance Verification
- **Direct DbContext access in `Components/`**: **0 violations**. All data access in components is mediated via `IDataProvider` abstractions or ViewModels.
- **Direct `IDialogService` usage in `Components/`**: **0 violations**. Components consistently use `IDialogLauncher`.
- **`LocationChanged` and `DotNetObjectReference`**: Components subscribing to `LocationChanged` (`NavMenu`, `NotificationBadge`) and components creating `DotNetObjectReference` (`ItemCard`, `TagCard`, `ItemCardContent`, `TagTree`, `AddItem`, `UserDetail`, `ItemEditDialog`) implement `IDisposable` or `IAsyncDisposable` and properly release their handlers and references.

---

## 2. Logic Chain

```
[Observation 1.1] TagHierarchyService imports "./js/tagHierarchy.js" as IJSObjectReference without disposing it
        ↓
[Reasoning 1] In Blazor, IJSObjectReference holds a proxy handle across the SignalR circuit to a JavaScript object in the browser runtime. Failure to dispose it prevents GC on both .NET and JS sides.
        ↓
[Conclusion 1] Memory leak in TagHierarchyService when generating parent tag suggestions.

------------------------------------------------------------------------------------------------------

[Observation 1.2] 20+ Dialog ViewModels are registered as Scoped in ServiceCollectionExtensions.cs
        ↓
[Reasoning 2] In ASP.NET Core Blazor InteractiveServer, a Scoped service has the lifetime of the Blazor Circuit (the user session). Dialogs are transient UI elements. When a user opens, interacts with, and closes a dialog, a Scoped ViewModel retains its mutated properties (e.g., input strings, selected items, validation flags). When the dialog is opened again, the same ViewModel instance is injected.
        ↓
[Conclusion 2] Circuit state pollution and stale state bugs across dialog sessions. ViewModels for dialogs must be Transient.

------------------------------------------------------------------------------------------------------

[Observation 1.3] ReactionCommentViewModel, TagDiagramCanvasViewModel, CreateEdgeViewModel exist with unit tests but are not used by their corresponding Razor components
        ↓
[Reasoning 3] Developers authored ViewModels and unit tests, but the corresponding Razor components retained legacy code or were never refactored to use the new ViewModels.
        ↓
[Conclusion 3] Dead code, maintenance hazard, and false confidence from unit tests testing code not running in production.

------------------------------------------------------------------------------------------------------

[Observation 1.4 & 1.5] Services/ classes reference Components/UI/ dialogs, snackbars, and ViewModels; ViewModels reference concrete Razor dialog types and ISnackbar
        ↓
[Reasoning 4] Clean Architecture requires dependencies to point inward: Views → ViewModels → Services → Core Models. When Services reference Components.UI, or when ViewModels reference concrete Razor Dialog types, circular dependencies occur, presentation details leak into business logic, and UI-independent unit testing is impaired.
        ↓
[Conclusion 4] Layer boundary violations across Services and Components requiring extraction of domain parsing to Services/Models and relocation of UI coordinators to the presentation layer.
```

---

## 3. Severity Categorization & Proposed Fixes

| ID | Issue | Severity | File(s) & Line(s) | Pattern Violated | Proposed Fix |
|---|---|---|---|---|---|
| **F-01** | `IJSObjectReference` module leak | **High** | `Services/TagHierarchyService.cs:105` | Resource Management / JS Interop Lifecycle | Use `await using IJSObjectReference module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/tagHierarchy.js");` |
| **F-02** | Dialog & Page ViewModels registered as `Scoped` causing circuit state pollution | **High** | `Extensions/ServiceCollectionExtensions.cs:176–220` | DI Lifetime Scoping / Circuit State Isolation | Change dialog ViewModels (`TagAddViewModel`, `TagEditViewModel`, `ItemEditViewModel`, `BountyCreateViewModel`, `FulfillBountyViewModel`, `UserGroupCreateEditViewModel`, `ReportDetailActionViewModel`, etc.) to `AddTransient<T>()`. |
| **F-03** | Orphaned ViewModels disconnected from Razor components | **High** | `Components/UI/ReactionCommentDialog.razor.cs:17`<br>`Components/Diagram/TagDiagramCanvas.razor:18`<br>`Components/Diagram/CreateEdgeDialog.razor.cs:19` | MVVM Architecture / Dead Code Elimination | 1. Wire `ReactionCommentViewModel` into `ReactionCommentDialog.razor.cs` and remove duplicated timer fields.<br>2. Wire `CreateEdgeViewModel` into `CreateEdgeDialog.razor.cs` and register in DI.<br>3. Wire or retire `TagDiagramCanvasViewModel`. |
| **F-04** | Clean Architecture layer inversion: Services depending on UI Components | **Medium** | `Services/TaggingRequestActions.cs:95`<br>`Services/ItemCardSplitCoordinator.cs:22`<br>`Services/ItemCardTagCoordinator.cs:16` | Clean Architecture / Dependency Inversion (DIP) | Move UI action coordinators (`ItemCardSplitCoordinator`, `ItemCardTagCoordinator`, `TaggingRequestActions`) from `Services/` to `Components/UI/` or `Presentation/Coordinators/`. |
| **F-05** | Domain utilities placed in UI ViewModels, forcing Services to import UI | **Medium** | `Services/InternalLinkConversionService.cs:59`<br>`Services/TagDiagramDataProvider.cs:39, 91`<br>`Services/ItemListExportService.cs:174`<br>`Components/UI/ResourceListViewModel.cs:11, 18` | Single Responsibility / Domain Separation | 1. Move `SystemTagIds` and `ReactionTagIds` to `Models/`.<br>2. Move `InternalLinkRegex`, `GetContentSegments`, and `ExtractUrls` from `ItemCardViewModel` to a domain text parsing service in `Services/` or `Models/`. |
| **F-06** | ViewModels injecting `ISnackbar` and launching Dialogs | **Medium** | `UserManagementViewModel.cs:25`<br>`TagAddViewModel.cs:19`<br>`ReportDetailActionViewModel.cs:17`<br>`RightAssetOverviewViewModel.cs:122`<br>`ImportTaggingViewModel.cs:167` | MVVM Presentation Purity | Have ViewModels return `Result<T>` or `TagCardActionResult`; let the Blazor component display `Snackbar` messages and trigger `DialogLauncher`. |
| **F-07** | JS `eval` used for DOM manipulation in ViewModel and Component | **Medium** | `Components/User/MakeMeAdminViewModel.cs:49`<br>`Components/Item/ItemEditDialog.razor.cs:58` | Security & JS Interop Best Practices | Replace `eval` with dedicated JS module functions or Blazor native events/form submissions. |
| **F-08** | Incomplete disposal of Scoped ViewModel from component | **Low** | `Components/UI/NotificationBadge.razor.cs:32–39` | Lifecycle Management | Invoke `ViewModel.Dispose()` or register `NotificationBadgeViewModel` as `Transient` with component-managed lifecycle. |
| **F-09** | Duplicate `<MudThemeProvider/>` rendering | **Low** | `Components/Layout/MainLayout.razor:4` vs `MudProviders.razor:1` | UI Component Best Practice | Remove `<MudThemeProvider/>` from `MainLayout.razor` since it is already rendered in `MudProviders.razor`. |
| **F-10** | In-render JSON deserialization and comparer allocation in Virtualize loop | **Low** | `Components/Pages/Home.razor:26, 30–31` | Performance / Allocation Optimization | Pre-deserialize `TimelineTarget` in `HomeViewModel.LoadTimelineAsync` and define `TimelineFeedGroupComparer` as a `static readonly` instance. |

---

## 4. Caveats

1. **Static Analysis Environment**: `dotnet build` and `dotnet test` could not be executed within this exploration step due to the standard read-only sandbox policy (`zsh: operation not permitted: dotnet`). All findings are established through comprehensive static code analysis, AST tracing, and cross-reference grep searches.
2. **Interactive vs Static Render Modes**: Parts of `SRNSMudApp` (e.g., `MainLayout.razor`) operate under static SSR, while nested components (`MudProviders`, `NotificationBadge`) specify `@rendermode="InteractiveServer"`. In static SSR, Blazor does not call `Dispose()` on components; however, because request scopes terminate with the HTTP response, request-scoped services are disposed by the DI container. The primary circuit leak risks occur in InteractiveServer components.
3. **Behavioral Compatibility**: When changing dialog ViewModels from `Scoped` to `Transient`, any existing tests that resolve multiple instances from the same scope expecting shared state should be reviewed; however, dialog ViewModels should never share state across dialog instances.

---

## 5. Conclusion

1. **AGENTS.md Compliance**: The solution strictly obeys the primary `AGENTS.md` guidelines for UI components: **no component directly accesses DbContext**, and **no component directly uses `IDialogService`** (all dialog launches use `IDialogLauncher`).
2. **Critical Leak Finding**: A genuine unmanaged resource leak was found in `TagHierarchyService.cs:105`, where an imported `IJSObjectReference` module is never disposed.
3. **Primary Architectural Defect (DI Lifetimes)**: Over 20 dialog and page ViewModels are incorrectly registered as `Scoped`. In Blazor InteractiveServer, this causes circuit-level state leaks where stale form inputs and search results pollute subsequent dialog openings.
4. **Primary Structural Defect (Orphaned ViewModels & Layer Inversion)**: Three ViewModels (`ReactionCommentViewModel`, `TagDiagramCanvasViewModel`, `CreateEdgeViewModel`) exist in isolation from their UI components, and the `Services/` layer has several upward couplings into `Components.UI` components and ViewModels.
5. **Action Plan for Implementer**:
   - Fix 1: Add `await using` to `module` in `TagHierarchyService.cs`.
   - Fix 2: Change dialog ViewModel registrations in `ServiceCollectionExtensions.cs` to `Transient`.
   - Fix 3: Wire `ReactionCommentViewModel` into `ReactionCommentDialog.razor.cs`.
   - Fix 4: Move `SystemTagIds` and `ReactionTagIds` to `Models/` and decouple `Services` from `Components.UI`.

---

## 6. Verification Method

To independently verify these findings:

1. **Verify `IJSObjectReference` leak in `TagHierarchyService.cs`**:
   - Inspect line 105 of `SRNSMudApp/Services/TagHierarchyService.cs`.
   - Check whether `await using` or `DisposeAsync()` is called on `module`. It is not.
2. **Verify Scoped vs Transient registrations**:
   - Inspect `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs` lines 176–245.
   - Note that dialog ViewModels such as `TagAddViewModel` (line 193) and `TagEditViewModel` (line 206) are in `AddScoped`, while `GenericTagEditorViewModel` (line 226) is in `AddTransient`.
3. **Verify Orphaned ViewModels**:
   - Inspect `SRNSMudApp/Components/UI/ReactionCommentDialog.razor.cs`.
   - Observe that `ReactionCommentViewModel` is never injected; line 26 declares `private CancellationTokenSource? _cts;` and lines 35–66 implement the countdown directly.
   - Inspect `SRNSMudApp.Tests/Components/UI/ReactionCommentViewModelTests.cs` to confirm tests are asserting on a ViewModel disconnected from the UI.
4. **Verify Layer Inversion**:
   - Inspect `SRNSMudApp/Services/TaggingRequestActions.cs:95` to observe `ShowAsync<Components.UI.RejectRequestDialog>`.
   - Inspect `SRNSMudApp/Services/InternalLinkConversionService.cs:59` to observe `ItemCardViewModel.GetContentSegments(content)`.
   - Inspect `SRNSMudApp/Services/TagDiagramDataProvider.cs:39` to observe `ItemCardViewModel.InternalLinkRegex()`.
