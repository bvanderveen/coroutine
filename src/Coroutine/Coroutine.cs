using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    public delegate void Continuation(Action<object> complete, Action<Exception> exception);


    //public interface IContinuationState<out T>
    //{
    //    Continuation Continuation { get; }
    //    T Result { get; }
    //}


    public class ContinuationState
    {
        internal Continuation Continuation;
        public ContinuationState(Continuation c)
        {
            Continuation = c;
        }
    }
    public class ContinuationState<T> : ContinuationState
    {
        public ContinuationState(Continuation c) : base(c) { }
        public T Result { get { if (Coroutine2.exceptionState != null) throw Coroutine2.exceptionState; return (T)Coroutine2.resultState; } }
    }

    public static class Coroutine2
    {
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

        [ThreadStatic]
        internal static object resultState;
        [ThreadStatic]
        internal static Exception exceptionState;

        public static void Continue(IEnumerator<object> continuation, Action<object> result, Action<Exception> exception, Action<Action> trampoline)
        {
            //Console.WriteLine("Continuing!");
            var continues = false;
            object value = null;

            try
            {
                continues = continuation.MoveNext();
                if (continues)
                    value = continuation.Current;
            }
            catch (Exception e)
            {
                //Console.WriteLine("Exception during MoveNext.");
                Console.WriteLine("Exit location 1.");
                continuation.Dispose();
                exception(e);
                return;
            }

            if (!continues)
            {
                //Console.WriteLine("Continuation does not continue.");
                Console.WriteLine("Exit location 2.");
                continuation.Dispose();
                //result(null);
                return;
            }

            try
            {
                Continuation cont = null;

                if (value is Continuation)
                    cont = value as Continuation;
                else if (value is ContinuationState)
                    cont = (value as ContinuationState).Continuation;
                else if (value is Task)
                    cont = (value as Task).AsContinuation();
                
                if (cont != null)
                {
                    cont(
                        (trampoline == null ? 
                            (Action<object>)(r => 
                                { 
                                    resultState = r; 
                                    Continue(continuation, result, exception, trampoline); 
                                    resultState = null;
                                }) :
                            (Action<object>)(r => trampoline(() => 
                                { 
                                    resultState = r; 
                                    Continue(continuation, result, exception, trampoline); 
                                    resultState = null; 
                                }))),
                        e => { exceptionState = e; exception(e); exceptionState = null; });
                }
                else
                {
                    continuation.Dispose();
                    result(value);
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exit location 3.");
                continuation.Dispose();
                exception(e);
                return;
            }
        }
    }

    public class Coroutine<T>
    {
        IEnumerator<object> continuation;
        AsyncResult<T> asyncResult;
        TaskScheduler scheduler;

        public Coroutine(IEnumerator<object> continuation, TaskScheduler scheduler)
        {
            this.continuation = continuation;
            this.scheduler = scheduler;
        }

        public IAsyncResult BeginInvoke(AsyncCallback cb, object state)
        {
            asyncResult = new AsyncResult<T>(cb, state);

            Continue(true);

            return asyncResult;
        }

        public T EndInvoke(IAsyncResult result)
        {
            //Console.WriteLine("Coroutine.EndInvoke");
            AsyncResult<T> ar = (AsyncResult<T>)result;
            continuation.Dispose();
            return ar.EndInvoke();
        }

        void Continue(bool sync)
        {
            //Console.WriteLine("Continuing!");
            var continues = false;

            try
            {
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                //Console.WriteLine("Exception during MoveNext.");
                asyncResult.SetAsCompleted(e, sync);
                return;
            }

            if (!continues)
            {
                //Console.WriteLine("Continuation does not continue.");
                asyncResult.SetAsCompleted(null, sync);
                return;
            }

            try
            {
                var value = continuation.Current;

                var task = value as Task;

                if (task != null)
                {
                    //Console.WriteLine("Will continue after Task.");
                    task.ContinueWith(t =>
                    {
                        bool synch = ((IAsyncResult)t).CompletedSynchronously;

                        if (false && t.IsFaulted) // disabled, iterator blocks must always remember to check for exceptions
                        {
                            //Console.WriteLine("Exception in Task.");
                            //Console.Out.WriteException(t.Exception);
                            asyncResult.SetAsCompleted(t.Exception, sync);
                        }
                        else
                        {
                            //Console.WriteLine("Continuing after Task.");
                            Continue(sync);
                        }
                    }, CancellationToken.None, TaskContinuationOptions.None, scheduler);
                }
                else
                {
                    if (value is T)
                    {
                        //Console.WriteLine("Completing with value.");
                        asyncResult.SetAsCompleted((T)value, sync);
                        return;
                    }

                    //Console.WriteLine("Continuing, discarding value.");
                    Continue(sync);
                }
            }
            catch (Exception e)
            {
                asyncResult.SetAsCompleted(e, sync);
                return;
            }
        }
    }
}
