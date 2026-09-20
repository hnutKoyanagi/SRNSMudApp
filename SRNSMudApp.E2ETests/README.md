# SRNSMudApp.E2ETests

Playwright によるE2Eテストプロジェクト。Phase 1〜4 のテスト移行プロジェクト完了時点での
棚卸し結果（何が・なぜ残っているか）を記録する。

## テストの役割分担（判断基準）

| プロジェクト | 役割 |
|---|---|
| `SRNSMudApp.Tests` | xUnit + bUnit によるコンポーネントテスト／サービステスト。**新機能追加時は基本的にここに書く** |
| `SRNSMudApp.E2ETests` | Playwright によるE2Eテスト。**実ブラウザAPI依存・実認証パイプライン検証など、他の手段で代替できないものだけに限定する** |

新しいテストを書く際の判断基準:

- **ブラウザのネイティブAPIに依存するか？**（IntersectionObserver、WebAuthn/CDP、実JSグローバル関数、
  実SignalR接続、Cookie発行を伴う認証リダイレクト）→ はい = E2E
- **Blazorのレンダリング結果とDB状態の検証だけで済むか？** → はい = bUnit コンポーネントテスト
- 純粋なロジック（embedding類似度、JSONエクスポート構築、URL状態同期）→ サービス層に切り出してユニットテスト

## 現存ファイル一覧と維持理由

### テストインフラ

| ファイル | 役割 |
|---|---|
| `CustomWebApplicationFactory.cs` | Testcontainers(MSSQL) + WebApplicationFactory のテスト基盤。削除しない |
| `SharedTestServerFixture.cs` | アセンブリ全体で1つのファクトリ（MSSQLコンテナ1つ）を共有する NUnit `[SetUpFixture]`（Phase 5-4）。ダミーGoogle ClientId もここで設定 |
| `WebAuthnTestHelpers.cs` | Passkey系2テストの共通セットアップ/後処理ヘルパー（Phase 5-2） |

### 維持対象のE2Eテスト

| ファイル | 検証内容 | E2Eとして残す理由 |
|---|---|---|
| `PasskeyLoginE2ETests.cs` | WebAuthnによるパスキー登録・ログイン | CDPセッション＋仮想オーセンティケータなど実ブラウザAPI依存（真のE2E検証） |

### 移行済み・削除済み（参考）

| 元ファイル | 移行先 |
|---|---|
| `PasskeyRenameE2ETests.cs` | `SRNSMudApp.Tests/AccountPagesTests.cs`（RenamePasskey の StaticTextField モデルバインディング属性生成検証）（Phase 8） |
| `ExternalLoginButtonsE2ETests.cs` | `SRNSMudApp.Tests/AccountPagesTests.cs`（Login コンポーネントの Google/LINE/GitHub ボタン描画および Google ボタン JS 呼び出し検証）（Phase 8） |
| `GlobalPopoverE2ETests.cs` | `SRNSMudApp.Tests/Components/Pages/PageRenderSmokeTests.cs`（Home, TagSearch, TagList, TagTree, ItemList, UserSearch の bUnit レンダリングスモーク）（Phase 8） |
| `ItemListFocusE2ETests.cs` | `SRNSMudApp.Tests/Components/Item/ItemListFocusTests.cs`（ItemCard の OnElementFocusedByScroll 呼び出しによる自動フォーカス・URL 更新検証）（Phase 8） |
| `LoginAndPostItemE2ETests.cs` | `SRNSMudApp.Tests/Auth/ExternalLoginCallbackIntegrationTests.cs`（In-Memory WebApplicationFactory による Google/LINE/GitHub モックコールバック Cookie 発行検証）＋ `Components/Pages/AuthCallbackTests.cs`（Phase 8） |
| `ItemReactionE2ETests.cs` | `Components/UI/ReactionBarTests.cs`（ボタン・チップ描画・ナビゲーション）＋ `Services/ItemReactionServiceTests.cs`（投票・重み計算）＋ `Services/ItemCardVoteCoordinatorTests.cs`（Phase 7） |
| `ItemQuoteE2ETests.cs` | `Components/Item/QuotedItemListDialogTests.cs` ＋ `Components/UI/ItemCardQuoteFocusTests.cs` ＋ `ItemQuoteServiceTests.cs`（Phase 7） |
| `ItemSplitRequestE2ETests.cs` | `Components/UI/ItemCardSplitRequestTests.cs` ＋ `Services/ItemSplitServiceTests.cs`（Phase 7） |
| `UserFollowE2ETests.cs` | `Components/User/UserDetailFollowTests.cs` ＋ `Services/UserDataProviderFollowTests.cs`（Phase 7） |
| `ItemPrivateModeE2ETests.cs` | `Components/UI/ItemCardPrivacyBadgeTests.cs` ＋ `Services/ItemPrivateModeTests.cs`（Phase 7） |
| `ItemListTagFilterE2ETests.cs` | `Components/Item/ItemListTagSearchTests.cs` ＋ `Services/ItemListDataProviderTests.cs`（Phase 7） |
| `ItemDetailScrollE2ETests.cs` | `Components/Item/ItemDetailThreadTests.cs`（`scrollToElement` 呼び出し検証）（Phase 7） |
| `TagDiagramContextE2ETests.cs` / `TagDiagramQuotedItemE2ETests.cs` | `Services/TagDiagramDataProviderTests.cs` ＋ `Components/Diagram/TagDiagramPageTests.cs`（Phase 7） |
| `ItemReplyNotificationE2ETests.cs` | `Components/UI/ItemReplyThreadTests.cs` ＋ `Components/UI/NotificationBadgeTests.cs` ＋ `ItemReplyServiceTests.cs` ＋ `Services/NotificationServiceTests.cs`（Phase 7） |
| `AddItemMentionE2ETests.cs` / `AddItemUserMentionE2ETests.cs` | `Components/Item/AddItemTests.cs` ＋ `tributeInteropCore.test.js`（Phase 7） |
| `AddItemImeCompositionE2ETests.cs` / `ImeTestHelpers.cs` | `Components/Item/AddItemTests.cs` ＋ `tributeInteropCore.test.js`（Phase 7） |
| `InspectItem12006Test.cs` / `RegexTest.cs` | 一時デバッグコード削除 / `SRNSMudApp.Tests/RegexTest.cs` に集約（Phase 7） |
| `DuplicateTaggingRequestCancelE2ETests.cs` | `Components/Contract/DuplicateTaggingRequestCancelTests.cs`（承認の分離性 + 送信済みからの取り下げを2ケースに分解）（Phase 6） |
| `ItemDetailTagWeightE2ETests.cs` | `Components/Item/ItemDetailTagWeightTests.cs`（Weight減ボタン→アクション列表示＋DB反映）（Phase 6） |
| `TagDeletionTrackingE2ETests.cs` | `Components/Tag/TagDeletionTrackingTests.cs`（タグ追加ダイアログ→チップ削除・トラッキング例外回帰。IDialogLauncher モック活用により専用コンテナ起動も解消）（Phase 6） |
| `VectorSearchE2ETests.cs`（4ケース+無関係コード） | `SRNSMudApp.Tests/TagEmbeddingServiceTests.cs`（実LocalEmbedderでコサイン類似度のコア検証）＋ `Components/Tag/TagSearchTests.cs`（UI配線1ケースのみに集約）（Phase 4-7） |
| `ItemDetailDeepLinkE2ETests.cs` | `Components/Item/ItemDetailDeepLinkTests.cs`（状態⇔URL双方向）（Phase 4-5） |
| `ItemListExportE2ETests.cs` | `Components/Item/ItemListExportTests.cs`（JS interop傍受によりブラウザ不要）（Phase 4-3） |
| `MudPopoverE2ETests.cs` | `Components/User/UserSearchTests.cs` に入力→候補表示ケースを追加（Phase 4-2） |
| `ContractManagementE2ETests.cs`, `PublicOfferE2ETests.cs`, `ContractAndOfferScenarioE2ETests.cs` | `Components/PublicOffer/*Tests.cs`（Phase 3） |
| `NotificationsTagRequestE2ETests.cs` | `Components/Notifications/NotificationsPageTests.cs`（Phase 3） |
| `ItemListAutocomplete/TagSearch/AddItem/ImportTag/UserDetailTree/TagListConcurrency` 系 | Phase 2 で bUnit 化済み |

## Before / After サマリー

| 指標 | Before（初期状態） | After（Phase 7 移行後） |
|---|---|---|
| E2Eテストファイル数 | 30+ | 6テストクラス＋基盤3ファイル |
| E2Eテストケース数 | 37+ | 14 |
| E2E実行時間 | 約3〜4分（コンテナ起動×クラス数） | 約50秒（コンテナ起動1回・Phase 5-4後の実測） |
| bUnit/サービステスト | 120 | 175+ |
| 単体テスト実行時間 | 数秒 | 約2秒 |


### カバレッジ（移行対象コンポーネント、Phase 5-6 実測）

| コンポーネント/サービス | 行カバー率 |
|---|---|
| CreatePublicOfferDialog | 90% |
| TagSearch | 76% |
| NotificationsPage | 74% |
| TriggerPublicOfferDialog | 69% |
| ItemDetail / PublicOfferBoard | 約64% |
| ResourceList | 58% |
| ItemList | 57% |
| TagEmbeddingService | 100% |
