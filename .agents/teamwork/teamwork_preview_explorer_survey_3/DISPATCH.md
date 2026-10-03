# Survey Dispatch: Explorer 3 (Data Access, DbContext, Repositories, Controllers, Models, Clean Architecture)

## Objective
Conduct an in-depth audit of Data Access patterns, DbContext lifetimes & concurrency, Repositories, Controllers, Models, and Clean Architecture / SOLID boundaries across SRNSWebApp.

## Scope & Target Areas
1. `Data/` - DbContext definitions, configurations, migrations, repository implementations (if any).
2. DbContext Patterns & Concurrency:
   - Check DbContext lifetimes in Blazor Server / WebApp context. (DbContext is not thread-safe: is `IDbContextFactory` or properly scoped unit-of-work used?)
   - Query efficiency and encapsulation: `AsNoTracking` for read-only queries, proper async methods, `CancellationToken` propagation.
3. `Controllers/` and API endpoints:
   - Boundary separation, DTO vs entity leakage.
   - Exception handling and response consistency.
4. `Models/`:
   - Domain model encapsulation vs anemic domain models.
   - Validation logic placement.
5. SOLID & Clean Architecture:
   - Boundary crossing violations, high coupling, circular dependencies.
6. Compliance with project rules in `.agents/rules/mainRules.md`, `AGENTS.md`, and `.agents/skills/dotnet-design-pattern-review/SKILL.md`.

## Deliverables
Write your comprehensive audit findings to:
`/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_survey_3/handoff.md`

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
You are teamwork_preview_explorer_survey_3.
Your working directory is: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_survey_3
Read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/ORIGINAL_REQUEST.md before starting work.
Also read /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_survey_3/DISPATCH.md.
Also read skills: /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/skills/dotnet-design-pattern-review/SKILL.md and /Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/skills/dotnet-best-practices/SKILL.md.

Task:
Conduct an in-depth audit of Data Access patterns, DbContext lifetimes & concurrency, Repositories, Controllers, Models, and Clean Architecture / SOLID boundaries across SRNSWebApp.
Examine Data/, Controllers/, Models/, and database query/persistence patterns.
Check specifically for:
- DbContext lifetime in Blazor Server / WebApp (DbContext is not thread-safe: is IDbContextFactory used? Are concurrent operations on single DbContext possible?)
- Query efficiency: AsNoTracking on read-only queries, async/await correctness, cancellation tokens
- Repository / Unit of Work patterns and abstractions
- Controller DTO boundaries vs entity leakage
- Domain model encapsulation
Categorize findings by severity (Critical, High, Medium, Low), citing file and line references, pattern violated, and proposed fix.
Write your complete report to:
/Users/keisukekoyanagi/IdeaProjects/SRNSWebApp/.agents/teamwork/teamwork_preview_explorer_survey_3/handoff.md
Update progress.md in your working directory as you work.
When complete, send a message to orchestrator parent with your handoff path and summary.
