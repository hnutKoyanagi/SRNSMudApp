# SRNSWebApp

Blazor Server + MudBlazor 製のタグベースSNSアプリケーション（SRNSMudApp）。

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fkoyakei%2FSRNSMudApp%2Fmaster%2Fazuredeploy.json)

## Azure へのデプロイ (Free Tier)

上記「Deploy to Azure」ボタンから、Azure の無料枠（Free Tier）を利用してワンクリックで本アプリケーションをデプロイできます。

### 構成リソース
- **Azure App Service Plan**: `B1` (Basic) を既定値として設定（※F1 無料枠のクォータ `0` 制限を回避するため。F1 クォータをお持ちの場合は `appServiceSku` で `F1` も選択可能）
- **Azure App Service (Web App)**: Blazor Server 用に WebSockets を有効化、GitHub からの自動ビルド・デプロイ (`sourcecontrols`)
- **Azure SQL Database**: `GP_S_Gen5_1` (Serverless Free Offer) - 毎月 100,000 vCore 秒 & 32GB ストレージ無料（アイドル時自動一時停止: 60分）

### デプロイ時に入力が必要な項目
1. **リソース グループ**: 既存または新規作成
2. **sqlAdministratorLoginPassword**: Azure SQL Server 管理者パスワード（大文字・小文字・数字・記号を含む8文字以上）
3. **systemUserInitialPassword**: アプリ内 `system` 管理者アカウントの初期パスワード
4. **googleClientId**（任意）: Google OAuth 2.0 クライアント ID（`xxxxxx.apps.googleusercontent.com`）。未入力でデプロイし、後から設定することも可能です。

### Google 認証 (Google Auth Platform) の設定手順
Google ログインを有効化するには、[Google Cloud Console](https://console.cloud.google.com/) で以下の設定を行います。

1. **認証情報の作成**:
   - 「API とサービス」>「認証情報」>「認証情報を作成」>「OAuth クライアント ID」
   - アプリケーションの種類: **ウェブ アプリケーション**
2. **承認済みの JavaScript 送信元**:
   - `https://<作成されたアプリ名>.azurewebsites.net` を追加（※末尾のスラッシュは不要）
3. **クライアント ID の反映**:
   - **デプロイ時**: テンプレートの `googleClientId` 入力欄に指定
   - **デプロイ後（または変更時）**: Azure Portal の App Service > **「設定」>「環境変数」**（または「構成」）にて、以下のキーを追加/編集して保存（再起動）:
     - 名前: `Authentication__Google__ClientId`
     - 値: `<取得したクライアントID>`

> 💡 **`SubscriptionIsOverQuotaForSku (Limit: 0)` エラーが出る場合:**
> 東日本 (japaneast) 等の一部混雑リージョンでは、Azure 側のキャパシティ制限により F1 (無料) プランのクォータ上限が 0 に制限されていることがあります。
> - **完全無料で動かしたい場合**: デプロイ先リージョン（リソースグループの場所またはテンプレートの `location`）に、無料枠の空きが多い **`East US 2` (米国東部 2)** や **`Central US` (米国中央)** を選択してください。
> - **同一リージョンで動かしたい場合**: テンプレートの `appServiceSku` パラメータを **`B1`** (Basic) に変更してください。


## テスト戦略

テストは2プロジェクトに役割分担されている。新しくテストを書く際は以下の判断基準に従うこと。

| プロジェクト | フレームワーク | 役割 |
|---|---|---|
| `SRNSMudApp.Tests` | xUnit + bUnit | コンポーネントテスト／サービステスト。**新機能追加時は基本的にここに書く** |
| `SRNSMudApp.E2ETests` | Playwright (NUnit) | E2Eテスト。実ブラウザAPI依存など、他の手段で代替できないものだけに限定する |

※ 旧 `SRNSMudApp.ComponentTests` プロジェクトは Phase 1 のテスト統合時に削除済み
（bUnit テストはすべて `SRNSMudApp.Tests` に集約）。

### 新しいテストを書く際の判断基準

1. **ブラウザのネイティブAPIに依存するか？**
   - IntersectionObserver / WebAuthn(CDP) / 実JSグローバル関数 / 実SignalR接続 /
     Cookie発行を伴う認証リダイレクト → **E2E (`SRNSMudApp.E2ETests`)**
2. **Blazorのレンダリング結果とDB状態の検証だけで済むか？**
   - → **コンポーネントテスト (`SRNSMudApp.Tests/Components/...`)**
3. 純粋なロジック（類似度計算、JSON生成、状態同期など）
   - UIから切り出して**サービステスト (`SRNSMudApp.Tests`) で直接検証する**

### 実行方法

```bash
# 軽量（毎日の開発用・約2秒）
dotnet test SRNSMudApp.Tests

# 重い（Testcontainers で MSSQL コンテナを起動。アセンブリで1回のみ・約1分）
dotnet test SRNSMudApp.E2ETests
```

E2Eテストの詳細な棚卸し（何が・なぜ残っているか）は
[SRNSMudApp.E2ETests/README.md](SRNSMudApp.E2ETests/README.md) を参照。

## CI について

現時点で `.github/workflows/` 配下にCIパイプラインは存在しない。
CIを追加する場合の目安:

- `dotnet test SRNSMudApp.Tests`: 数秒。全PRで必ず実行。
- `dotnet test SRNSMudApp.E2ETests`: Testcontainers(Docker) が必要。
  共有フィクスチャによりMSSQLコンテナ起動はアセンブリで1回のみ（実績: 約50秒）。
  残存テスト数が少ないため、単一ジョブでの逐次実行で十分。
