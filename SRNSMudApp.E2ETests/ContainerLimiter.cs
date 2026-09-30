namespace SRNSMudApp.E2ETests;

/// <summary>
///     E2E テスト実行時に起動される MSSQL Testcontainers コンテナの同時作成・稼働数を
///     プロセス内およびプロセス間（単体テスト等との共有）で最大 4 つまでに制限するリミッター。
/// </summary>
public static class ContainerLimiter
{
    private const int MaxContainers = 4;
    private static readonly SemaphoreSlim LocalLock = new(MaxContainers, MaxContainers);
    private static readonly string LockDir = Path.Combine(Path.GetTempPath(), "srns_container_locks");

    /// <summary>
    ///     コンテナ起動スロットを 1 つ確保する。
    ///     同時に 4 つ以上のコンテナが稼働している場合は空きが出るまで待機する。
    ///     返却された <see cref="IDisposable"/> を破棄（Dispose）することでスロットが解放される。
    /// </summary>
    public static async Task<IDisposable> AcquireSlotAsync(CancellationToken cancellationToken = default)
    {
        await LocalLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(LockDir);
            FileStream? lockFile = null;

            while (lockFile is null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 0; i < MaxContainers; i++)
                {
                    var filePath = Path.Combine(LockDir, $"slot_{i}.lock");
                    try
                    {
                        lockFile = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                        break;
                    }
                    catch (IOException)
                    {
                        // 他のスレッド/プロセスが利用中
                    }
                }

                if (lockFile is null)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }

            return new SlotLease(LocalLock, lockFile);
        }
        catch
        {
            _ = LocalLock.Release();
            throw;
        }
    }

    private sealed class SlotLease(SemaphoreSlim semaphore, FileStream lockFile) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    try
                    {
                        lockFile.Dispose();
                    }
                    catch (Exception)
                    {
                        // 破棄時の例外は無視するがセマフォは解放する
                    }
                }
                finally
                {
                    _ = semaphore.Release();
                }
            }
        }
    }
}