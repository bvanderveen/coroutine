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

        //public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        //{
        //    return new ContinuationState<T>(iteratorBlock.AsContinuation());
        //}

        //public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        //{
        //    return new ContinuationState<T>(iteratorBlock.AsContinuation(trampoline));
        //}

        //public static ContinuationState<T> AsContinuationState<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        //{
        //    return new ContinuationState<T>(AsContinuation(begin, end));
        //}

        public static ContinuationState SetCurrentContinuation(Continuation c)
        {
            ContinuationState.SetContinuation(ContinuationState.current, c);
            return ContinuationState.current;
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
            ContinuationState previous = ContinuationState.current;
            ContinuationState.current = new ContinuationState();
            Coroutine.Continue(iteratorBlock.GetEnumerator(), result, exception, null);
            ContinuationState.current = previous;
        }

        public static void BeginCoroutine(this IEnumerable<object> iteratorBlock,
            Action<object> result, Action<Exception> exception, Action<Action> trampoline)
        {
            ContinuationState previous = ContinuationState.current;
            ContinuationState.current = new ContinuationState();
            Coroutine.Continue(iteratorBlock.GetEnumerator(), result, exception, trampoline);
            ContinuationState.current = previous;
        }


        public static Task<object> AsTask(this Continuation cont)
        {
            var tcs = new TaskCompletionSource<object>();

            cont(r => tcs.SetResult(r), e => tcs.SetException(e));

            return tcs.Task;
        }

        public static Continuation AsContinuation(this Task task)
        {
            return (complete, exception) =>
            {
                task.ContinueWith(t => complete(null));
            };
        }

        public static Continuation AsContinuation(this IObservable<object> observable)
        {
            return (complete, exception) =>
            {
                observable.Subscribe(new CompletionObserver(complete));
            };
        }

        class CompletionObserver : IObserver<object>
        {
            Action<object> complete;

            public CompletionObserver(Action<object> complete)
            {
                this.complete = complete;
            }

            public void OnCompleted()
            {
                complete(null);
            }

            public void OnError(Exception error) { }
            public void OnNext(object value) { }
        }
    }
}
