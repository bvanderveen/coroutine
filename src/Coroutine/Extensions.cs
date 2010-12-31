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
        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsCoroutine<T>(TaskScheduler.Current);
        }

        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
        {
            return iteratorBlock.AsCoroutine<T>(TaskCreationOptions.None, scheduler);
        }

        /// <summary>
        /// Creates a Coroutine Task from the iterator block. 
        /// </summary>
        /// <typeparam name="T">The type of the expected return value of the Coroutine.</typeparam>
        /// <param name="iteratorBlock">The iterator block.</param>
        /// <param name="opts">The <cref>TaskCreationOptions</cref> for the Coroutine Task.</param>
        /// <param name="scheduler">The <cref>TaskScheduler</cref> on which the Coroutine should execute.</param>
        /// <returns></returns>
        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskCreationOptions opts, TaskScheduler scheduler)
        {
            var coroutine = new Coroutine<T>(iteratorBlock.GetEnumerator(), scheduler);

            var cs = new TaskCompletionSource<T>(opts);

            coroutine.BeginInvoke(iasr =>
            {
                try
                {
                    cs.SetResult(coroutine.EndInvoke(iasr));
                }
                catch (Exception e)
                {
                    cs.SetException(e);
                }
            }, null);

            return cs.Task;
        }
        
        /// <summary>
        /// Returns Task&lt;object&gt; if the argument is a Task&lt;T&gt;, otherwise null.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static Task<object> AsTaskWithValue(this Task task)
        {
            // well, this is significantly less painful than the observable version...

            var taskType = task.GetType();

            if (!taskType.IsGenericType) return null;

            var tcs = new TaskCompletionSource<object>();
            task.ContinueWith(t =>
            {
                if (task.IsFaulted)
                    tcs.SetException(task.Exception);
                else
                    tcs.SetResult(taskType.GetProperty("Result").GetGetMethod().Invoke(task, null));
            });
            return tcs.Task;
        }

        public static Task<T> CreateCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.CreateCoroutine<T>(TaskScheduler.Current);
        }

        public static Task<T> CreateCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
        {
            var taskFactory = new TaskFactory(scheduler);
            var tcs = new TaskCompletionSource<T>();

            taskFactory.StartNew(() => iteratorBlock.AsCoroutine<T>().ContinueWith(t
                =>
            {
                if (t.IsFaulted)
                    tcs.SetException(t.Exception.InnerExceptions);
                else
                    tcs.SetResult(t.Result);
            }));

            return tcs.Task;
        }


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

        public static Continuation AsContinuation(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsContinuation(null);
        }

        public static Continuation AsContinuation(this IEnumerable<object> iteratorBlock, Action<Action> trampoline)
        {
            return (result, exception) =>
                Coroutine2.Continue(iteratorBlock.GetEnumerator(), result, exception, trampoline);
        }
    }
}
