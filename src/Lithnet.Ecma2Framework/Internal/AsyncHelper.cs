using System.Threading;
using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// A helper class to allow synchronous execution of asynchronous methods
    /// </summary>
    public static class AsyncHelper
    {
        /// <summary>
        /// Runs an asynchronous method synchronously
        /// </summary>
        /// <typeparam name="TResult">The return type of the asynchronous method</typeparam>
        /// <param name="func">The asynchronous method to run</param>
        /// <returns>The result of the asynchronous method</returns>
        public static TResult RunSync<TResult>(Task<TResult> func)
        {
            return AsyncHelper.RunSync(func, CancellationToken.None);
        }

        /// <summary>
        /// Runs an asynchronous method synchronously
        /// </summary>
        /// <typeparam name="TResult">The return type of the asynchronous method</typeparam>
        /// <param name="func">The asynchronous method to run</param>
        /// <param name="token">A cancellation token</param>
        /// <returns>The result of the asynchronous method</returns>
        public static TResult RunSync<TResult>(Task<TResult> func, CancellationToken token)
        {
            return func.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs an asynchronous method synchronously
        /// </summary>
        /// <param name="func">The asynchronous method to run</param>
        public static void RunSync(Task func)
        {
            AsyncHelper.RunSync(func, CancellationToken.None);
        }

        /// <summary>
        /// Runs an asynchronous method synchronously
        /// </summary>
        /// <param name="func">The asynchronous method to run</param>
        /// <param name="token">A cancellation token</param>
        public static void RunSync(Task func, CancellationToken token)
        {
            func.GetAwaiter().GetResult();
        }
    }
}