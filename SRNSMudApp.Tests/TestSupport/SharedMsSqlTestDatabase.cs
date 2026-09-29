using SRNSMudApp.Data;

using Testcontainers.MsSql;

namespace SRNSMudApp.Tests.TestSupport;

/// <summary>
/// テストアセンブリ全体で共有される単一の MSSQL Testcontainers コンテナおよびマイグレーション済みデータベース。
/// 全テストクラスがこの共有インスタンスに対して並行で読み書きを行い、tid（名前空間）によってデータを完全分離する。
/// 同時に起動・作成されるコンテナ数は ContainerLimiter により最大 4 つまでに制限される。
/// </summary>
public static class SharedMsSqlTestDatabase
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static IDisposable? s_containerSlot;
    private static MsSqlContainer? s_container;
    private static MsSqlTestDatabase? s_database;
    private static Exception? s_initException;

    public static async Task<MsSqlTestDatabase> GetInstanceAsync()
    {
        if (s_database != null)
        {
            return s_database;
        }

        if (s_initException != null)
        {
            throw new InvalidOperationException("SharedMsSqlTestDatabase failed to initialize previously.", s_initException);
        }

        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (s_database != null)
            {
                return s_database;
            }

            if (s_initException != null)
            {
                throw new InvalidOperationException("SharedMsSqlTestDatabase failed to initialize previously.", s_initException);
            }

            try
            {
                if (s_container == null)
                {
                    // 最大 4 つのコンテナ同時稼働制限スロットを確保
                    s_containerSlot = await ContainerLimiter.AcquireSlotAsync().ConfigureAwait(false);
                    s_container = new MsSqlBuilder().Build();
                    await s_container.StartAsync().ConfigureAwait(false);
                }

                s_database = await MsSqlTestDatabase.CreateAsync(s_container.GetConnectionString(), "SharedTestDb").ConfigureAwait(false);
                return s_database;
            }
            catch (Exception ex)
            {
                s_initException = ex;
                if (s_database == null && s_container != null)
                {
                    try
                    {
                        await s_container.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // 無視
                    }
                    s_container = null;
                    s_containerSlot?.Dispose();
                    s_containerSlot = null;
                }
                throw;
            }
        }
        finally
        {
            _ = InitLock.Release();
        }
    }

    public static async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var db = await GetInstanceAsync().ConfigureAwait(false);
        return new ApplicationDbContext(db.Options);
    }

    public static async Task<MsSqlTestDatabase> CreateIsolatedDatabaseAsync(string prefix = "isolated")
    {
        _ = await GetInstanceAsync().ConfigureAwait(false);
        return await MsSqlTestDatabase.CreateAsync(s_container!.GetConnectionString(), prefix).ConfigureAwait(false);
    }
}