using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// A minimal stand-in for <c>System.Threading.Tasks.Parallel.ForEachAsync</c>, which was
    /// introduced in .NET 6 and is not available on the netstandard2.0 target this framework
    /// builds against. The framework deliberately keeps a tight dependency closure (see the
    /// shim's refusal of System.Memory for the same reason), so rather than take a third-party
    /// polyfill package or re-introduce a multi-targeted TFM split, the small slice of
    /// <c>Parallel.ForEachAsync</c> semantics that the orchestrators actually use is reproduced
    /// here using only BCL primitives that exist on netstandard2.0.
    /// </summary>
    internal static class ParallelForEach
    {
        /// <summary>
        /// Invokes <paramref name="body"/> for each element of <paramref name="source"/>, running
        /// at most <paramref name="maxDegreeOfParallelism"/> bodies concurrently. The semantics
        /// match <c>Parallel.ForEachAsync</c>:
        /// <list type="bullet">
        ///   <item>At most <paramref name="maxDegreeOfParallelism"/> bodies run at once. When the
        ///     value is less than or equal to zero, <see cref="Environment.ProcessorCount"/> is
        ///     used, matching the <c>ParallelOptions</c> default.</item>
        ///   <item><paramref name="cancellationToken"/> is honoured: once it is signalled no new
        ///     iterations are scheduled, and the token (linked) is passed into each body.</item>
        ///   <item>When a body throws, no further iterations are scheduled, the in-flight bodies are
        ///     cancelled via a linked token, and the first exception surfaces from the returned task.
        ///     Exceptions are never swallowed.</item>
        /// </list>
        /// </summary>
        /// <typeparam name="T">The element type of the source sequence.</typeparam>
        /// <param name="source">The sequence to iterate.</param>
        /// <param name="maxDegreeOfParallelism">The maximum number of bodies to run concurrently.</param>
        /// <param name="cancellationToken">A token used to stop scheduling and to signal each body.</param>
        /// <param name="body">The asynchronous body invoked for each element.</param>
        public static async Task ForEachAsync<T>(IEnumerable<T> source, int maxDegreeOfParallelism, CancellationToken cancellationToken, Func<T, CancellationToken, Task> body)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (body == null)
            {
                throw new ArgumentNullException("body");
            }

            int degree = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;

            using (SemaphoreSlim semaphore = new SemaphoreSlim(degree))
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                List<Task> tasks = new List<Task>();

                try
                {
                    foreach (T item in source)
                    {
                        // WaitAsync throws OperationCanceledException once the external token fires or a
                        // sibling body has failed (which cancels the linked token). In either case we stop
                        // scheduling and break out so that the already-started tasks can be awaited below,
                        // surfacing the original fault rather than the cancellation.
                        await semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);

                        T captured = item;

                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await body(captured, linkedCts.Token).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Cancel siblings and stop further scheduling, then re-throw so the
                                // fault is observed by Task.WhenAll below.
                                linkedCts.Cancel();
                                throw;
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, linkedCts.Token));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Scheduling was cancelled (external cancellation or a sibling failure). Fall through
                    // to await the started tasks so their exceptions (or the cancellation) propagate.
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
    }
}
