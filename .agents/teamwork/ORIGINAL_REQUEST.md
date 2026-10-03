# Original User Request

## 2026-10-02T13:49:32Z

Conduct a comprehensive .NET/C# design pattern review across the entire SRNSWebApp solution (Services, Data, ViewModels/Components, Controllers, Models). Identify critical architectural/design pattern flaws, implement fixes for high-impact/critical issues, and verify that the solution builds and passes tests.

Working directory: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp
Integrity mode: development

## Requirements

### R1. Comprehensive Design Pattern Review
Audit the codebase against .NET design pattern best practices:
- **Command & Strategy/Provider Patterns**: Separation of concerns, interface abstraction, handler implementations.
- **Dependency Injection & Lifetimes**: Proper constructor injection, service lifetime scopes, avoidance of service locator anti-patterns.
- **Repository / Data Access Patterns**: Data access decoupling, DbContext usage, query encapsulation.
- **Blazor Component & ViewModel Patterns**: Proper MVVM separation, lifecycle management, event/resource disposal (`IAsyncDisposable` / event unsubscription).
- **SOLID & Clean Architecture**: Violation detection across boundaries.

### R2. Fix High-Priority / Critical Issues
- For high-impact architectural violations, anti-patterns, memory leaks (e.g., event handler leaks in ViewModels), or severe DI misconfigurations, implement direct code refactorings/fixes.
- Preserve existing functionality and maintain public API stability.
- Adhere to project guidelines (`AGENTS.md`, `.agents/rules/mainRules.md`): use primary constructors where appropriate, async I/O, Japanese comments explaining design rationale.

### R3. Verification and Testing
- Run `dotnet build` to ensure zero compilation errors and no new warnings introduced.
- Run unit/integration tests via `dotnet test` to ensure existing behavior remains intact.

### R4. Consolidated Review & Refactoring Report
- Produce a clear markdown report documenting:
  1. Identified design patterns and architecture assessment.
  2. High-priority issues addressed (with files modified and rationale).
  3. Medium/Low priority recommendations and roadmap for future iterations.

## Acceptance Criteria

### Verification & Stability
- [ ] `dotnet build` succeeds with 0 errors.
- [ ] Existing tests (`dotnet test`) pass without regression.
- [ ] Any modified components or services properly manage resources and disposals.

### Deliverables
- [ ] High-priority design pattern fixes are directly implemented and verified.
- [ ] A comprehensive review report document is generated detailing findings and future improvements.

## 2026-10-02T14:44:19Z

User instruction update:
Please scope the implementation milestones to end at Milestone 2.
Do NOT proceed with code changes for Milestone 3 (Clean Architecture & Decoupling).
After completing Milestone 2 (DI Lifetimes, Resource Leaks & Interceptors) and its review gate, proceed directly to Milestone 4 / Final Verification & Consolidated Report (running dotnet build, dotnet test, and generating the comprehensive review report). Any Milestone 3 items should remain as recommendations in the final report rather than code changes.

## 2026-10-02T14:53:38Z

User instruction update:
STOP all code changes after Milestone 1.
Do NOT proceed with Milestone 2 code changes.
As soon as Milestone 1 (including its current review gate/fixes) is completed and verified, proceed directly to Milestone 4 / Final Verification & Consolidated Report:
1. Ensure solution builds (`dotnet build`) with 0 errors and all tests (`dotnet test`) pass.
2. Produce the comprehensive design pattern review report (documenting findings across all layers, details of Milestone 1 fixes implemented, and documenting Milestone 2, Milestone 3, and other improvements as recommendations/future roadmap).
3. Conclude the teamwork task.
