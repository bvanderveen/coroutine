using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
