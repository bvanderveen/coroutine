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
    //    public static Task<object> StartCoroutineTask(this IEnumerable<object> iteratorBlock)
    //    {
    //        return iteratorBlock.StartCoroutineTask(TaskScheduler.Current);
    //    }

    //    public static Task<object> StartCoroutineTask(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
    //    {
    //        var tcs = new TaskCompletionSource<object>();

    //        iteratorBlock.AsContinuation(a => Task.Factory.StartNew(a, CancellationToken.None, TaskCreationOptions.None, scheduler))
    //            (r => tcs.SetResult(r), e => tcs.SetException(e));

    //        return tcs.Task;
    //    }

        public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsCoroutine<T>(null);
        }

        public static ContinuationState<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return new ContinuationState<T>(iteratorBlock.AsContinuation<T>(trampoline));
        }

        public static Continuation<T> AsContinuation<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.GetEnumerator().AsContinuation<T>(null);
        }

        public static Continuation<T> AsContinuation<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return iteratorBlock.GetEnumerator().AsContinuation<T>(trampoline);
        }

        public static Continuation<T> AsContinuation<T>(this IEnumerator<object> enumerator)
        {
            return (result, exception) =>
                Coroutine.Continue<T>(enumerator, result, exception, null);
        }

        public static Continuation<T> AsContinuation<T>(this IEnumerator<object> enumerator, Action<Action> trampoline)
        {
            return (result, exception) =>
                Coroutine.Continue<T>(enumerator, result, exception, trampoline);
        }

        public static ContinuationState<T> AsContinuationState<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return new ContinuationState<T>(AsContinuation(begin, end));
        }

        public static Continuation<T> AsContinuation<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return (r, e) =>
            {
                begin(iasr =>
                {
                    T result = default(T);
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
            };
        }

        //public static Continuation<T> AsContinuation<T>(this IEnumerable<object> iteratorBlock)
        //{
        //    return iteratorBlock.AsContinuation<T>(null);
        //}

        //public static Continuation<T> AsContinuation<T>(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        //{
        //    return (result, exception) =>
        //        iteratorBlock.BeginCoroutine<T>(result, exception, trampoline);
        //}

        //public static void BeginCoroutine<T>(this IEnumerable<object> iteratorBlock,
        //    Action<T> result, Action<Exception> exception)
        //{
        //    iteratorBlock.BeginCoroutine<T>(result, exception, null);
        //}

        //public static void BeginCoroutine<T>(this IEnumerable<object> iteratorBlock,
        //    Action<T> result, Action<Exception> exception, Action<Action> trampoline)
        //{
        //    iteratorBlock.GetEnumerator().AsContinuation<T>(trampoline)(result, exception);
        //}


        //public static Task<object> AsTask(this Continuation cont)
        //{
        //    var tcs = new TaskCompletionSource<object>();

        //    cont(r => tcs.SetResult(r), e => tcs.SetException(e));

        //    return tcs.Task;
        //}

        //public static Continuation AsContinuation(this Task task)
        //{
        //    return (complete, exception) =>
        //    {
        //        task.ContinueWith(t => complete(null));
        //    };
        //}

        //public static Continuation AsContinuation(this IObservable<object> observable)
        //{
        //    return (complete, exception) =>
        //    {
        //        observable.Subscribe(new CompletionObserver(complete));
        //    };
        //}

        //class CompletionObserver : IObserver<object>
        //{
        //    Action<object> complete;

        //    public CompletionObserver(Action<object> complete)
        //    {
        //        this.complete = complete;
        //    }

        //    public void OnCompleted()
        //    {
        //        complete(null);
        //    }

        //    public void OnError(Exception error) { }
        //    public void OnNext(object value) { }
        //}
    }
}
