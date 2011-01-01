using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    // merely contains a record of the continuation, used by coroutine driver.
    public class ContinuationState
    {
        public Action<Action> Continuation;
    }

    // used by iterator scope to capture the side effects of the operation 
    // (coroutine driver only cares about the continuation).
    public class ContinuationState<T> : ContinuationState
    {
        T result;
        Exception exception;

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
                if (value is ContinuationState)
                    value = (value as ContinuationState).Continuation;

                if (value is Action<Action>)
                {
                    Console.WriteLine("will continue.");
                    (value as Action<Action>)(
                        trampoline == null ?
                            (Action)(() => Continue(continuation, result, exception, null)) :
                            (Action)(() => trampoline(() => Continue<T>(continuation, result, exception, trampoline))));
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
