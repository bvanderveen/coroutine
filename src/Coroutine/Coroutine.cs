using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
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
