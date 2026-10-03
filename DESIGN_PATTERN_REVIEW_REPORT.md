# SRNSWebApp 総合アーキテクチャ & デザインパターンレビュー報告書
**Comprehensive .NET/C# Design Pattern Review & Refactoring Report**

- **対象ソリューション**: `SRNSWebApp` (`SRNSMudApp`, `SRNSMudApp.Client`, `SRNSMudApp.Tests`, `SRNSMudApp.E2ETests`)
- **ターゲットフレームワーク**: .NET 11.0 (Preview), ASP.NET Core Blazor Web App (InteractiveServer & WASM), MudBlazor 9.7, Entity Framework Core 11 (SQL Server / HierarchyId / LocalEmbeddings)
- **監査実施日**: 2026-10-02
- **監査者**: Teamwork Architecture Audit Agent (`worker_m4_report`)
- **統合ステータス**: Milestone 1（高優先度セキュリティ・並行性・データ整合性改修）完了 & 検証済み、Milestone 2/3（将来ロードマップ策定）
- **フォレンジック監査判定**: **CLEAN**（ファサード・ハードコード一切なし、実環境検証完了）

---

## 目次 (Table of Contents)

1. [エグゼクティブサマリ (Executive Summary)](#1-エグゼクティブサマリ-executive-summary)
2. [ソリューション全5レイヤー別アーキテクチャ & デザインパターン詳細分析 (Architecture & Design Pattern Analysis Across All 5 Solution Layers)](#2-ソリューション全5レイヤー別アーキテクチャ--デザインパターン詳細分析)
   - [2.1 サービスレイヤー (Services Layer)](#21-サービスレイヤー-services-layer)
   - [2.2 データアクセス & 永続化レイヤー (Data Access & Persistence Layer)](#22-データアクセス--永続化レイヤー-data-access--persistence-layer)
   - [2.3 プレゼンテーション・ViewModel・コンポーネントレイヤー (Presentation, ViewModels & Components Layer)](#23-プレゼンテーションviewmodelコンポーネントレイヤー-presentation-viewmodels--components-layer)
   - [2.4 API コントローラーレイヤー (API Controllers Layer)](#24-api-コントローラーレイヤー-api-controllers-layer)
   - [2.5 ドメインモデル & エンティティレイヤー (Domain Models & Entities Layer)](#25-ドメインモデル--エンティティレイヤー-domain-models--entities-layer)
3. [Milestone 1 実装・検証済み高優先度修正 (High-Priority Fixes Implemented & Verified in Milestone 1)](#3-milestone-1-実装検証済み高優先度修正)
   - [3.1 SEC-01: プッシュ通知認可制御およびサブスクリプションなりすまし脆弱性の完全排除](#31-sec-01-プッシュ通知認可制御およびサブスクリプションなりすまし脆弱性の完全排除)
   - [3.2 DATA-01 & DATA-02: JPYC購入における金融トランザクション原子性保証 & ExecutionStrategy再試行冪等性確立](#32-data-01--data-02-jpyc購入における金融トランザクション原子性保証--executionstrategy再試行冪等性確立)
   - [3.3 THREAD-01: 外部トークン検証サービスにおける並行性競合解消 & 不正ヘッダー例外防御](#33-thread-01-外部トークン検証サービスにおける並行性競合解消--不正ヘッダー例外防御)
4. [Milestone 2 & Milestone 3 優先度別推奨事項 & 将来ロードマップ (Prioritized Recommendations & Future Roadmap)](#4-milestone-2--milestone-3-優先度別推奨事項--将来ロードマップ)
   - [4.1 優先度 1 (Milestone 2 スコープ): DIライフタイム、リソースリーク解消 & インターセプター統合](#41-優先度-1-milestone-2-スコープ-diライフタイムリソースリーク解消--インターセプター統合)
   - [4.2 優先度 2 (Milestone 3 スコープ): クリーンアーキテクチャ分離 & DTO境界確立](#42-優先度-2-milestone-3-スコープ-クリーンアーキテクチャ分離--dto境界確立)
   - [4.3 優先度 3: クエリ効率化 & パフォーマンス最適化ロードマップ](#43-優先度-3-クエリ効率化--パフォーマンス最適化ロードマップ)
5. [最終検証結果マトリクス (Verification Results Summary)](#5-最終検証結果マトリクス-verification-results-summary)

---

## 1. エグゼクティブサマリ (Executive Summary)

本レビューは、`SRNSWebApp` 全体（`Services/`, `Data/`, `Components/`, `Controllers/`, `Models/`）を対象に、エンタープライズ .NET/C# デザインパターン、SOLID原則、Blazorライフサイクル規約、EF Core 11並行性制御、およびプロジェクト固有ルール（`AGENTS.md`, `.agents/rules/mainRules.md`）への適合性を網羅的に診断し、高リスク欠陥の是正と将来改修ロードマップを策定した公式アーキテクチャ報告書です。

### 1.1 ソリューション概要
- **UI & プレゼンテーション**: ASP.NET Core Blazor Web App (.NET 11 Preview)、MudBlazor 9.7。SSR と InteractiveServer（一部 WASM）が混在するハイブリッド構成。
- **データアクセス**: EF Core 11、SQL Server（`HierarchyId` による階層構造管理、`LocalEmbeddings` による 384次元ベクトル検索）、`IDbContextFactory<ApplicationDbContext>` による並行アクセス分離。
- **認証 & 認可**: ASP.NET Core Identity Core + LINE/GitHub 外部トークン検証サービス。

### 1.2 評価概要と総合スコア
コードベースは全体として、Blazor Server における UI/データアクセスの分離（`IDataProvider` 抽象化）、`IDialogService` 直接利用の禁止（`IDialogLauncher` ラッパーの徹底）、Command パターンや Strategy パターンの積極的導入など、先進的で洗練されたアーキテクチャ意図を持っています。

しかしながら、先行する詳細調査（Phase 0 Explorer Surveys 1〜3）により、**直ちに対処が必要なクリティカルな不具合が 4 件（認証欠落、金融トランザクション二重コミット/再試行汚染、共有ヘッダー並行書き換え）**、ならびに **DIライフタイム不整合、Clean Architecture レイヤー逆転、クエリ性能雪崩などの高〜中リスク課題** が特定されました。

| 領域 / レイヤー | 健全性評価 | 主な強み | 主な懸念事項 & 課題 |
|---|---|---|---|
| **Services** | **良好 (B+)** | CQRS/Command パターン、Strategy パターンが高度に適用 | Gemini API 直叩き（Provider化不足）、通知イベントの Circuit 内局所化 |
| **Data Access** | **要改善 (B-)** | `IDbContextFactory` による UI/DB スレッド分離 | `ExecutionStrategy` の誤用によるトランザクション喪失、15クエリ雪崩、ベクトル全件メモリ展開 |
| **Components & MVVM** | **良好 (B+)** | `AGENTS.md` 規約完全遵守（DB直叩き0、Dialog直利用0） | Dialog ViewModel の `Scoped` 登録による状態汚染、JS Module 未破棄リーク |
| **Controllers** | **堅牢 (A-)** ※M1改修後 | M1にて認証認可・DTO無害化を完了 | エラー応答フォーマット（ProblemDetails）の統一余地 |
| **Domain & Models** | **良 (B)** | 階層ノード・権限モデルの明確なドメインモデリング | Services が Components.UI を参照するレイヤー逆転、CA2227 コレクション公開セッター |

### 1.3 監査判定
ユーザーからの指示（Milestone 1 実装完了後にコード変更を凍結し、Milestone 4 最終検証・総合報告へ直行）に基づき、**Milestone 1 の高優先度課題（SEC-01, DATA-01, DATA-02, THREAD-01）の修正を完遂し、フォレンジック監査において「CLEAN」判定を獲得**しました。後続の Milestone 2 / Milestone 3 課題については、実装手順と設計方針を詳細な推奨ロードマップとして本報告書にまとめました。

---

## 2. ソリューション全5レイヤー別アーキテクチャ & デザインパターン詳細分析

### 2.1 サービスレイヤー (Services Layer)

#### 採用されているデザインパターン
1. **Command パターン / CQRS 分離**:
   - `SRNSMudApp/Services/Commands/` 配下において、`ICommandHandler<TOptions>`, `CommandHandlerBase<TOptions>` を基底とするミューテーション処理が整備されています。
   - 例: `TagSearchQueryService`（読み取り・検索クエリ）と `TagCommandService`（タグ作成・更新コマンド）により、明瞭な CQRS パスが形成されています。
2. **Strategy パターン**:
   - 契約履行エンジン `IContractExecutor` に対し、`GratisContractExecutor`, `MutualContractExecutor`, `RightTransferContractExecutor` がポリモーフィックに注入され、契約種別ごとのビジネスルールを疎結合に保っています。
   - 通報処理において `IReportTargetHandler`（Item, Tag, User 等）がディスパッチされています。
3. **Provider パターン**:
   - 外部リンク取得において `ILinkPreviewProvider`（`ExternalOgpLinkPreviewProvider`, `ItemLinkPreviewProvider`）が抽象化されています。

#### 発見された設計課題 & 改善点
- **キャプティブ依存 (Captive Dependency) [F-02 / CAPTIVE-01]**:
  - `Program.cs:175` において、`builder.Services.AddSingleton<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>();` と登録されている一方、`ExternalOgpLinkPreviewProvider` はプライマリコンストラクタで `HttpClient`（Transient または Scoped 生成インスタンス）を直接受け取っています。
  - **影響**: シングルトンが一時的な `HttpClient` を永久に保持（キャプティブ化）するため、DNS 解決の更新が反映されず（DNS Staleness）、ソケット枯渇やネットワーク到達不能を招くリスクがあります。またコンストラクタ内で `client.DefaultRequestHeaders.Add("User-Agent", ...)` を実行しており、インスタンス共有時に競合の原因となります。
- **外部 AI 連携における Provider パターン違反 [F-04]**:
  - `TagHierarchyService.cs:128-147, 206-250` において、ドメインロジックの内部から直接 `_httpClientFactory.CreateClient()` を用い、Google Generative Language API (`gemini-2.0-flash:generateContent`) に対する生 HTTP POST 通信と独自 JSON パースを行っています。
  - **影響**: プロジェクト共通ルール（`dotnet-design-pattern-review`）が要求する「AIサービス抽象化プロバイダ」が存在せず、モデルの切り替え、フォールバック、モックテストが困難になっています。
- **Blazor Server Circuit 境界を越えられない Scoped Event Broker [F-05]**:
  - `NotificationService.cs:56-59` の `event EventHandler? NotificationsChanged;` は、`ServiceCollectionExtensions.cs:136` にて `AddScoped<INotificationService, NotificationService>()` として登録されています。
  - **影響**: Blazor Server では各ブラウザ接続ごとに独立した DI Scope（Circuit）が割り当てられます。ユーザー A がタグ提案や通報を行った際、ユーザー A の Circuit 内の `NotificationService` でイベントが発火しますが、通知先であるユーザー B の Circuit にはイベントが一切伝播せず、UI の未読バッジがリアルタイム更新されません。
- **デッドコード / レガシーサービス [F-09]**:
  - `TagRelationService.cs`（207行）はコメントに `(Legacy/Direct)` と明記され、DI コンテナにも未登録ですが、リポジトリ内に残存しておりテストのみから参照されています。

---

### 2.2 データアクセス & 永続化レイヤー (Data Access & Persistence Layer)

#### 採用されているデザインパターン
1. **Factory パターン (`IDbContextFactory<T>`)**:
   - Blazor Server の並行スレッド制約を回避するため、`ApplicationDbContext` を直接 Scoped 注入せず、`IDbContextFactory<ApplicationDbContext>` を注入して各操作ごとに軽量な短命 DbContext を生成するパターンが一貫して適用されています。
2. **Provider パターン (`IDataProvider`)**:
   - `ItemListDataProvider`, `TagCardDataProvider`, `UserDataProvider`, `HomeDataProvider` 等により、コンポーネントから EF Core を完全に隠蔽しています。

#### 発見された設計課題 & 改善点
- **トランザクション境界の喪失と偽の原子性 [DATA-01] (※M1にて修正完了)**:
  - `ExecutionStrategyExtensions.cs` の xmldoc に「トランザクションは内部で管理されるため明示的な Begin/Commit/Rollback は不要」と誤って記載され、複数回の `SaveChangesAsync()` を含む操作がトランザクションなしで `ExecuteWithStrategyAsync` に渡されていました。
- **金融アセットと預入トランザクションの分離コミット [DATA-02] (※M1にて修正完了)**:
  - `RightAssetPurchaseService.cs` において、`RightAsset` の新規発行コミットと `JpycDepositTransaction` の保存コミットが別々の `SaveChangesAsync()` で行われ、2フェーズコミットハザード（資産のみ発行される二重支払いリスク）が存在していました。
- **DI 登録インターセプターのバイパス [F-03 / INTERCEPT-01]**:
  - `ServiceCollectionExtensions.cs:160` で `services.AddSingleton<ApplicationDbSaveChangesInterceptor>();` を登録し、`TimeProvider` を注入可能にしているにもかかわらず、`ApplicationDbContext.cs:832` の `OnConfiguring` で `new ApplicationDbSaveChangesInterceptor()` と直接引数なしでインスタンス化されていました。
  - **影響**: DI コンテナのシングルトンと注入された `TimeProvider` が無視され、単体テストで時刻の固定・モック化が機能しません。
- **未読バッジ取得時の 15 クエリ雪崩 (Query Avalanche) [PERF-01]**:
  - `NotificationService.GetUnreadCountAsync` が `NotificationsDataProvider.GetNotificationRawDataAsync` を呼び出します。このメソッドは 8 テーブルにわたる 15 回の個別 SQL クエリを連続実行し、全関連エンティティをメモリに読み込んだ上で C# 側で未読件数をカウントしています。ページ遷移やヘッダー再描画ごとに膨大な DB 負荷を発生させています。
- **オートコンプリート時のベクトル全件インメモリ走査 [PERF-02]**:
  - `ItemListDataProvider.cs`, `TagSuggestionService.cs`, `TagHierarchyService.cs`, `TagSearchQueryService.cs` において、入力キーストロークのたびに `context.Tags.Where(t => t.Embedding != null).ToListAsync()` を実行し、テーブル全体の全タグと 384次元の float[] ベクトル配列をサーバーメモリへ毎回ダウンロードしてコサイン類似度を C# で計算しています。データ量増加に伴い致命的なメモリ逼迫とレイテンシ悪化を招きます。
- **一括削除における N+1 DB 接続 [PERF-03]**:
  - `TagTreeDataProvider.DeleteTagsAsync` 内で、対象タグごとに `TagLockService.IsTagOrSiblingLockedAsync` をループ呼び出ししており、タグ数 N に対して N 個の DbContext と 2〜3N 回の SQL クエリが発生しています。
- **読み取り専用クエリにおける `AsNoTracking()` の欠落 [PERF-04]**:
  - `AdminDataProvider`, `ContentReportService`, `ContractDataProvider`, `TagCardDataProvider`, `ItemListDataProvider` などの多数の参照系クエリで `AsNoTracking()` が付与されておらず、短命コンテキストであっても不要な ChangeTracker トラッキングオーバーヘッドが発生しています。

---

### 2.3 プレゼンテーション・ViewModel・コンポーネントレイヤー (Presentation, ViewModels & Components Layer)

#### 採用されているデザインパターン & ルール遵守状況
1. **`AGENTS.md` 規約完全遵守**:
   - **コンポーネントからの DbContext 直接アクセス: 0 件**。すべて `IDataProvider` 経由。
   - **コンポーネントからの `IDialogService` 直接利用: 0 件**。すべて独自ラッパー `IDialogLauncher` を介して型安全に起動。
2. **リソース破棄とイベント購読解除**:
   - `LocationChanged` や `DotNetObjectReference` を扱うコンポーネント（`ItemCard`, `TagCard`, `NavMenu`, `NotificationBadge` 等）は、漏れなく `IDisposable` または `IAsyncDisposable` を実装し、購読解除を徹底しています。

#### 発見された設計課題 & 改善点
- **`IJSObjectReference` モジュールの未破棄リーク [F-01 / LEAK-01]**:
  - `TagHierarchyService.cs:105-118` の `SuggestParentTagAsync` において、`await jsRuntime.InvokeAsync<IJSObjectReference>("import", ... "./js/tagHierarchy.js")` で動的インポートした JS モジュール参照を、ローカル変数 `module` に代入したまま `DisposeAsync()` も `await using` も行わずに破棄しています。
  - **影響**: 親タグ提案を実行するたびに Blazor の JS Interop ハンドルが破棄されず蓄積し、クライアントブラウザおよび SignalR サーキット双方でメモリリークが発生します。
- **Dialog ViewModel の `Scoped` 登録によるサーキット状態汚染 [F-02 / STATE-01]**:
  - `ServiceCollectionExtensions.cs:176-220` において、`TagAddViewModel`, `TagEditViewModel`, `ItemEditViewModel`, `PurchaseRightAssetViewModel`, `BountyCreateViewModel` などのダイアログ用 ViewModel が **20件以上 `AddScoped` として登録**されています。
  - **影響**: Blazor InteractiveServer では `Scoped` のライフタイムはユーザーの SignalR サーキット（セッション全体）と同一です。ユーザーがダイアログを開き、入力して閉じた後、再度同じダイアログを開くと同一の ViewModel インスタンスが注入されます。`InitializeAsync()` で部分的に初期化しても、未送信の入力文字列や検索結果、選択状態などのプロパティが残留し、状態汚染や入力のゴースト表示バグを引き起こします。これらは本来 `AddTransient` であるべきです。
- **UI コンポーネントと未接続の孤立 ViewModel [F-03 / ORPHAN-01]**:
  - `ReactionCommentViewModel`: 単体テスト（`ReactionCommentViewModelTests.cs`）も完備され DI 登録されていますが、対応する `ReactionCommentDialog.razor.cs` では一切インジェクションされず、ダイアログ側で `_remainingSeconds = 10` や `CancellationTokenSource` によるタイマー制御をベタ書きで再実装しています。
  - `CreateEdgeViewModel`: クラスも単体テストも存在しますが、DI 未登録であり、`CreateEdgeDialog.razor.cs` でも使われていません。
- **Presentation の責務漏洩 (Snackbar & Dialog 呼び出し)**:
  - 一部の ViewModel（`TagAddViewModel`, `UserManagementViewModel`, `RightAssetOverviewViewModel` 等）が `ISnackbar` や `IDialogLauncher` を直接インジェクションし、UI ポップアップを制御しています。純粋な MVVM では ViewModel は状態とコマンド結果を返し、UI 側がトースト表示をハンドリングするのが理想的です。

---

### 2.4 API コントローラーレイヤー (API Controllers Layer)

#### 設計状況 & M1での是正完了事項
- **認証欠落による全世界プッシュ通知ブロードキャスト脆弱性 [SEC-01] (※M1にて修正完了)**:
  - `PushNotificationController.cs:74-90` の `[HttpPost("send")]` に `[Authorize]` 属性が一切なく、未認証の第三者が全世界の登録ユーザーに対して任意のプッシュ通知を一斉送信できる状態でした。
  - M1 にて `[Authorize(Roles = "Admin")]` を適用し、管理者以外のアクセスを遮断しました。
- **サブスクリプション登録における UserId なりすまし脆弱性 [SEC-01] (※M1にて修正完了)**:
  - `[HttpPost("subscribe")]` において、クライアントが JSON ボディに指定した `subscription.UserId` がそのままバックエンドストアに登録可能となっており、未認証の攻撃者が他人の `UserId` を騙ってプッシュ購読を上書き・盗聴できる脆弱性がありました。
  - M1 にて、サーバー側で検証されたクレーム（`NameIdentifier` / `sub`）に基づく DTO 無害化（`subscription with { UserId = userId }`）を徹底し、未認証時は強制的に `null` に上書きする防御を実装しました。
- **外部トークン検証の並行性破壊 [THREAD-01] (※M1にて修正完了)**:
  - `ExternalTokenVerificationService.VerifyLineTokenAsync` において、共有インスタンスである `_httpClient.DefaultRequestHeaders.Authorization` を書き換えて通信を行っていました。並行リクエスト時に他ユーザーのトークンで上書きされる致命的な人違い認証バグが存在していましたが、M1 にてリクエスト単位の `HttpRequestMessage` 利用に是正されました。

---

### 2.5 ドメインモデル & エンティティレイヤー (Domain Models & Entities Layer)

#### クリーンアーキテクチャ境界とドメインカプセル化
- **クリーンアーキテクチャの依存方向逆転 [ARCH-01, ARCH-02, ARCH-03]**:
  - 依存の矢印は「UI → ViewModels → Services → Data/Domain Models」と内側に向かうべきですが、本ソリューションでは **`Services/` 配下の複数のドメインサービスが `Components.UI` や `Components.Pages` を直接参照するレイヤー逆転**が発生しています。
    - `InternalLinkConversionService.cs:11`: `using SRNSMudApp.Components.UI;` をインポートし、`ItemCardViewModel.GetContentSegments(content)` を呼び出し。
    - `ItemListExportService.cs:6`: `using SRNSMudApp.Components.UI;` をインポートし、`ItemCardViewModel.ExtractUrls(content)` を呼び出し。
    - `HomeDataProvider.cs:5`: `using SRNSMudApp.Components.Pages;` をインポートし、UI 側で定義された `TimelineFeedGroup` を戻り値に使用。
    - `SystemTagEnsurer.cs:1`: `using SRNSMudApp.Components.UI;` をインポートし、UI の ViewModel 内に宣言された `SystemTagIds`, `ReactionTagIds` を利用。
    - `ItemCardSplitCoordinator.cs`, `ItemCardTagCoordinator.cs`: `Services/` 配下に配置されているにもかかわらず、`IDialogLauncher`, `ISnackbar`, `Components.UI.RejectRequestDialog` などの UI 型に依存。
- **ドメインエンティティのアネミック（貧血型）構造 & CA2227 警告抑止**:
  - `Item`, `Tag`, `TagRelation`, `RightAsset` などのエンティティにおいて、ナビゲーションコレクションプロパティが `public ICollection<...> ... { get; set; } = [];` と公開セッターを持っており、コード分析規則 `CA2227` が抑止されています。外部からコレクションインスタンスそのものを差し替え可能な状態となっており、EF Core のリレーション追跡を破壊する恐れがあります。
- **エンティティプロパティ内での反復 JSON デシリアライズ**:
  - `TaggingRequestEntity.cs:107-111` や `RightAsset.cs:20-25` において、`[NotMapped]` な `Payload` プロパティの `get` アクセサ内でアクセスのたびに `JsonSerializer.Deserialize` を実行しており、不要なアロケーションを発生させています。
- **`BaseEntity` における可変 GetHashCode アンチパターン**:
  - `BaseEntity.cs:51` において、`GetHashCode()` が `Id == 0 ? base.GetHashCode() : Id.GetHashCode();` と実装されています。未保存時（Id=0）に `HashSet` や `Dictionary` のキーに格納されたエンティティが、DB 保存後に Id が採番されるとハッシュコードが変化し、コレクションから検索できなくなるバグを生じさせます。

---

## 3. Milestone 1 実装・検証済み高優先度修正

Milestone 1 では、システムのセキュリティ境界、金融取引の原子性、および認証基盤のスレッド安全性を担保するため、以下の 4 大重要課題（SEC-01, DATA-01, DATA-02, THREAD-01）を対象に修正を完了しました。

### 3.1 SEC-01: プッシュ通知認可制御およびサブスクリプションなりすまし脆弱性の完全排除

#### 脆弱性の内容
1. `PushNotificationController.SendNotification`（`/api/PushNotification/send`）が無認可で公開されており、誰でも全ユーザーへプッシュ通知をブロードキャスト可能だった。
2. `PushNotificationController.Subscribe` において、未認証の呼び出し元がペイロードの `subscription.UserId` に被害者の `UserId` を指定すると、メモリ内ストア（`InMemoryPushSubscriptionStore`）が `userId ?? subscription.UserId` のフォールバックロジックによって被害者のアカウントとして購読情報をインデックス化し、なりすまし登録が可能だった。

#### 修正ファイル & コード変更内容
1. **`SRNSMudApp/Controllers/PushNotificationController.cs`**:
   - `SendNotification` エンドポイントに `[Authorize(Roles = "Admin")]` 属性を付与。
   - `Subscribe` メソッドにおいて、認証クレーム（`ClaimTypes.NameIdentifier` または `"sub"`）から `authenticatedUserId` を厳格に抽出し、未認証の場合は `null` を強制。
   - クライアント入力の DTO を C# 10 レコードの with 式で無害化（Sanitization）:
     ```csharp
     string? authenticatedUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
     string? userId = (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedUserId))
         ? authenticatedUserId
         : null;

     // SEC-01: クライアントがリクエストボディで指定した UserId を無条件に破棄し、
     // サーバー側で検証した userId（認証済みならクレーム値、未認証なら null）で DTO を無害化して保存する
     var sanitizedSubscription = subscription with { UserId = userId };

     await _subscriptionStore.AddOrUpdateAsync(sanitizedSubscription, userId, cancellationToken);
     ```
2. **`SRNSMudApp/Services/Push/InMemoryPushSubscriptionStore.cs`**:
   - クライアント DTO 由来の `subscription.UserId` へのフォールバックを完全排除し、引数で明示的に渡されたサーバー検証済み `userId` のみを採用:
     ```csharp
     // SEC-01: クライアント入力由来の subscription.UserId へのフォールバックを排除し、
     // 呼び出し元から明示的に渡された検証済み userId のみを採用する
     string? effectiveUserId = userId;
     var dtoWithUserId = subscription with { UserId = effectiveUserId };
     _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
     ```

#### 設計意図（ルール適合性）
- ゼロトラスト原則に基づき、クライアントから送信されたユーザー識別子を信用せず、ASP.NET Core Identity の認証トークンから抽出したクレームのみを信頼の唯一の根拠としました。
- ストア層においても二重防御（Defense-in-Depth）を敷き、不正な DTO が渡された場合でもフォールバックしない堅牢な設計としました。

#### 検証結果 & 敵対的テストの証跡
- `PushNotificationAdversarialTests`: 実ストアを直接攻撃する `DirectStoreExploit`、および実際の ASP.NET Core テストサーバーへ HTTP 通信を行う `EndToEndHttpExploit` の双方が実行され、被害者の購読リストに攻撃者の購読情報が混入しないことを確認（合格: 4件）。
- `PushNotificationTests`: 認可および購読登録単体テスト（合格: 12件）。

---

### 3.2 DATA-01 & DATA-02: JPYC購入における金融トランザクション原子性保証 & ExecutionStrategy再試行冪等性確立

#### 課題の内容
1. `RightAssetPurchaseService.PurchaseRightAssetWithJpycAsync` において、`RightAsset` を追加した後に `SaveChangesAsync()` を実行し、その後 `JpycDepositTransaction` を追加して再度 `SaveChangesAsync()` を実行していた。2回目の保存で例外が発生した場合、資産だけが発行されて預入履歴が残らず、同一トランザクションハッシュを用いた二重購入（Double-Spending）が可能となる致命的なハザードが存在した。
2. EF Core の `SqlServerRetryingExecutionStrategy`（`ExecuteWithStrategyAsync`）は一時的エラー発生時にデリゲート全体を再実行するが、**トランザクションのロールバックや `ChangeTracker` の状態リセットは行わない**。そのため、1回目の試行で失敗したエンティティが `ChangeTracker` に残存し、2回目の試行で重複登録（Double-Minting）や一意制約違反（`IX_JpycDepositTransactions_TransactionHash`）を引き起こす問題があった。

#### 修正ファイル & コード変更内容
1. **`SRNSMudApp/Services/RightAssetPurchaseService.cs`**:
   - `ExecuteWithStrategyAsync` デリゲートの先頭で必ず `dbContext.ChangeTracker.Clear();` を実行し、再試行時に前回の失敗エンティティをデタッチ。
   - `await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);` で明示的トランザクションを開始。
   - `RightAsset` と `JpycDepositTransaction` を同一トランザクション内で処理し、最後に `await transaction.CommitAsync(cancellationToken);` を実行して原子性を保証。
     ```csharp
     return await dbContext.Database.ExecuteWithStrategyAsync(async () =>
     {
         // DATA-01 / DATA-02: 一時的障害によるリトライ発生時、前回試行で失敗・ロールバックされたエンティティが
         // ChangeTracker に残存していると、重複登録（double-minting）や一意キー制約違反が発生する。
         // 各試行の開始時に ChangeTracker をクリアして常にクリーンな状態でトランザクションを再実行する。
         dbContext.ChangeTracker.Clear();

         await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
         ...
         // 両エンティティの保存
         await dbContext.SaveChangesAsync(cancellationToken);
         ...
         await transaction.CommitAsync(cancellationToken);
         return Result.Success(new PurchaseRightAssetResult(newAsset.Id, depositTx.Id));
     });
     ```
2. **`SRNSMudApp/Data/ExecutionStrategyExtensions.cs`**:
   - 誤解を招く xmldoc コメントを改訂し、再試行デリゲート内における `ChangeTracker.Clear()` の呼び出しと明示的な `BeginTransactionAsync` / `CommitAsync` の必須性を設計指針として明文化。

#### 設計意図（ルール適合性）
- 金融アセットの整合性を最優先とし、ACID 特性を完全に担保しました。
- クラウド環境（Azure SQL Database 等）における一時的接続断時のリトライアルゴリズムに対し、ChangeTracker の冪等性リセットを組み込むことで、フェイルセーフなデータ永続化を実現しました。

#### 検証結果 & 証跡
- 実 SQL Server インスタンスに対する一時的障害シミュレーションテスト（`SaveChangesInterceptor` で 1回目・2回目の保存時に `TimeoutException` を注入）：
  - `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnFirstSave_RetriesSafelyWithoutDuplicatingAssets`: 合格（発行アセット数 1、重複 0）。
  - `PurchaseRightAssetWithJpycAsync_WhenTransientFailureOnSecondSave_RetriesSafelyAndAtomically`: 合格（一意キー違反 0、アセット数 1、Tx 数 1）。
  - `PurchaseRightAssetWithJpycAsync_WhenDepositTransactionSaveFails_RollsBackAssetAtomically`: 合格（ロールバック確認）。

---

### 3.3 THREAD-01: 外部トークン検証サービスにおける並行性競合解消 & 不正ヘッダー例外防御

#### 課題の内容
1. `ExternalTokenVerificationService.VerifyLineTokenAsync` において、クラス共有の `_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);` を書き換えて通信していた。並行して複数ユーザーがログイン処理を行うと、ヘッダーの競合（人違いトークンでの検証）や `InvalidOperationException` が発生していた。
2. トークン文字列に改行コードや制御文字が含まれる場合、`AuthenticationHeaderValue` のコンストラクタが `FormatException` をスローし、未処理例外（HTTP 500）となっていた。

#### 修正ファイル & コード変更内容
1. **`SRNSMudApp/Services/Auth/ExternalTokenVerificationService.cs`**:
   - `_httpClient.DefaultRequestHeaders` への変更を完全に排除し、リクエスト単位の `HttpRequestMessage` を生成して `request.Headers.Authorization` を設定する方式に統一。
   - `FormatException` に対する専用の catch ブロックを設け、システム例外ではなくドメインの失敗結果（`Result.Fail`）として安全に返却。
     ```csharp
     using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.line.me/v2/profile"));
     try
     {
         request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
     }
     catch (FormatException ex)
     {
         return LogAndReturnFailure("Invalid LINE token format", ex);
     }

     HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
     ```

#### 設計意図（ルール適合性）
- .NET の `HttpClient` 設計ベストプラクティス（`DefaultRequestHeaders` は起動時イミュータブル、リクエスト固有ヘッダーは `HttpRequestMessage` で設定）に準拠しました。
- 外部入力に起因するフォーマット異常を上位へ漏出させず、Result パターンで安全にカプセル化しました。

#### 検証結果 & 証跡
- `THREAD01_StressTest_HighConcurrency_LineTokenVerification`: 100件の並行リクエストにランダムな微小遅延を加え、共有ヘッダーの汚染が一切発生しないことを実証（合格）。
- `ExternalTokenVerificationServiceTests`: 不正フォーマットトークンに対する防御テストを含む全 6 件合格。

---

## 4. Milestone 2 & Milestone 3 優先度別推奨事項 & 将来ロードマップ

ユーザー指示に基づき、Milestone 1 をもってコード変更を凍結したため、Phase 0 の包括調査によって洗い出された残余の重要課題を、今後のイテレーションで実施すべき「優先度別推奨事項 & 将来ロードマップ」として構造化しました。

### 4.1 優先度 1 (Milestone 2 スコープ): DIライフタイム、リソースリーク解消 & インターセプター統合

| 課題 ID | 分類 | 対象ファイル | 課題の概要 | 推奨される具体的改修内容 |
|---|---|---|---|---|
| **CAPTIVE-01** | DI Lifetime | `Program.cs:175`<br>`ExternalOgpLinkPreviewProvider.cs` | シングルトンが Transient な `HttpClient` をキャプティブ化し、DNS 更新が滞る。 | `ExternalOgpLinkPreviewProvider` に `IHttpClientFactory` を注入するか、`builder.Services.AddHttpClient<ILinkPreviewProvider, ExternalOgpLinkPreviewProvider>()` の型付きクライアントに変更する。 |
| **INTERCEPT-01** | DI Wiring | `Program.cs:127-151`<br>`ApplicationDbContext.cs:832` | DI 登録された `ApplicationDbSaveChangesInterceptor`（`TimeProvider` 注入済み）が無視され、DbContext 側で `new` されている。 | `Program.cs` の `AddDbContext` および `AddDbContextFactory` において、`(sp, options) => options.AddInterceptors(sp.GetRequiredService<ApplicationDbSaveChangesInterceptor>())` を追加する。 |
| **LEAK-01** | Resource Leak | `TagHierarchyService.cs:105-118` | `import ./js/tagHierarchy.js` で動的インポートした `IJSObjectReference` が未破棄のまま放置されメモリリーク。 | `await using IJSObjectReference module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/tagHierarchy.js");` を適用する。 |
| **STATE-01** | UI State | `ServiceCollectionExtensions.cs:176-220` | 20件以上のダイアログ用 ViewModel が `AddScoped` 登録され、Blazor サーキット内で入力状態が残留・汚染。 | ダイアログ用 ViewModel（`TagAddViewModel`, `TagEditViewModel`, `ItemEditViewModel`, `BountyCreateViewModel` 等）を `AddTransient` に変更し、表示ごとにクリーンなインスタンスを生成する。 |
| **DATA-03** | Thread Safety | `Program.cs:163`<br>`UserDetailViewModel.cs:16` | Blazor サーキット内で `UserManager`（Scoped DbContext を保持）と DataProvider（Factory 生成）が競合する。 | ViewModel 内で `IServiceScopeFactory.CreateAsyncScope()` を利用し、短命なスコープ内で `UserManager` を操作するパターンへ移行する。 |
| **DATA-04** | Async EF | `ApplicationDbSaveChangesInterceptor.cs:37` | `SavingChangesAsync` の内部で同期的な LINQ クエリ（`FirstOrDefault()`）を実行（Sync-over-Async）。 | 非同期版クエリ（`FirstOrDefaultAsync()`）を実装し、`SavingChangesAsync` 内で非同期にバリデーションを行う。 |

---

### 4.2 優先度 2 (Milestone 3 スコープ): クリーンアーキテクチャ分離 & DTO境界確立

| 課題 ID | 分類 | 対象ファイル | 課題の概要 | 推奨される具体的改修内容 |
|---|---|---|---|---|
| **ORPHAN-01** | MVVM | `ReactionCommentDialog.razor.cs`<br>`CreateEdgeDialog.razor.cs` | 単体テストが存在する `ReactionCommentViewModel`, `CreateEdgeViewModel` が UI で使われずコードが重複。 | 各 Dialog razor.cs に対応する ViewModel を注入し、タイマーやタグ絞り込みのインライン実装を ViewModel へ統合する。 |
| **ARCH-01** | Clean Arch | `ResourceListViewModel.cs:11`<br>`HomeModels.cs`<br>`SystemTagEnsurer.cs` | ドメイン定数（`SystemTagIds`）やデータモデル（`TimelineFeedGroup`）が UI 配下に定義され、Services が UI を参照。 | `SystemTagIds`, `ReactionTagIds` を `SRNSMudApp/Models/` 配下に移動し、`TimelineFeedGroup` をドメインモデルへ昇格させ、依存方向を正規化する。 |
| **ARCH-02** | Clean Arch | `InternalLinkConversionService.cs`<br>`ItemListExportService.cs`<br>`ItemCardViewModel.cs` | 内部リンク解析・URL 抽出などの文字列パーサーが `ItemCardViewModel` にあり、Services が UI ViewModel を参照。 | テキスト処理・正規表現を `Services/Parsing/ContentParser.cs` などのドメインユーティリティへ抽出し、ViewModel 側がそれを呼び出す構造に改める。 |
| **ARCH-03** | Clean Arch | `ItemCardSplitCoordinator.cs`<br>`ItemCardTagCoordinator.cs`<br>`TaggingRequestActions.cs` | `Services/` 配下のクラスが `IDialogLauncher`, `ISnackbar` に依存し、UI ダイアログを起動。 | これらのコーディネーターを `Components/Coordinators/` などのプレゼンテーション補助レイヤーへ移動する。 |
| **SOLID-01** | DIP | `RiskAssessmentService.cs`<br>`AuthController.cs:20` | `RiskAssessmentService` にインターフェースが存在せず、具象クラスのまま DI 登録・注入されている。 | `IRiskAssessmentService` を抽出し、コントローラーおよびサービスコレクション登録をインターフェース経由に更新する。 |
| **SEC-02** | DTO Boundary | `UserDataProvider.cs:20`<br>`UserDetailViewModel.cs` | `ApplicationUser`（パスワードハッシュやセキュリティスタンプを含む）が生のまま UI 層へ返却されている。 | パブリック属性のみを保持する `UserProfileDto` または `UserSummaryDto` を新設し、UI へのエンティティ漏出を遮断する。 |

---

### 4.3 優先度 3: クエリ効率化 & パフォーマンス最適化ロードマップ

1. **PERF-01 (未読通知カウントのクエリ集約)**:
   - 現状: `GetUnreadCountAsync` が 15 個の個別 SQL クエリを発行し、数千行のデータを C# に読み込んでからカウント。
   - 推奨: `INotificationsDataProvider` に専用の `GetUnreadCountAsync` を追加し、SQL の `COUNT(*)` クエリ 1 本で未読件数を取得する。
2. **PERF-02 (ベクトル埋め込みのインメモリキャッシュ化)**:
   - 現状: タグ入力やサジェストのたびに全タグの 384次元ベクトル配列を SQL Server からダウンロード。
   - 推奨: アプリケーション起動時またはバックグラウンドで `ITagEmbeddingIndexService`（Singleton + `IMemoryCache`）を構築し、タグ更新時のみ差分同期を行う。DB 負荷とネットワーク転送を 99% 以上削減可能。
3. **PERF-03 (タグロック確認の一括バッチ化)**:
   - 現状: 一括削除対象のタグ N 個に対し、シリアルループで DbContext を都度生成してクエリ。
   - 推奨: `ITagLockService.GetLockedTagIdsAsync(IEnumerable<int> tagIds)` を新設し、`WHERE Id IN (...)` で 1 クエリに集約する。
4. **PERF-04 (参照系クエリへの AsNoTracking 徹底)**:
   - 現状: `AdminDataProvider`, `ContentReportService`, `ContractDataProvider` 等でエンティティ追跡が無駄に動作。
   - 推奨: 参照専用メソッドの LINQ に `.AsNoTracking()` を一括適用する。
5. **PERF-05 (JSON カラムの部分一致検索の解消)**:
   - 現状: `ItemKindJson.Contains(...)` により SQL `LIKE '%...%'` によるフルテーブルスキャンが発生。
   - 推奨: 頻繁に検索される属性を第1正規形のカラムまたはインデックス付きリレーションテーブルへ移行する。
6. **PERF-06 (CancellationToken の完全伝播)**:
   - 現状: `IDataProvider` や `ITagRelationService` の一部メソッドで `CancellationToken` が欠落。
   - 推奨: すべての非同期 DB アクセスメソッドに `CancellationToken cancellationToken = default` を追加し、クライアント切断時の不要な処理を即座に中断させる。

---

## 5. 最終検証結果マトリクス (Verification Results Summary)

Milestone 1 の実装完了後、ソリューション全体に対して実行された最終検証コマンドと結果の完全なエビデンスを以下に記録します。

### 5.1 検証コマンド実行結果マトリクス

| 検証項目 | 実行コマンド | 終了コード | 実行結果・詳細 | 判定 |
|---|---|---|---|---|
| **コードフォーマット検証** | `dotnet format --diagnostics IDE0055 --verify-no-changes` | 0 | 差分・違反 0件。全プロジェクトで規約遵守。 | **PASS** |
| **ソリューションビルド検証** | `dotnet build` | 0 | エラー 0件、新規警告 0件（既存のログデリゲート警告8件のみ）。 | **PASS** |
| **Milestone 1 単体・並行性・敵対的テスト** | `dotnet test SRNSMudApp.Tests/SRNSMudApp.Tests.csproj --filter "FullyQualifiedName~PushNotificationTests\|FullyQualifiedName~PushNotificationAdversarialTests\|FullyQualifiedName~RightAssetPurchaseServiceTests\|FullyQualifiedName~ExternalTokenVerificationConcurrencyTests\|FullyQualifiedName~ExternalTokenVerificationServiceTests"` | 0 | **45 / 45 テスト合格** (失敗: 0, スキップ: 0, 実行時間: 10秒)。 | **PASS** |
| **E2E システムテスト検証** | `dotnet test SRNSMudApp.E2ETests/SRNSMudApp.E2ETests.csproj` | 0 | **1 / 1 テスト合格** (失敗: 0, スキップ: 0, 実行時間: 9秒)。 | **PASS** |
| **独立フォレンジック監査** | 監査エージェント (`auditor_m1_it2_1`) による静的・動的検査 | - | ハードコードなし、ファサードなし、実DB/HTTPサーバー検証済み。 | **CLEAN** |

### 5.2 ルール適合性チェックリスト
- [x] **Blazor UI 責務とドメインロジックの分離**: コンポーネントからの直接 DB アクセス 0 件。
- [x] **`IDialogService` 直接利用の排除**: すべて `IDialogLauncher` を経由。
- [x] **非同期 I/O とリソース破棄**: 修正箇所はすべて非同期実装、CancellationToken 伝播。
- [x] **フォーマット制約遵守**: `dotnet format --diagnostics IDE0055` を使用し、フルフォーマット実行を回避。
- [x] **テストの追加と回帰テスト実行**: Adversarial テストを含む 45 件のテストおよび E2E テストが全件合格。
- [x] **一時スクリプト・ゴミ成果物のクリーンアップ**: リポジトリ作業ツリーに不要な一時ファイルを一切残存させずクリーンな状態を維持。

---
*報告書作成完了: 2026-10-02T18:30:00Z | Teamwork Architecture Worker (`worker_m4_report`)*
