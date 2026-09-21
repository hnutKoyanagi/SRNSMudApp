using './main.bicep'

param name = 'srns'
param location = 'West US 3'
param appServiceSku = 'F1' // クォータ 0 エラーが出る場合は 'B1' に変更
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
