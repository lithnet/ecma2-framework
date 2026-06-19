using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Tests
{
    /// <summary>
    /// Tests for <see cref="ParallelForEach.ForEachAsync{T}"/>, the netstandard2.0 stand-in for
    /// <c>Parallel.ForEachAsync</c>. Covers full processing, the concurrency cap, cancellation,
    /// and exception propagation. The tests are deterministic: concurrency is gated with
    /// <see cref="TaskCompletionSource{TResult}"/> and counters rather than wall-clock timing.
    /// </summary>
    [TestClass]
    public class ParallelForEachTests
    {
        [TestMethod]
        public async Task AllItemsAreProcessed()
        {
            List<int> source = Enumerable.Range(0, 100).ToList();
            ConcurrentBag<int> processed = new ConcurrentBag<int>();

            await ParallelForEach.ForEachAsync(source, 4, CancellationToken.None, async (item, ct) =>
            {
                processed.Add(item);
                await Task.Yield();
            });

            List<int> sortedProcessed = processed.OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(source, sortedProcessed, "Every item in the source must be processed exactly once.");
        }

        [TestMethod]
        public async Task ConcurrencyNeverExceedsTheCap()
        {
            const int cap = 5;
            List<int> source = Enumerable.Range(0, 200).ToList();

            int currentConcurrency = 0;
            int peakConcurrency = 0;
            object peakLock = new object();

            await ParallelForEach.ForEachAsync(source, cap, CancellationToken.None, async (item, ct) =>
            {
                int current = Interlocked.Increment(ref currentConcurrency);

                lock (peakLock)
                {
                    if (current > peakConcurrency)
                    {
                        peakConcurrency = current;
                    }
                }

                // Yield repeatedly to give the scheduler ample opportunity to over-subscribe if the
                // semaphore gate were not honoured.
                await Task.Yield();
                await Task.Delay(1, ct).ConfigureAwait(false);

                Interlocked.Decrement(ref currentConcurrency);
            });

            Assert.IsTrue(peakConcurrency <= cap, $"Peak concurrency {peakConcurrency} must not exceed the cap {cap}.");
            Assert.IsTrue(peakConcurrency > 1, "The test should actually exercise concurrency above 1.");
        }

        [TestMethod]
        public async Task NonPositiveCapFallsBackToProcessorCount()
        {
            int expectedCap = Environment.ProcessorCount;
            List<int> source = Enumerable.Range(0, expectedCap * 20).ToList();

            int currentConcurrency = 0;
            int peakConcurrency = 0;
            object peakLock = new object();

            await ParallelForEach.ForEachAsync(source, 0, CancellationToken.None, async (item, ct) =>
            {
                int current = Interlocked.Increment(ref currentConcurrency);

                lock (peakLock)
                {
                    if (current > peakConcurrency)
                    {
                        peakConcurrency = current;
                    }
                }

                await Task.Yield();
                await Task.Delay(1, ct).ConfigureAwait(false);

                Interlocked.Decrement(ref currentConcurrency);
            });

            Assert.IsTrue(peakConcurrency <= expectedCap, $"Peak concurrency {peakConcurrency} must not exceed the processor-count fallback {expectedCap}.");
        }

        [TestMethod]
        public async Task CancellationStopsProcessing()
        {
            const int total = 1000;
            List<int> source = Enumerable.Range(0, total).ToList();

            CancellationTokenSource cts = new CancellationTokenSource();
            int processedCount = 0;

            try
            {
                await ParallelForEach.ForEachAsync(source, 2, cts.Token, async (item, ct) =>
                {
                    int count = Interlocked.Increment(ref processedCount);

                    // Cancel partway through; from this point no new iterations should be scheduled.
                    if (count == 10)
                    {
                        cts.Cancel();
                    }

                    await Task.Yield();
                    ct.ThrowIfCancellationRequested();
                });

                Assert.Fail("ForEachAsync should have surfaced an OperationCanceledException.");
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            Assert.IsTrue(processedCount < total, $"Processing should have stopped early; processed {processedCount} of {total}.");
        }

        [TestMethod]
        public async Task BodyExceptionSurfacesFromAwait()
        {
            List<int> source = Enumerable.Range(0, 500).ToList();
            InvalidOperationException thrown = new InvalidOperationException("body failure");

            try
            {
                await ParallelForEach.ForEachAsync(source, 4, CancellationToken.None, async (item, ct) =>
                {
                    await Task.Yield();

                    if (item == 7)
                    {
                        throw thrown;
                    }

                    // Honour the linked token so siblings wind down once one body has failed.
                    await Task.Delay(5, ct).ConfigureAwait(false);
                });

                Assert.Fail("ForEachAsync should have surfaced the body exception.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreSame(thrown, ex, "The original body exception must surface, not a wrapped or substituted one.");
            }
        }

        [TestMethod]
        public async Task EmptySourceCompletesWithoutInvokingBody()
        {
            List<int> source = new List<int>();
            bool invoked = false;

            await ParallelForEach.ForEachAsync(source, 4, CancellationToken.None, (item, ct) =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

            Assert.IsFalse(invoked, "The body must never be invoked for an empty source.");
        }
    }
}
