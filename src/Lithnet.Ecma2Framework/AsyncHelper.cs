using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    public static class AsyncHelper
    {
        // This function destroys ILMerge, so we switched to ILRepack instead. The GetAwaiter() call seems to be responsible for making ILMerge hang
        public static TResult RunSync<TResult>(Task<TResult> func)
        {
            return AsyncHelper.RunSync(func, CancellationToken.None);
        }

        public static TResult RunSync<TResult>(Task<TResult> func, CancellationToken token)
        {
            return func.GetAwaiter().GetResult();
        }

        public static void RunSync(Task func)
        {
            AsyncHelper.RunSync(func, CancellationToken.None);
        }

        public static void RunSync(Task func, CancellationToken token)
        {
            func.GetAwaiter().GetResult();
        }

        public static long InterlockedCombine(ref long location, Func<long, long> update)
        {
            long initialValue, newValue;

            do
            {
                initialValue = location;
                newValue = update(initialValue);
            }
            while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

            return initialValue;
        }

        public static long InterlockedMax(ref long location, long value)
        {
            return AsyncHelper.InterlockedCombine(ref location, v => Math.Max(v, value));
        }
    }
}