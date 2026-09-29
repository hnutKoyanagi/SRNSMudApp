using Testcontainers.MsSql;

namespace SRNSMudApp.Tests.TestSupport;

/// <summary>
///     テストアセンブリ全体で 1 つだけ MSSQL Testcontainers コンテナを起動・共有する xUnit フィクスチャ。
///     同時に起動・作成されるコンテナ数は ContainerLimiter により最大 4 つまでに制限される。
/// </summary>
public class MsSqlContainerFixture : IAsyncLifetime
{
    private IDisposable? _containerSlot;
    private MsSqlContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("MsSqlContainer is not initialized.");

    public async Task InitializeAsync()
    {
        _containerSlot = await ContainerLimiter.AcquireSlotAsync().ConfigureAwait(false);
        try
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            _containerSlot?.Dispose();
            _containerSlot = null;
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_container != null)
            {
                await _container.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _containerSlot?.Dispose();
            _containerSlot = null;
        }
    }
}