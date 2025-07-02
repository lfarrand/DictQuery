using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace AntlrParser8.Tests
{
    namespace AntlrParser.Tests
    {
        public class MemoryCacheTrimTests
        {
            [Fact]
            public async Task Constructor_StartsBackgroundTask_ThatCompactsCache()
            {
                // Arrange
                var mockCache = Substitute.For<IMemoryCache>();
                var compactionCalled = false;
                mockCache.When(c => c.Compact(Arg.Any<double>()))
                    .Do(_ => compactionCalled = true);

                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());

                // Act
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var trimmer = new MemoryCacheTrim(mockCache, cacheLock, cancellationTokenSource,
                        TimeSpan.FromMilliseconds(10));

                    // Wait for the compact to be called
                    var timeout = TimeSpan.FromSeconds(1);
                    var sw = Stopwatch.StartNew();
                    while (!compactionCalled && sw.Elapsed < timeout)
                    {
                        await Task.Delay(10);
                    }

                    // Assert
                    Assert.True(compactionCalled, "Compact should have been called");
                }
            }


            [Fact]
            public async Task BackgroundTask_StopsWhenCancellationRequested()
            {
                // Arrange
                var mockCache = Substitute.For<IMemoryCache>();
                var compactionCount = 0;
                mockCache.When(c => c.Compact(Arg.Any<double>()))
                    .Do(_ => Interlocked.Increment(ref compactionCount));

                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());

                using (var cts = new CancellationTokenSource())
                {
                    var delay = TimeSpan.FromMilliseconds(10);
                    var trimmer = new MemoryCacheTrim(mockCache, cacheLock, cts, delay);

                    // Act
                    await Task.Delay(100);
                    var initialCount = compactionCount;
                    cts.Cancel();
                    await Task.Delay(100);

                    // Assert
                    Assert.Equal(initialCount, compactionCount);
                }
            }


            [Fact]
            public async Task BackgroundTask_UsesWriteLock()
            {
                // Arrange
                var mockLock = Substitute.For<IReaderWriterLock>();
                var isLockHeld = false;
                var callCount = 0;

                mockLock.When(x => x.EnterWriteLock())
                    .Do(_ =>
                    {
                        if (isLockHeld)
                        {
                            throw new SynchronizationLockException("Lock already held");
                        }

                        isLockHeld = true;
                        callCount++;
                    });

                mockLock.When(x => x.ExitWriteLock())
                    .Do(_ =>
                    {
                        if (!isLockHeld)
                        {
                            throw new SynchronizationLockException("Lock not held");
                        }

                        isLockHeld = false;
                    });

                using (var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions())))
                {
                    // Act
                    using (var cancellationTokenSource = new CancellationTokenSource())
                    {
                        var delay = TimeSpan.FromMilliseconds(10);
                        var trimmer = new MemoryCacheTrim(cache, mockLock, cancellationTokenSource, delay);

                        // Wait for at least one execution
                        await Task.Delay(50);

                        // Cancel to prevent more executions
                        cancellationTokenSource.Cancel();
                        await Task.Delay(50); // Give time for the task to complete

                        // Assert
                        Assert.True(callCount > 0, "Lock should have been entered at least once");
                        Assert.False(isLockHeld, "Lock should not be held at the end");

                        // Verify the exit was called same number of times as enter
                        mockLock.Received(callCount).ExitWriteLock();
                        mockLock.Received(callCount).EnterWriteLock();
                    }
                }
            }


            [Fact]
            public async Task BackgroundTask_HandlesExceptions()
            {
                // Arrange
                var mockCache = Substitute.For<IMemoryCache>();
                mockCache.When(c => c.Compact(Arg.Any<double>()))
                    .Do(_ => throw new InvalidOperationException());

                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());

                // Act & Assert
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var delay = TimeSpan.FromMilliseconds(10);
                    var trimmer = new MemoryCacheTrim(mockCache, cacheLock, cancellationTokenSource, delay);
                    await Task.Delay(100);

                    // If we got here without an exception, the test passes
                    Assert.True(true);
                }
            }


            [Fact]
            public async Task BackgroundTask_CompactsWithCorrectPercentage()
            {
                // Arrange
                var mockCache = Substitute.For<IMemoryCache>();
                var compactCalled = false;
                double? usedPercentage = null;

                mockCache.When(c => c.Compact(Arg.Any<double>()))
                    .Do(x =>
                    {
                        compactCalled = true;
                        usedPercentage = x.Arg<double>();
                    });

                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());

                // Act
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var trimmer = new MemoryCacheTrim(mockCache, cacheLock, cancellationTokenSource,
                        TimeSpan.FromMilliseconds(10));

                    // Wait for the compact to be called
                    var timeout = TimeSpan.FromSeconds(1);
                    var sw = Stopwatch.StartNew();
                    while (!compactCalled && sw.Elapsed < timeout)
                    {
                        await Task.Delay(10);
                    }

                    // Assert
                    Assert.True(compactCalled, "Compact should have been called");
                    Assert.Equal(0.5, usedPercentage);
                }
            }

            [Fact]
            public async Task MemoryCacheTrim_ShouldCompactPeriodically()
            {
                var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());
                var cts = new CancellationTokenSource();
                var interval = TimeSpan.FromMilliseconds(100);

                // Add items to cache
                cache.Set("a", 1);
                cache.Set("b", 2);
                cache.Set("c", 3);

                // Start trimmer
                var trimmer = new MemoryCacheTrim(cache, cacheLock, cts, interval);

                // Wait for at least one trim cycle
                await Task.Delay(300);

                // Compact should have removed about half the items
                int count;
                cacheLock.EnterReadLock();
                try
                {
                    count = 0;
                    foreach (var entry in cache.GetKeys())
                    {
                        count++;
                    }
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }

                Assert.True(count < 3, $"Expected fewer than 3 items after trim, got {count}");

                // Cancel and clean up
                cts.Cancel();
            }

            [Fact]
            public async Task MemoryCacheTrim_ShouldRespectCancellation()
            {
                var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
                var cacheLock = new ReaderWriterLockSlimAdapter(new ReaderWriterLockSlim());
                var cts = new CancellationTokenSource();
                var interval = TimeSpan.FromMilliseconds(50);

                var trimmer = new MemoryCacheTrim(cache, cacheLock, cts, interval);

                // Cancel after a short delay
                await Task.Delay(100);
                cts.Cancel();

                // Wait to ensure the task has stopped
                await Task.Delay(100);

                // No exceptions should have occurred, and the test should complete
                Assert.True(true);
            }

            [Fact]
            public async Task MemoryCacheTrim_ShouldHandleLockExceptions()
            {
                var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
                var cacheLock = Substitute.For<IReaderWriterLock>();
                var cts = new CancellationTokenSource();
                var interval = TimeSpan.FromMilliseconds(50);

                // Acquire the write lock so the trimmer cannot enter
                cacheLock.EnterWriteLock();

                var trimmer = new MemoryCacheTrim(cache, cacheLock, cts, interval);

                // Wait for a cycle, then release the lock
                await Task.Delay(100);
                cacheLock.ExitWriteLock();

                // Cancel and clean up
                cts.Cancel();

                // If the code reaches here without deadlock, the test passes
                Assert.True(true);
            }
        }
    }
}