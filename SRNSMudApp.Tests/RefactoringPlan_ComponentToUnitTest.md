# 全コンポーネントテストの単体テスト化（ViewModel集約）リファクタリング全社計画書

## 1. 目的と基本原則

### 1.1 目的
`SRNSMudApp.Tests/Components` 配下に存在する 114 のテストファイルにおいて、bUnit を用いた DOM 探索・レンダリング待機・UI クリック経由のテストが多用され、**テストの実行速度低下（vstest タイムアウト等）**、**UI 変更時の脆さ (Fragile Tests)**、**関心事の混在** が生じています。
本計画では、この構造を **「UI（.razor）は極限まで薄くし、ロジックはピュア C# の ViewModel に集約して xUnit + Moq で直接単体テストする」** という MVVM コンセプトをリポジトリ内の全ファイルに適用します。

### 1.2 コア原則
1. **ロジックの非 UI 化 (Pure C# ViewModel):**
   - サービス呼び出し、権限チェック、ステータス分岐、URL クエリ同期、フィルタ計算はすべて ViewModel（または State / Service）に配置する。
   - ViewModel は `Microsoft.AspNetCore.Components`（特に `IRenderedComponent` や DOM 関連）に一切依存しない。
2. **テストピラミッドの適正化:**
   - **単体テスト (xUnit + Moq):** 85%〜90% のビジネスロジック・ユースケース・状態遷移をカバー。ミリ秒単位で高速・決定論的に実行。
   - **コンポーネントテスト (bUnit):** 10%〜15% に限定。ViewModel と Razor 間の `@bind`、EventCallback の疎通、条件付き CSS/Class 出力スモークテストのみ。
   - **E2E テスト (Playwright):** 画面全体の統合フローを担保（既存の `SRNSMudApp.E2ETests`）。
3. **重複・無駄な統合テストの排除:**
   - 親コンポーネント（例: `ItemDetail`）をマウントして子コンポーネント（例: `ItemTagChip`）のボタンをクリックしサービス呼び出しを検証するような多層テストは廃止し、責務を持つコンポーネント / ViewModel の単体テストへ移譲する。

---

## 2. 全 114 ファイルの分類と移行カタログ

リポジトリ内の全テストファイルを以下の 4 カテゴリに分類し、段階的に移行を推進します。

### カテゴリ A: サービス呼び出し・ビジネスロジック検証（最優先移行対象: 約 45 ファイル）
DOM のボタンクリックからサービス（`IItemTagService`, `ITaggingService`, `IItemReplyService` 等）が呼ばれることを検証しているテスト群。

| 対象ディレクトリ | 主なテストファイル | 移行先 / 方針 |
|---|---|---|
| `Components/Item/` | `ItemDetailTagWeightTests.cs`<br>`ItemDetailThreadTests.cs`<br>`AddItemTests.cs`<br>`ItemDetailVisibilityTests.cs`<br>`ItemListExportTests.cs` | `ItemTagChipViewModelTests`<br>`ItemDetailThreadViewModelTests`<br>`AddItemViewModelTests`<br>※bUnit を全廃し xUnit + Moq の直接呼び出しへ移行 |
| `Components/Tag/` | `ItemTagChipTests.cs`<br>`TagDetailPermissionRequestTests.cs`<br>`TaggingRequestApprovalTests.cs`<br>`TaggingRequestRejectTests.cs`<br>`TaggingRequestCancelTests.cs`<br>`TaggingRequestReplyTests.cs`<br>`TagDetailDeleteTests.cs` | `TagActionViewModelTests`<br>`TagRequestViewModelTests`<br>※ステータス更新・通知・エラーハンドリングを ViewModel で完結 |
| `Components/UI/` | `ItemCardReplyCountTests.cs`<br>`ItemCardAdminActionTests.cs`<br>`ItemCardSplitRequestTests.cs`<br>`ItemCardTagAddTests.cs`<br>`ReactionBarTests.cs` | `ItemCardViewModelTests`<br>`ReactionBarViewModelTests`<br>※リプライ送信、アップボート/ダウンボート、分割リクエスト発行ロジックを単体テスト化 |
| `Components/Contract/` | `DuplicateTaggingRequestCancelTests.cs`<br>`ProposeContractDialogTests.cs` | `ContractManagementViewModelTests`<br>※契約承認・拒否・重複キャンセルロジックの単体テスト |
| `Components/User/` | `UserDetailFollowTests.cs`<br>`MakeMeAdminTests.cs`<br>`UserManagementTests.cs` | `UserManagementViewModelTests`<br>※フォロー/解除、ロール昇格ロジックの単体テスト |
| `Components/Admin/` | `ReportManagerTests.cs`<br>`TagManagementTests.cs`<br>`RequireConfirmedAccountTests.cs` | `ReportManagerViewModelTests`<br>`TagManagementViewModelTests`<br>※通報処理、承認処理ロジックの単体テスト |
| `Components/Diagram/` | `ItemNodeTests.cs`<br>`TagDiagramCanvasTests.cs`<br>`TagDiagramPageTests.cs` | `ItemNodeViewModelTests`<br>`TagDiagramCanvasViewModelTests`<br>※ノード選択・エッジ生成・位置計算の単体テスト |

### カテゴリ B: 状態遷移・クエリ・フィルタ・表示計算（約 35 ファイル）
URL クエリパラメータの解析、リストのフィルタリング、オートコンプリート候補生成を検証しているテスト群。

| 主なテストファイル | 現状の課題 | 移行方針 |
|---|---|---|
| `ItemListTagSearchTests.cs`<br>`ItemListAutocompleteTests.cs`<br>`TagSearchTests.cs`<br>`TagAutocompleteTests.cs` | bUnit で Input 入力イベントを発火させて結果テキストをアサートしている | `TagSearchQuery`、`ItemTagTableViewModel`、`TagSearchViewModel` などの純粋関数・純粋メソッドに対するデータ駆動テスト (`[Theory]`, `[InlineData]`) に完全移行 |
| `ItemDetailDeepLinkTests.cs`<br>`ItemListQueryStateTests.cs`<br>`ItemDetailQueryStateTests.cs` | DOM レンダリングを伴って URL 同期を検証している | `ItemDetailQueryStateFactoryTests` などのピュア xUnit テストで URL 生成とパースを検証 |
| `AsyncPageStateTests.cs`<br>`AsyncPageViewTests.cs` | 非同期ロード状態の UI 表示切替 | `AsyncPageState` の Union 型遷移の単体テストに寄せる |

### カテゴリ C: ダイアログ・モーダル・インタラクション（約 20 ファイル）
`IDialogService` / `IDialogLauncher` を用いたポップアップとユーザー入力の検証。

| 主なテストファイル | 移行方針 |
|---|---|
| `ConfirmDeleteDialogTests.cs`<br>`ItemEditDialogTests.cs`<br>`ReactionCommentDialogTests.cs`<br>`RequestTagPermissionDialogTests.cs`<br>`TagAddDialogTests.cs`<br>`TagResolutionDialogTests.cs`<br>`TagLinkReplaceDialogTests.cs`<br>`TriggerPublicOfferDialogTests.cs` | 1. ダイアログ入力値のバリデーションロジックは Validator または ViewModel でテスト。<br>2. ダイアログを呼ぶ側のコンポーネントは `IDialogLauncher` のモックを用いて「適切なパラメータでダイアログを起動したか」「OK/Cancel 返却時に想定通りのサービスが呼ばれたか」のみを検証。 |

### カテゴリ D: 純粋レンダリング・スモークテスト（約 14 ファイル、維持・スリム化）
Razor 構文エラーや CSS クラス、マークアップ整合性を最低限保証するテスト。

| 主なテストファイル | 移行方針 |
|---|---|
| `PageRenderSmokeTests.cs`<br>`ResourceListRenderingTests.cs`<br>`NotificationBadgeTests.cs`<br>`TagCardChipTests.cs` | - bUnit のままで維持するが、サービス呼び出しの検証は一切行わない。<br>- 偽の静的データを注入し、コンポーネントが例外を投げずにレンダリングされることのみを確認するシンプルな構成に軽量化。 |

---

## 3. 実装アーキテクチャ規約

### 3.1 ViewModel の責務設計
各コンポーネントに対応する ViewModel は以下のいずれかの形態をとる：

1. **ステートレス ViewModel（静的クラス）:**
   - 用途: 色判定、スタイル計算、フィルタ関数、オートコンプリート候補計算。
   - 例: `ItemCardViewModel.GetItemScore()`, `ItemTagTableViewModel.FilterFunc()`
2. **ステートフル ViewModel（クラス / DI 登録 または コンポーネント内保持）:**
   - 用途: サービス呼び出しを伴う操作、非同期データロード、ページ状態管理。
   - 特徴: コンストラクタで必要なサービス（`IItemTagService`, `ISnackbar` 等）を受け取り、UI に非依存なメソッドを提供する。

### 3.2 テストコードの移行パターン比較

#### 【Before】bUnit による重厚なテスト（壊れやすく遅い）
```csharp
[Fact]
public void ClickingDecreaseWeightButton_CallsServiceAndUpdatesWeight()
{
    // 大量の DI モック設定、JSInterop、MudServices、MudPopoverProvider
    _itemDetailDataMock.Setup(d => d.GetItemDetailAsync(itemId)).ReturnsAsync(...);
    _itemTagServiceMock.Setup(s => s.UpdateTagWeightAsync(relation.Id, -1, UserId)).ReturnsAsync(UpdateWeightResult.Success);

    // 画面全体をレンダリングし非同期待機
    IRenderedComponent<ItemDetail> cut = _ctx.Render<ItemDetail>(...);
    cut.WaitForState(() => !cut.Markup.Contains("mud-progress-circular"));

    // DOM セレクタでボタンを探してクリック
    IElement decreaseButton = cut.FindAll("button[title='Weightを減らす']")[0];
    decreaseButton.Click();

    // 検証
    _itemTagServiceMock.Verify(s => s.UpdateTagWeightAsync(relation.Id, -1, UserId), Times.Once);
}
```

#### 【After】ViewModel 単体テスト（高速・堅牢・明確）
```csharp
[Fact]
public async Task DecreaseWeightAsync_WhenOwner_CallsServiceAndNotifies()
{
    // Arrange: 必要なサービスのみモック化（bUnit・DOM なし）
    var itemTagServiceMock = new Mock<IItemTagService>();
    itemTagServiceMock
        .Setup(s => s.UpdateTagWeightAsync(50, -1, "user-1"))
        .ReturnsAsync(UpdateWeightResult.Success);

    var viewModel = new ItemTagChipActionsViewModel(itemTagServiceMock.Object);

    // Act: メソッドを直接実行
    var result = await viewModel.DecreaseWeightAsync(tagRelationId: 50, currentUserId: "user-1", ownerId: "user-1");

    // Assert: 結果とサービス呼び出しを直接検証
    Assert.True(result.IsSuccess);
    itemTagServiceMock.Verify(s => s.UpdateTagWeightAsync(50, -1, "user-1"), Times.Once);
}
```

---

## 4. 全ファイル適用の実施ロードマップ

| フェーズ | 対象領域 | 主な成果物 |
|---|---|---|
| **Phase 1 (即時着手)** | **Item & Tag コア領域** | `ItemDetailTagWeightTests`, `ItemDetailThreadTests`, `ItemTagChipTests` の単体テスト化。<br>`ItemTagChipActionsViewModel` の新設・リファクタリング。 |
| **Phase 2** | **UI 共通コンポーネント領域** | `ItemCard*Tests`, `ReactionBarTests`, `NotificationBadgeTests` の bUnit 依存削減。<br>`ItemCardViewModel` へのアクション統合。 |
| **Phase 3** | **Tag 管理・契約・提案領域** | `TagDetail*Tests`, `TaggingRequest*Tests`, `Contract*Tests` の ViewModel 化と単体テスト移行。 |
| **Phase 4** | **ユーザー・管理者・ダイアログ領域** | `UserManagement*Tests`, `Admin*Tests`, `*DialogTests` の入力・権限バリデーション単体テスト化。 |
| **Phase 5** | **ダイアグラム・残存テストクリーンアップ** | `Diagram*Tests` の移行、不要となった bUnit ヘルパーや重複テストの削除、テスト実行時間・安定性の計測。 |
