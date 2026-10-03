# Survey Dispatch: Explorer 2 (Blazor Components, ViewModels, Lifecycle, Leaks)

## Objective
Conduct an in-depth audit of Blazor Components, ViewModels, UI architecture, lifecycle management, and event/resource disposal across SRNSWebApp.

## Scope & Target Areas
1. `Components/` - Pages, Layouts, Shared components, Dialogs.
2. `ViewModels/` - ViewModel classes, state management, event subscriptions.
3. Resource & Event Lifecycle:
   - Check implementation of `IAsyncDisposable` / `IDisposable`.
   - Detect event handler leaks: navigation events (`NavigationManager.LocationChanged`), custom event handlers, timers (`PeriodicTimer`, `System.Timers.Timer`) that lack unsubscription or disposal.
4. Architectural & Rule Violations:
   - Check if components directly access `DbContext` or database (violates `AGENTS.md`).
   - Check if components directly inject/use `IDialogService` instead of designated service/launcher wrappers (violates `AGENTS.md`).
   - Check MVVM separation: is presentation logic separated from component UI?
5. Compliance with project rules in `.agents/rules/mainRules.md`, `AGENTS.md`, and `.agents/skills/dotnet-design-pattern-review/SKILL.md`.

## Deliverables
Write your comprehensive audit findings to:
`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_survey_2/handoff.md`

Categorize every finding with:
- Severity: Critical, High, Medium, Low
- File and line references
- Design pattern or architectural principle violated
- Concrete recommendation / fix strategy

## Mandatory Reading
- `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/AGENTS.md`
- `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/rules/mainRules.md`
- `/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/skills/dotnet-design-pattern-review/SKILL.md`


## 2026-10-02T13:52:07Z
Task received from parent (077cc920-cc9e-40bc-99e6-163a40d89fa9):
Conduct an in-depth audit of Blazor Components, ViewModels, UI architecture, lifecycle management, and event/resource disposal across SRNSWebApp.
Examine Components/ (Pages, Layouts, Shared, Dialogs), ViewModels/, and state management.
Check specifically for:
- Event handler leaks (NavigationManager.LocationChanged, CancellationTokenSource, Timer, event subscriptions without unsubscription/disposal)
- Missing IDisposable / IAsyncDisposable
- Violations of AGENTS.md (e.g., components directly querying DbContext or directly using IDialogService instead of launchers)
- MVVM separation issues
Categorize findings by severity (Critical, High, Medium, Low), citing file and line references, pattern violated, and proposed fix.
Write complete report to handoff.md.
