# Progress Report — Explorer 2 (Blazor Components, ViewModels, Lifecycle & Disposal)

Last visited: 2026-10-02T14:20:00Z
Status: Complete

## Current Activity
- Completed comprehensive static audit of Components/, ViewModels/, Services/ across SRNSWebApp.
- Synthesizing findings into structured severity tiers (Critical, High, Medium, Low).
- Drafting handoff.md with 5-component protocol: Observation, Logic Chain, Caveats, Conclusion, Verification Method.

## Findings Summary
1. **Unmanaged Resource / JS Object Reference Leak**:
   - `TagHierarchyService.cs:105`: `IJSObjectReference module` imported via `jsRuntime.InvokeAsync<IJSObjectReference>("import", ...)` is never disposed via `DisposeAsync()`.
2. **Orphaned / Unwired ViewModels (Dead MVVM Layers)**:
   - `ReactionCommentViewModel`: Registered in DI, fully implemented and unit-tested, but `ReactionCommentDialog.razor.cs` duplicates its logic and does not use the ViewModel.
   - `TagDiagramCanvasViewModel`: Registered in DI, implemented and tested, but `TagDiagramCanvas.razor` does not use it.
   - `CreateEdgeViewModel`: Implemented and tested, but not registered in DI and ignored by `CreateEdgeDialog.razor.cs`.
3. **DI Lifetime Inconsistencies for Dialog ViewModels (Circuit State Leaks)**:
   - Over 15 Dialog ViewModels (`TagAddViewModel`, `TagEditViewModel`, `ItemEditViewModel`, `ProposeContractViewModel`, `PurchaseRightAssetViewModel`, `ReportContentViewModel`, `UserGroupCreateEditViewModel`, `BountyCreateViewModel`, `FulfillBountyViewModel`, `TriggerPublicOfferViewModel`, `CreatePublicOfferViewModel`, `TagResolutionViewModel`, `TagLinkReplaceViewModel`, `TagNameProposalViewModel`, `TagContentProposalViewModel`, `AttachTagToEdgeViewModel`, `UserSearchViewModel`, `UserGroupMembersViewModel`, `ReportDetailActionViewModel`) are registered as `Scoped` instead of `Transient`. In Blazor InteractiveServer, `Scoped` is per-circuit, causing previous dialog inputs, selections, and validation states to persist across dialog sessions.
4. **Clean Architecture / Inverted Layer Dependencies**:
   - `TaggingRequestActions.cs:95` in `Services/` directly references and launches `Components.UI.RejectRequestDialog`.
   - `ItemCardSplitCoordinator.cs` and `ItemCardTagCoordinator.cs` in `Services/` directly inject MudBlazor UI types (`IDialogLauncher`, `ISnackbar`, `IJSRuntime`).
   - `InternalLinkConversionService.cs`, `TagDiagramDataProvider.cs`, and `ItemListExportService.cs` in `Services/` call static methods on `ItemCardViewModel` in `Components/UI/` (`InternalLinkRegex`, `GetContentSegments`, `ExtractUrls`).
   - Domain records `SystemTagIds` and `ReactionTagIds` defined inside `Components/UI/ResourceListViewModel.cs` forcing domain service `ISystemTagEnsurer` to import `Components.UI`.
5. **MVVM Purity & UI Coupling in ViewModels**:
   - Multiple ViewModels (`UserManagementViewModel`, `TagAddViewModel`, `ReportDetailActionViewModel`, `ItemCardActionViewModel`, `PushNotificationPromptViewModel`, `RequestTagPermissionViewModel`) directly inject and call `ISnackbar`.
   - `MakeMeAdminViewModel.cs:49` invokes JS `eval` to click a DOM button.
   - `RightAssetOverviewViewModel.cs:122` and `ImportTaggingViewModel.cs:167,253` directly launch concrete dialog components via `IDialogLauncher`.
   - `UserGroupListViewModel.cs` and `BountyBoardViewModel.cs` construct MudBlazor `DialogParameters<T>` referencing concrete dialog component types.
6. **Component Lifecycle & Disposal Details**:
   - `NotificationBadge.razor.cs` does not dispose its injected `NotificationBadgeViewModel` (which listens to `_notificationService.NotificationsChanged`).
   - `ResourceList.razor.cs` implements `IAsyncDisposable` with an empty no-op.
   - Duplicate `<MudThemeProvider/>` in `MainLayout.razor` and `MudProviders.razor`.
