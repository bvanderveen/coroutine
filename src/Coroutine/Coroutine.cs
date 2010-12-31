using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    public delegate void Continuation(Action<object> complete, Action<Exception> exception);

    public class ContinuationState
    {
        [ThreadStatic]
        public static ContinuationState current;

        internal object result;
        internal Exception exception;
        internal Continuation Continuation;

        public static void SetContinuation(ContinuationState state, Continuation continuation)
        {
            state.Continuation =
                (r, e) => continuation(
                    r0 => { state.result = r0; r(r0); }, 
                    e0 => { state.exception = e0; e(e0); });
        }

        public T GetResult<T>() {
            if (ContinuationState.current.exception != null) 
                throw ContinuationState.current.exception;

            return (T)ContinuationState.current.result; 
        }
    }

    public static class Coroutine
    {
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
                    ContinuationState currentState = ContinuationState.current;

                    Action setStateAndContinue = () => {
                                    ContinuationState.current = currentState;
                                    Continue(continuation, result, exception, trampoline); 
                                };

                    Action<object> trampolined = trampoline == null ? 
                        (Action<object>)(_ => setStateAndContinue()) : 
                        (Action<object>)(_ => trampoline(setStateAndContinue));

                    cont(trampolined, trampolined);
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
