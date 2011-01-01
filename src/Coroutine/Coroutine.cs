using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    public delegate void Continuation<T>(Action<T> complete, Action<Exception> exception);

    public class ContinuationState<T>
    {
        T result;
        Exception exception;
        internal Continuation<T> continuation;

        public ContinuationState(Continuation<T> continuation)
        {
            this.continuation =
                (r, e) => continuation(
                    r0 => { result = r0; r(r0); }, 
                    e0 => { exception = e0; e(e0); });
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

    public static class Coroutine
    {
        public static void Continue<T>(IEnumerator<object> continuation, Action<T> result, Action<Exception> exception, Action<Action> trampoline)
        {
            Console.WriteLine("Continuing!");
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
                Console.WriteLine("Exception during MoveNext.");
                continuation.Dispose();
                exception(new Exception("Exception while continuing coroutine.", e));
                return;
            }

            if (!continues)
            {
                Console.WriteLine("Continuation " + continuation + "  does not continue.");
                continuation.Dispose();
                return;
            }

            try
            {
                if (value is ContinuationState<T>)
                    value = (value as ContinuationState<T>).continuation;

                if (value is Continuation<T>)
                {
                    Console.WriteLine("will continue.");
                    (value as Continuation<T>)(
                        trampoline == null ?
                            (Action<T>)(_ => Continue(continuation, result, exception, null)) :
                            (Action<T>)(_ => trampoline(() => Continue<T>(continuation, result, exception, trampoline))),
                        trampoline == null ?
                            (Action<Exception>)(_ => Continue(continuation, result, exception, null)) :
                            (Action<Exception>)(_ => trampoline(() => Continue<T>(continuation, result, exception, trampoline))));
                    return;
                }
                else if (value == null || value is T)
                {
                    Console.WriteLine("result value!");
                    result((T)value);

                    // enforce single-value. iterate over the end of the block so all code is executed.
                    while (continuation.MoveNext()) { };
                    continuation.Dispose();
                    return;
                }

                Continue<T>(continuation, result, exception, trampoline);
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
