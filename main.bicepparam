using './main.bicep'

param name = 'srns'
// param location = 'japaneast' // 未指定の場合はデプロイ先リソースグループのリージョン（resourceGroup().location）に自動追従します
param appServiceSku = 'B1' // B1: Basic (VNet統合有効)
param linuxFxVersion = 'DOTNETCORE|11.0'

param serverName = 'srns-server'
param databaseName = 'srns-database'
param serverUsername = 'srns-server-admin'

// SQL サーバーの初期管理者パスワードは main.bicep 内で安全な既定値が自動生成されるため入力不要です。
// （明示的に指定したい場合のみ以下を有効化してください）
// param serverPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')

param sqlDbSkuName = 'GP_S_Gen5_2'
param useFreeLimit = true
param freeLimitExhaustionBehavior = 'AutoPause'
param autoPauseDelay = 60

param connectionStringName = 'DefaultConnection'
param aspNetCoreEnvironment = 'Production'

// EF Core の migration をローカルから流す場合のみ自分の IP を入れる
param clientIpAddress = ''

// Google OAuth Client ID
param googleClientId = '890065771342-2ruam1rjo1ppvjs5fe11n4eh7mp7t9vv.apps.googleusercontent.com'
