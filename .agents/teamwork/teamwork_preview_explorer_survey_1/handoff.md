# In-Depth Audit Report: Services, Dependency Injection, Lifetimes & Design Patterns

## 1. Observation

A full-codebase investigation of the `SRNSMudApp` solution (`Program.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Services/`, `Controllers/`, `Components/`, and `Data/`) was conducted to evaluate compliance with .NET design patterns, dependency injection best practices, service lifetimes, and architectural guidelines (`AGENTS.md`, `.agents/rules/mainRules.md`, `dotnet-design-pattern-review/SKILL.md`, `dotnet-best-practices/SKILL.md`).

Below are direct verbatim observations across the solution:

### Observation O1: `ExternalTokenVerificationService.cs` Mutating `_httpClient.DefaultRequestHeaders` Concurrently
- **File & Lines**: `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs:55-61`
- **Verbatim Code**:
  ```csharp
  private async Task<Result<ExternalTokenPayload>> VerifyLineTokenAsync(string idToken, CancellationToken cancellationToken)
  {
      try
      {
          _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
          HttpResponseMessage response = await _httpClient.GetAsync(new Uri("https://api.line.me/v2/profile"), cancellationToken);
  ```
- **Context**: In `ExternalTokenVerificationService`, `HttpClient` is injected via primary constructor. In `VerifyLineTokenAsync`, `_httpClient.DefaultRequestHeaders.Authorization` is mutated on the shared `HttpClient` instance. Conversely, in the same class at lines 86-95 (`VerifyGithubTokenAsync`), per-request `HttpRequestMessage` is used:
  ```csharp
  using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.github.com/user"));
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", codeOrToken);
  HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
  ```

### Observation O2: Captive Dependency & Header Mutation in Singleton `ExternalOgpLinkPreviewProvider.cs`
- **File & Lines**: `SRNSMudApp/Program.cs:171,175` & `SRNSMudApp/Services/Providers/ExternalOgpLinkPreviewProvider.cs:16-32`
- **Verbatim Code (`Program.cs`)**:
  ```csharp
  builder.Services.AddHttpClient();
  ...
  builder.Services.AddSingleton<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>();
  ```
- **Verbatim Code (`ExternalOgpLinkPreviewProvider.cs`)**:
  ```csharp
  public partial class ExternalOgpLinkPreviewProvider(
      HttpClient httpClient,
      ILogger<ExternalOgpLinkPreviewProvider> logger) : ILinkPreviewProvider
  {
      private readonly HttpClient _httpClient = ConfigureHttpClient(httpClient);
      ...
      private static HttpClient ConfigureHttpClient(HttpClient client)
      {
          ArgumentNullException.ThrowIfNull(client);
          if (!client.DefaultRequestHeaders.Contains("User-Agent"))
          {
              client.DefaultRequestHeaders.Add("User-Agent", "SRNSMudApp-LinkPreviewBot/1.0");
          }
          return client;
      }
  ```

### Observation O3: Orphan DI Registration & Bypassed Container for `ApplicationDbSaveChangesInterceptor`
- **File & Lines**: `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs:157,160`, `SRNSMudApp/Program.cs:127-151`, and `SRNSMudApp/Data/ApplicationDbContext.cs:824-834`
- **Verbatim Code (`ServiceCollectionExtensions.cs`)**:
  ```csharp
  // テスト時に時刻固定を可能にする TimeProvider 抽象化
  services.AddSingleton(TimeProvider.System);

  // EF Core SaveChangesInterceptor
  services.AddSingleton<Data.Interceptors.ApplicationDbSaveChangesInterceptor>();
  ```
- **Verbatim Code (`ApplicationDbContext.cs:824-834`)**:
  ```csharp
  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
      base.OnConfiguring(optionsBuilder);
      ArgumentNullException.ThrowIfNull(optionsBuilder);

      var coreOptions = optionsBuilder.Options.FindExtension<CoreOptionsExtension>();
      if (coreOptions?.Interceptors?.Any(i => i is ApplicationDbSaveChangesInterceptor) != true)
      {
          optionsBuilder.AddInterceptors(new ApplicationDbSaveChangesInterceptor());
      }
  }
  ```
- **Context**: In `Program.cs`, neither `AddDbContext<ApplicationDbContext>` nor `AddDbContextFactory<ApplicationDbContext>` configures interceptors via options. When contexts are created, `OnConfiguring` directly executes `new ApplicationDbSaveChangesInterceptor()`, passing `timeProvider: null` (defaulting to system clock). The Singleton registered in DI with injected `TimeProvider` is never retrieved or used by EF Core.

### Observation O4: Violation of Provider Pattern for External AI Services in `TagHierarchyService.cs`
- **File & Lines**: `SRNSMudApp/Services/TagHierarchyService.cs:128-147, 206-250`
- **Verbatim Code**:
  ```csharp
  // 2. Gemini API による判定フォールバック
  string? apiKey = _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
  if (!string.IsNullOrWhiteSpace(apiKey))
  {
      try
      {
          Tag? geminiMatched = await DetermineHierarchyViaGeminiAsync(newTagName, candidateTags, apiKey, cancellationToken);
  ...
  private async Task<Tag?> DetermineHierarchyViaGeminiAsync(
      string newTagName,
      List<Tag> candidateTags,
      string apiKey,
      CancellationToken cancellationToken)
  {
      using HttpClient client = _httpClientFactory.CreateClient();
      string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";
  ```
- **Context**: Raw HTTP POST to `generativelanguage.googleapis.com` with hardcoded model name and ad-hoc JSON document serialization is implemented directly inside domain service `TagHierarchyService`. There is no Provider abstraction (e.g., `IAiHierarchyProvider` or `IGeminiApiClient`) for external AI services.

### Observation O5: Scoped Event Broker Failure in `NotificationService.NotificationsChanged`
- **File & Lines**: `SRNSMudApp/Services/NotificationService.cs:56-59` and `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs:136`
- **Verbatim Code (`NotificationService.cs`)**:
  ```csharp
  public event EventHandler? NotificationsChanged;
  public void NotifyNotificationsChanged() => NotificationsChanged?.Invoke(this, EventArgs.Empty);
  ```
- **Verbatim Code (`ServiceCollectionExtensions.cs:136`)**:
  ```csharp
  services.AddScoped<INotificationService, NotificationService>();
  ```
- **Context**: In Blazor Server, each user circuit has its own isolated DI scope. When User A performs actions in `TagContentProposalService.ProposeContentAsync` (line 110), `TagNameProposalService.ProposeNameAsync` (line 147), `ContentReportCommands.ExecuteAsync` (line 116), or `ItemSplitService.RequestSplitAsync` (line 87), `_notificationService.NotifyNotificationsChanged()` is called on User A's Scoped instance. The target recipient (User B) is on a separate circuit and never receives the event.

### Observation O6: Missing Interface Abstraction for `RiskAssessmentService`
- **File & Lines**: `SRNSMudApp/Services/Auth/RiskAssessmentService.cs:5`, `SRNSMudApp/Program.cs:55`, and `SRNSMudApp/Controllers/AuthController.cs:18-24`
- **Verbatim Code (`Program.cs`)**:
  ```csharp
  builder.Services.AddScoped<RiskAssessmentService>();
  ```
- **Verbatim Code (`AuthController.cs`)**:
  ```csharp
  public partial class AuthController(
      IExternalTokenVerificationService tokenService,
      RiskAssessmentService riskService,
      ...
  ```
- **Context**: `RiskAssessmentService` has no interface (`IRiskAssessmentService`). It is registered and injected as a concrete class, violating Dependency Inversion and project naming standards.

### Observation O7: Transient IDisposable & Inconsistent ViewModel Lifetimes
- **File & Lines**: `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs:176-245` and `SRNSMudApp/Components/Layout/NavMenuViewModel.cs:13-23`
- **Verbatim Code (`ServiceCollectionExtensions.cs`)**:
  - Lines 176–220: 39 ViewModels registered as `services.AddScoped<...>()`
  - Lines 221–245: 24 ViewModels registered as `services.AddTransient<...>()`
  - Line 244: `services.AddTransient<NavMenuViewModel>();`
- **Verbatim Code (`NavMenuViewModel.cs`)**:
  ```csharp
  public sealed class NavMenuViewModel : IDisposable
  {
      public NavMenuViewModel(INotificationService notificationService)
      {
          _notificationService.NotificationsChanged += OnNotificationsChanged;
      }
      ...
      public void Dispose()
      {
          _notificationService.NotificationsChanged -= OnNotificationsChanged;
          GC.SuppressFinalize(this);
      }
  }
  ```
- **Context**: In .NET DI, Transient instances that implement `IDisposable` are captured by the current scope container until scope disposal.

### Observation O8: Inefficient Lifetime & Duplication in `AzureNotificationHubPushService`
- **File & Lines**: `SRNSMudApp/Program.cs:90-91` and `SRNSMudApp/Services/Push/AzureNotificationHubPushService.cs:32-60, 182-214`
- **Verbatim Code (`Program.cs`)**:
  ```csharp
  builder.Services.AddSingleton<IPushSubscriptionStore, InMemoryPushSubscriptionStore>();
  builder.Services.AddScoped<IWebPushNotificationService, AzureNotificationHubPushService>();
  ```
- **Verbatim Code (`AzureNotificationHubPushService.cs`)**:
  All constructor parameters (`IPushSubscriptionStore`, `IOptions<AzureNotificationHubOptions>`, `IOptions<VapidOptions>`, `ILogger`, `INotificationHubClient?`) are Singletons. When `_hubClient` is null, lines 182–214 duplicate the exact implementation of `WebPushNotificationService.cs:105-144`, instantiating `using var client = new WebPushClient();` per notification.

### Observation O9: Unregistered Dead Legacy Service `TagRelationService.cs`
- **File & Lines**: `SRNSMudApp/Services/TagRelationService.cs:1-207`
- **Context**: `TagRelationService` is a 207-line service with comments indicating `Reason = "TagRelationService.LinkTagToItemAsync (Legacy/Direct)"`. It is not registered in `Program.cs` or `ServiceCollectionExtensions.cs`. No application component or service injects it; only `SRNSMudApp.Tests/TagRelationServiceTests.cs` instantiates it via `new TagRelationService(...)`.

### Observation O10: Unused Service Locator Injection in `PasskeySubmit.razor`
- **File & Lines**: `SRNSMudApp/Components/Account/Shared/PasskeySubmit.razor:1`
- **Verbatim Code**:
  ```razor
  @inject IServiceProvider Services
  ```
- **Context**: The `Services` property is never referenced anywhere in `PasskeySubmit.razor`.

### Observation O11: Redundant DI Registration for `TaggingContractService`
- **File & Lines**: `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs:92-93`
- **Verbatim Code**:
  ```csharp
  services.AddScoped<ITaggingContractService, TaggingContractService>();
  services.AddScoped<TaggingContractService>(sp => (TaggingContractService)sp.GetRequiredService<ITaggingContractService>());
  ```
- **Context**: Zero consumers inject concrete `TaggingContractService`. All 11 consumers inject `ITaggingContractService`.

### Observation O12: Manual Factory Delegates for ViewModels in `ServiceCollectionExtensions.cs`
- **File & Lines**: `SRNSMudApp/Extensions/ServiceCollectionExtensions.cs:184-189`
- **Verbatim Code**:
  ```csharp
  services.AddScoped(sp => new UserDetailViewModel(
      sp.GetRequiredService<IUserDataProvider>(),
      sp.GetService<Microsoft.AspNetCore.Identity.UserManager<Data.ApplicationUser>>()));
  services.AddScoped(sp => new UserDetailActionViewModel(
      sp.GetRequiredService<IUserDataProvider>(),
      sp.GetService<Microsoft.AspNetCore.Identity.UserManager<Data.ApplicationUser>>()));
  ```

---

## 2. Logic Chain

From these observations, we trace the step-by-step reasoning leading to each finding:

```
[O1] VerifyLineTokenAsync mutates _httpClient.DefaultRequestHeaders.Authorization
     └──> HttpClient is shared across requests in the scope/app
     └──> Concurrent requests verifying tokens will clobber each other's Authorization headers
     └──> Result: Severe race condition, authentication failures, potential account takeover or wrong token validation.
     └──> Severity: CRITICAL (Finding 1)

[O2] AddSingleton<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>() receives Transient HttpClient
     └──> Singleton instance outlives the transient scope (Captive Dependency)
     └──> Singleton HttpClient prevents socket rotation and caches DNS queries indefinitely (DNS staleness)
     └──> Mutates client.DefaultRequestHeaders in constructor
     └──> Severity: HIGH (Finding 2)

[O3] ApplicationDbContext.OnConfiguring executes new ApplicationDbSaveChangesInterceptor()
     └──> Interceptor registered in DI with Singleton lifetime and TimeProvider is ignored
     └──> Container autowiring is bypassed; mock TimeProvider in test suites cannot intercept DbContext
     └──> Severity: HIGH (Finding 3)

[O4] TagHierarchyService embeds raw HttpClient calls to Gemini API endpoint with hardcoded URL and JSON schema
     └──> Violates Provider Pattern for external services and project AI integration rules
     └──> Domain logic is tightly coupled to Gemini endpoint; cannot mock or swap models cleanly
     └──> Severity: HIGH (Finding 4)

[O5] NotificationService.NotificationsChanged event is on a Scoped service in Blazor Server
     └──> Circuit A (User A) triggers event -> fires only on Circuit A's Scoped instance
     └──> Circuit B (User B, the recipient) never receives the event
     └──> UI real-time badge never updates for the affected user across circuits
     └──> Severity: HIGH (Finding 5)

[O6] RiskAssessmentService has no IRiskAssessmentService interface and is injected as concrete type
     └──> Violates DIP (Dependency Inversion Principle) and project naming rules
     └──> Prevents clean Moq mocking in controller unit tests
     └──> Severity: MEDIUM (Finding 6)

[O7] NavMenuViewModel implements IDisposable but is registered as AddTransient
     └──> In .NET DI, Transient IDisposable instances are tracked by the scope container until circuit teardown
     └──> Arbitrary split between Scoped and Transient ViewModels creates inconsistent state-sharing semantics
     └──> Severity: MEDIUM (Finding 7)

[O8] AzureNotificationHubPushService has 100% singleton dependencies but is AddScoped
     └──> Creates heavy NotificationHubClient per scope/circuit unnecessarily
     └──> Duplicates lines 105-144 of WebPushNotificationService; creates WebPushClient in a loop
     └──> Severity: MEDIUM (Finding 8)

[O9] TagRelationService is not registered in DI and unused by any application feature
     └──> 207 lines of duplicate/dead legacy code remaining in the codebase
     └──> Severity: MEDIUM (Finding 9)

[O10-O12] Minor code smells: unused IServiceProvider, redundant forwarding registration, manual factory delegates
     └──> Violate clean code and DI best practices
     └──> Severity: LOW (Findings 10, 11, 12, 13)
```

---

## 3. Catalog of Findings & Categorized Recommendations

| ID | Severity | Category | File & Line | Pattern Violated / Issue | Proposed Fix |
|---|---|---|---|---|---|
| **F-01** | **Critical** | Thread Safety / Concurrency | `Services/Auth/ExternalTokenVerificationService.cs:59-60` | Mutating shared `DefaultRequestHeaders.Authorization` | Use `HttpRequestMessage.Headers.Authorization` per request, exactly as done in `VerifyGithubTokenAsync`. |
| **F-02** | **High** | DI Lifetime / Captive Dependency | `Program.cs:171,175`, `Services/Providers/ExternalOgpLinkPreviewProvider.cs:16-20` | Singleton captures Transient `HttpClient` (Captive Dependency & DNS staleness) | Inject `IHttpClientFactory` into `ExternalOgpLinkPreviewProvider` or register as typed client `AddHttpClient<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>()`. |
| **F-03** | **High** | DI Registration / Bypassed DI | `Data/ApplicationDbContext.cs:832`, `Extensions/ServiceCollectionExtensions.cs:160` | `OnConfiguring` directly invokes `new ApplicationDbSaveChangesInterceptor()`, orphaning DI singleton | Pass interceptor into `AddDbContext` and `AddDbContextFactory` in `Program.cs` via `(sp, options) => options.AddInterceptors(sp.GetRequiredService<ApplicationDbSaveChangesInterceptor>())`. |
| **F-04** | **High** | Architecture / Provider Pattern | `Services/TagHierarchyService.cs:128-147, 213` | Direct hardcoded Gemini API HTTP calls in domain service violating Provider pattern | Extract `IAiHierarchyProvider` / `IGeminiClient` external provider abstraction and inject into `TagHierarchyService`. |
| **F-05** | **High** | Architecture / Observer Pattern | `Services/NotificationService.cs:56-59`, `Extensions/ServiceCollectionExtensions.cs:136` | Scoped event in Blazor Server fails to notify target user across circuits | Decouple event notification into a Singleton user-targeted notification bus/broker (`INotificationEventBus`). |
| **F-06** | **Medium** | SOLID / Dependency Inversion | `Services/Auth/RiskAssessmentService.cs:5`, `Controllers/AuthController.cs:20` | Missing interface abstraction (`IRiskAssessmentService`) | Extract `IRiskAssessmentService` interface, implement on `RiskAssessmentService`, update DI and controller. |
| **F-07** | **Medium** | DI Lifetime / Memory Leak | `Extensions/ServiceCollectionExtensions.cs:244`, `Components/Layout/NavMenuViewModel.cs:13` | Transient `IDisposable` tracking leak & inconsistent ViewModel lifetimes | Register `NavMenuViewModel` as `Scoped` (circuit-lifetime) or ensure non-container disposal; harmonize ViewModel lifetimes. |
| **F-08** | **Medium** | DI Lifetime / Code Duplication | `Program.cs:91`, `Services/Push/AzureNotificationHubPushService.cs:32` | Scoped registration of service with 100% singleton dependencies + duplicate fallback code | Change `IWebPushNotificationService` registration to `Singleton`; delegate fallback to `WebPushNotificationService`. |
| **F-09** | **Medium** | Code Quality / Dead Code | `Services/TagRelationService.cs:1-207` | Unregistered legacy service superseded by `ItemTagService` | Deprecate or remove `TagRelationService` and update tests to target `ItemTagService`. |
| **F-10** | **Low** | DI Registration Hygiene | `Extensions/ServiceCollectionExtensions.cs:93` | Redundant concrete forwarding registration for `TaggingContractService` | Remove line 93 `services.AddScoped<TaggingContractService>(sp => ...)`. |
| **F-11** | **Low** | Service Locator Anti-Pattern | `Components/Account/Shared/PasskeySubmit.razor:1` | Unused `@inject IServiceProvider Services` | Remove unused `@inject IServiceProvider Services`. |
| **F-12** | **Low** | DI Registration Hygiene | `Extensions/ServiceCollectionExtensions.cs:184-189` | Manual factory delegates for `UserDetailViewModel` | Replace with standard `services.AddScoped<UserDetailViewModel>()`. |
| **F-13** | **Low** | Performance / Lifetime Optimization | `Extensions/ServiceCollectionExtensions.cs:86-90, 152-154` | Stateless strategy executors & report handlers registered as Scoped | Consider registering stateless executors (`GratisContractExecutor`, `MutualContractExecutor`, `ReportTargetHandler`) as `Singleton` to eliminate repeated allocations and dictionary construction. |

---

## 4. Caveats

1. **Test Suite Integrity**: `SRNSMudApp.Tests/Architecture/ServiceCollectionExtensionsTests.cs` explicitly asserts existing lifetimes (e.g. line 60 checks `typeof(TaggingContractService)` being Scoped, line 89 checks `ApplicationDbSaveChangesInterceptor` being Singleton). Any refactoring of DI registrations must update these architectural tests in lockstep.
2. **Blazor Server Circuit Mechanics**: Blazor Server Scoped lifetime is tied to the SignalR circuit. Services registered as Scoped are singletons within that user's circuit. ViewModels holding per-view state may leak across navigations within the same circuit unless properly reset on navigation.
3. **Database Concurrency Isolation**: The project's decision to use `IDbContextFactory<ApplicationDbContext>` across all DataProviders and domain services is sound and effectively prevents EF Core DbContext thread-concurrency crashes in Blazor Server. The only gap identified in data access is the interceptor instantiation in `OnConfiguring`.

---

## 5. Conclusion

The SRNSWebApp architecture demonstrates strong patterns in data access decoupling (`IDbContextFactory` with Provider Pattern), Strategy implementations (`IContractExecutor`, `IReportTargetHandler`, `ILinkPreviewProvider`), and CQRS/Command patterns (`ICommandHandler`, `CommandHandlerBase`, `TagSearchQueryService` / `TagCommandService`). Furthermore, components strictly avoid direct `ApplicationDbContext` and `IDialogService` access.

However, the audit identified:
- **1 Critical bug**: Concurrency corruption in `ExternalTokenVerificationService.VerifyLineTokenAsync` due to shared `DefaultRequestHeaders` mutation.
- **4 High-severity architectural issues**: Captive dependency in `ExternalOgpLinkPreviewProvider`, bypassed DI interceptor in `ApplicationDbContext`, hardcoded Gemini HTTP calls in `TagHierarchyService`, and cross-circuit event failure in `NotificationService`.
- **4 Medium-severity issues**: Missing `IRiskAssessmentService` abstraction, Transient `IDisposable` tracking in `NavMenuViewModel`, Scoped registration and code duplication in `AzureNotificationHubPushService`, and dead legacy code in `TagRelationService`.
- **4 Low-severity cleanups**: Redundant registrations and unused service locator injections.

Addressing these issues will elevate the solution to enterprise-grade .NET quality, eliminate latent race conditions, and adhere fully to the repository's architectural mandates.

---

## 6. Verification Method

To verify these findings independently:

1. **Verify F-01 (ExternalTokenVerificationService race condition)**:
   - Inspect `SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs:59-60`. Compare with lines 90-95 in the same file.
2. **Verify F-02 (Captive Dependency)**:
   - Check `SRNSMudApp/Program.cs:175` (`AddSingleton<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>`) and `SRNSMudApp/Services/Providers/ExternalOgpLinkPreviewProvider.cs:16-20` (takes `HttpClient`).
3. **Verify F-03 (Bypassed Interceptor DI)**:
   - Inspect `SRNSMudApp/Data/ApplicationDbContext.cs:824-834`. Notice `new ApplicationDbSaveChangesInterceptor()` is called directly without DI parameters.
4. **Verify F-04 (Gemini Provider Pattern Violation)**:
   - Inspect `SRNSMudApp/Services/TagHierarchyService.cs:212-214`. Notice direct HTTP POST to Gemini URL without an external provider interface.
5. **Verify F-05 (Cross-circuit Notification Failure)**:
   - Inspect `SRNSMudApp/Services/NotificationService.cs:56-59` and `Extensions/ServiceCollectionExtensions.cs:136`. Observe `NotificationsChanged` event is invoked on a Scoped service.
6. **Verify Architecture Tests**:
   - Run:
     ```bash
     dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~Architecture"
     ```
   - Invalidation condition: If any finding claimed above does not match the actual code in the specified file:line, the finding is invalid.
