using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    public delegate void Continuation(Action<object> complete, Action<Exception> exception);

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
        public T Result { get { if (Coroutine.exceptionState != null) throw Coroutine.exceptionState; return (T)Coroutine.resultState; } }
    }

    public static class Coroutine
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
                continuation.Dispose();
                exception(e);
                return;
            }

            if (!continues)
            {
                //Console.WriteLine("Continuation does not continue.");
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
                continuation.Dispose();
                exception(e);
                return;
            }
        }
    }
}
