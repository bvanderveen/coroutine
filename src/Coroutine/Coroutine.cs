using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    // no-value
    public class Unit
    {
        private Unit() { }
        Unit value;
        public Unit Value { get { return value ?? (value = new Unit()); } }
    }


    // merely contains a record of the continuation, used by coroutine driver.
    public class ContinuationState
    {
        public Action<Action> Continuation;
        
        protected Exception exception;
        public Exception Exception { get { return exception; } }

        public ContinuationState() { }
        public ContinuationState(Action<Action, Action<Exception>> continuation)
        {
            Continuation = 
                a => continuation(a, e => { exception = e; a(); });
        }


        public static ContinuationState<T> FromAsync<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return new ContinuationState<T>(Coroutine.Continuation.FromAsync<T>(begin, end));
        }

        public static ContinuationState FromAsync(Func<AsyncCallback, object, IAsyncResult> begin, Action<IAsyncResult> end)
        {
            return new ContinuationState(Coroutine.Continuation.FromAsync(begin, end));
        }
    }

    // used by iterator scope to capture the side effects of the operation 
    // (coroutine driver only cares about the continuation).
    public class ContinuationState<T> : ContinuationState
    {
        T result;

        public ContinuationState(Action<Action<T>, Action<Exception>> continuation)
        {
            Continuation =
                a => continuation(
                    r0 => { result = r0; a(); }, 
                    e0 => { exception = e0; a(); });
        }

        public T Result
        {
            get
            {
                if (exception != null)
                    throw new Exception("The continuation resulted in an exception.", exception);

                return result;
            }
        }
    }

    public static class Continuation
    {
        public static Action<Action<T>, Action<Exception>> FromAsync<T>(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            return (r, e) =>
            {
                begin(iasr =>
                {
                    T result = default(T);
                    try
                    {
                        result = end(iasr);
                        r(result);
                    }
                    catch (Exception ex)
                    {
                        e(ex);
                    }
                }, null);
            };
        }

        public static Action<Action, Action<Exception>> FromAsync(Func<AsyncCallback, object, IAsyncResult> begin, Action<IAsyncResult> end)
        {
            return (r, e) =>
            {
                begin(iasr =>
                {
                    try
                    {
                        end(iasr);
                        r();
                    }
                    catch (Exception ex)
                    {
                        e(ex);
                    }
                }, null);
            };
        }

        public static void Enumerate<T>(IEnumerator<object> continuation, Action<T> result, Action<Exception> exception, Action<Action> trampoline)
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
                exception(new Exception("Exception while continuing coroutine.", e));
                return;
            }

            if (!continues)
            {
                //Console.WriteLine("Continuation " + continuation + "  does not continue.");
                continuation.Dispose();
                return;
            }

            try
            {
                if (value is ContinuationState)
                    value = (value as ContinuationState).Continuation;

                if (value is Action<Action>)
                {
                    //Console.WriteLine("will continue.");
                    (value as Action<Action>)(
                        trampoline == null ?
                            (Action)(() => Enumerate(continuation, result, exception, null)) :
                            (Action)(() => trampoline(() => Enumerate<T>(continuation, result, exception, trampoline))));
                    return;
                }
                else if (value == null || value is T)
                {
                    //Console.WriteLine("result value!");

                    // enforce single-value. iterate over the end of the block so all code is executed.
                    while (continuation.MoveNext()) { };
                    continuation.Dispose();

                    result((T)value);
                    return;
                }

                Enumerate<T>(continuation, result, exception, trampoline);
            }
            catch (Exception e)
            {
                continuation.Dispose();
                exception(new Exception("Exception while handling value yielded by coroutine.", e));
                return;
            }
        }
    }
}
