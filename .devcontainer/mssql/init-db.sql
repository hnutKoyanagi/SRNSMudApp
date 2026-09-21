-- ==============================================================================
-- init-db.sql
-- ローカル開発用 Docker SQL Server コンテナの初期化スクリプト
-- ※ sa (sysadmin) 権限で実行されます。
-- ==============================================================================

-- 1. CLR 統合の有効化 (SQL Server コンテナ環境用)
IF SERVERPROPERTY('EngineEdition') NOT IN (5, 8)
BEGIN
    PRINT 'Enabling CLR on SQL Server instance...';
    EXEC sp_configure 'show advanced options', 1;
    RECONFIGURE;
    EXEC sp_configure 'clr enabled', 1;
    RECONFIGURE;
END
GO

-- 2. データベースの作成
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SRNSMudApp')
BEGIN
    PRINT 'Creating database SRNSMudApp...';
    CREATE DATABASE [SRNSMudApp];
END
GO

-- 3. アプリケーション専用ログインの作成 (master)
USE [master];
GO

IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'srns_app')
BEGIN
    PRINT 'Creating login srns_app...';
    CREATE LOGIN [srns_app] WITH PASSWORD = N'LocalAppPassword123!', CHECK_POLICY = OFF;
END
GO

-- 4. データベースユーザーの作成と db_owner 権限付与
USE [SRNSMudApp];
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'srns_app')
BEGIN
    PRINT 'Creating user srns_app in SRNSMudApp...';
    CREATE USER [srns_app] FOR LOGIN [srns_app];
END
GO

PRINT 'Granting db_owner role to srns_app...';
ALTER ROLE db_owner ADD MEMBER [srns_app];
GO

PRINT 'Database initialization completed successfully.';
GO

