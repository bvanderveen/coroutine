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
        public static Task<object> StartCoroutineTask(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.StartCoroutineTask(TaskScheduler.Current);
        }

        public static Task<object> StartCoroutineTask(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
        {
            var tcs = new TaskCompletionSource<object>();

            iteratorBlock.AsContinuation(a => Task.Factory.StartNew(a, CancellationToken.None, TaskCreationOptions.None, scheduler))
                (r => tcs.SetResult(r), e => tcs.SetException(e));

            return tcs.Task;
        }

        public static ContinuationState<T> AsCoroutine2<T>(this IEnumerable<object> iteratorBlock)
        {
            return new ContinuationState<T>(iteratorBlock.AsContinuation());
        }

        public static ContinuationState<T> AsCoroutine2<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return new ContinuationState<T>(iteratorBlock.AsContinuation(trampoline));
        }

        public static ContinuationState<T> AsContinuationState<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return new ContinuationState<T>(AsContinuation(begin, end));
        }

        public static Continuation AsContinuation<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return (r, e) => begin(iasr => {
                object result = null;
                try
                {
                    result = end(iasr);
                }
                catch (Exception ex)
                {
                    e(ex);
                }
                r(result);
            }, null);
        }

        public static Continuation AsContinuation(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsContinuation(null);
        }

        public static Continuation AsContinuation(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return (result, exception) =>
                iteratorBlock.BeginCoroutine(result, exception, trampoline);
        }

        public static void BeginCoroutine(this IEnumerable<object> iteratorBlock,
            Action<object> result, Action<Exception> exception)
        {
            Coroutine.Continue(iteratorBlock.GetEnumerator(), result, exception, null);
        }

        public static void BeginCoroutine(this IEnumerable<object> iteratorBlock,
            Action<object> result, Action<Exception> exception, Action<Action> trampoline)
        {
            Coroutine.Continue(iteratorBlock.GetEnumerator(), result, exception, trampoline);
        }
    }
}
