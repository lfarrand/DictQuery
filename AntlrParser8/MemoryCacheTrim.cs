namespace AntlrParser8;

public class MemoryCacheTrim
{
    public MemoryCacheTrim(IMemoryCache cache, IReaderWriterLock cacheLock,
        CancellationTokenSource cancellationTokenSource, TimeSpan interval)
    {
        Task.Factory.StartNew(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    cacheLock.EnterWriteLock();
                    cache.Compact(0.5d);
                    Console.WriteLine($"Compact");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                await Task.Delay(interval);
            }
        }, TaskCreationOptions.LongRunning);
    }
}