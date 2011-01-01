using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Coroutine
{
    public static partial class Extensions
    {
        public static Action<Action<T>, Action<Exception>> AsContinuation<T>(this IEnumerable<object> enumerable)
        {
            return enumerable.GetEnumerator().AsContinuation<T>(null);
        }

        public static Action<Action<T>, Action<Exception>> AsContinuation<T>(this IEnumerable<object> enumerable, Action<Action> trampoline)
        {
            return enumerable.GetEnumerator().AsContinuation<T>(trampoline);
        }

        public static Action<Action<T>, Action<Exception>> AsContinuation<T>(this IEnumerator<object> enumerator)
        {
            return enumerator.AsContinuation<T>(null);
        }

        public static Action<Action<T>, Action<Exception>> AsContinuation<T>(this IEnumerator<object> enumerator, Action<Action> trampoline)
        {
            return (result, exception) =>
                Continuation.Enumerate<T>(enumerator, result, exception, trampoline);
        }

        public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.GetEnumerator().AsContinuationState<T>(null);
        }

        public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return iteratorBlock.GetEnumerator().AsContinuationState<T>(trampoline);
        }

        public static ContinuationState<T> AsContinuationState<T>(this IEnumerator<object> enumerator)
        {
            return enumerator.AsContinuationState<T>(null);
        }

        public static ContinuationState<T> AsContinuationState<T>(this IEnumerator<object> enumerator, Action<Action> trampoline)
        {
            return new ContinuationState<T>(AsContinuation<T>(enumerator, trampoline));
        }
    }
}
