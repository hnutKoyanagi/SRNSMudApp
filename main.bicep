// ============================================================
//  SRNSMudApp — 無料構成 (App Service F1 + Azure SQL 無料オファー)
//  デプロイ先スコープ: リソースグループ
//    az deployment group create -g srns -f main.bicep -p main.bicepparam
// ============================================================

targetScope = 'resourceGroup'

@description('Web アプリ名。<name>.azurewebsites.net になるためグローバル一意が必要。')
param name string = 'srns'

@description('リソースの作成先リージョン。')
param location string = resourceGroup().location

@description('App Service プランの SKU。F1 (無料) または B1 (Basic)。クォータ 0 エラーを回避する場合は B1 を指定してください。')
@allowed([
  'F1'
  'B1'
])
param appServiceSku string = 'F1'

@description('App Service プラン名。')
param hostingPlanName string = 'ASP-${name}-${toLower(appServiceSku)}'

@description('ランタイム。App Service Linux の linuxFxVersion 形式。')
param linuxFxVersion string = 'DOTNETCORE|11.0'

@description('論理 SQL サーバー名（グローバル一意）。')
param serverName string = '${name}-server'

@description('データベース名。')
param databaseName string = '${name}-database'

@description('SQL 管理者ログイン名。')
param serverUsername string = '${name}-server-admin'

@description('照合順序。')
param collation string = 'Japanese_XJIS_140_CI_AS_UTF8'

@description('''
サーバーレス General Purpose の SKU。
無料オファー (useFreeLimit) は GP_S_Gen5 系のみ対象。
毎月 10 万 vCore 秒が無料枠なので、vCore 数が小さいほど稼働できる時間は長い。
''')
param sqlDbSkuName string = 'GP_S_Gen5_2'

@description('自動一時停止までの分数。-1 で無効。無料枠を節約するため既定は 60 分。')
param autoPauseDelay int = 60

@description('''
無料枠（毎月 10 万 vCore 秒 + 32GB ストレージ）を使うかどうか。
※ 1 サブスクリプションにつき 1 データベースのみ設定可能。
''')
param useFreeLimit bool = true

@description('無料枠を使い切った後の挙動。AutoPause = 課金せず翌月まで停止 / BillOverUsage = 超過課金。')
@allowed([
  'AutoPause'
  'BillOverUsage'
])
param freeLimitExhaustionBehavior string = 'AutoPause'

@description('接続文字列に使う名前。appsettings.json の ConnectionStrings と合わせる。')
param connectionStringName string = 'DefaultConnection'

@description('ASPNETCORE_ENVIRONMENT の値。')
param aspNetCoreEnvironment string = 'Production'

@description('開発端末から SQL へ直接つなぐ場合の IP（EF migrations 実行用など）。空なら規則を作らない。')
param clientIpAddress string = ''

var maxSizeBytes = 34359738368 // 32 GB（無料枠の上限）
var serverPassword = 'Az!9_${take(uniqueString(resourceGroup().id, subscription().id), 8)}_${toUpper(take(uniqueString(subscription().id, resourceGroup().name), 6))}'

// ------------------------------------------------------------
//  App Service プラン（F1 / B1 Linux）
// ------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: hostingPlanName
  location: location
  kind: 'linux'
  sku: {
    name: appServiceSku
    tier: appServiceSku == 'F1' ? 'Free' : 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true // Linux
  }
}

// ------------------------------------------------------------
//  論理 SQL サーバー
// ------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: serverUsername
    #disable-next-line use-secure-value-for-secure-inputs // 接続には Managed Identity を使用するため自動生成値で安全に初期化
    administratorLoginPassword: serverPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // F1 は VNet 統合不可のため公開エンドポイント経由
  }
}

// Azure サービス（App Service を含む）からの接続を許可
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// 手元の PC から接続したい場合のみ
resource allowClientIp 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (!empty(clientIpAddress)) {
  parent: sqlServer
  name: 'AllowDevClient'
  properties: {
    startIpAddress: clientIpAddress
    endIpAddress: clientIpAddress
  }
}

// ------------------------------------------------------------
//  データベース（サーバーレス + 無料オファー）
// ------------------------------------------------------------
resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: sqlDbSkuName
    tier: 'GeneralPurpose'
  }
  properties: {
    collation: collation
    maxSizeBytes: maxSizeBytes
    autoPauseDelay: autoPauseDelay
    minCapacity: json('0.5')
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local' // 無料オファーはローカル冗長のみ
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: useFreeLimit ? freeLimitExhaustionBehavior : null
  }
}

// ------------------------------------------------------------
//  Web アプリ
// ------------------------------------------------------------
resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: name
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: true // Blazor Server の回線を同一インスタンスに固定
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      alwaysOn: appServiceSku != 'F1' // F1 では有効化不可、B1 では常時接続のため true
      webSocketsEnabled: true
      healthCheckPath: null
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspNetCoreEnvironment
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
      connectionStrings: [
        {
          name: connectionStringName
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Authentication=Active Directory Managed Identity;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        }
      ]
    }
  }
  dependsOn: [
    sqlDb
    allowAzureServices
  ]
}

// ------------------------------------------------------------
//  SQL Server の Microsoft Entra 管理者（App Service の Managed Identity）
// ------------------------------------------------------------
resource sqlEntraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: site.name
    sid: site.identity.principalId
    tenantId: subscription().tenantId
  }
}

output siteUrl string = 'https://${site.properties.defaultHostName}'
output sitePrincipalId string = site.identity.principalId
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output connectionStringSettingName string = connectionStringName
