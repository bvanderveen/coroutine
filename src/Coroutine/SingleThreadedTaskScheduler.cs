using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutine
{
    public class SingleThreadedTaskScheduler : TaskScheduler, IDisposable
    {
        [ThreadStatic]
        static bool isWorker;

        LinkedList<Task> queue;
        AutoResetEvent wh, stopWh;
        bool stopRequested;
        Thread worker;

        public SingleThreadedTaskScheduler()
        {
            queue = new LinkedList<Task>();
            wh = new AutoResetEvent(false);
        }

        public void Start()
        {
            if (worker != null) throw new InvalidOperationException("Already started!");

            worker = new Thread(() => { Run(); }) { IsBackground = true };
            worker.Start();
        }

        void Run()
        {
            isWorker = true;

            while (true)
            {
                //Console.WriteLine("Scheduler waiting.");
                wh.WaitOne();
                //Console.WriteLine("Scheduler set.");

                try
                {
                    while (true)
                    {
                        Task item;

                        lock (queue)
                        {
                            if (queue.Count == 0)
                            {
                                //Console.WriteLine("No more items in queue!");
                                break;
                            }

                            item = queue.First.Value;
                            queue.RemoveFirst();
                        }

                        //Console.WriteLine("Executing task. " + item);
                        TryExecuteTask(item);
                        //Console.WriteLine("'Done' executing.");
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    Console.WriteLine("Exception on worker!");
                    Console.WriteLine("[{0}] {1} {2}", e.GetType().Name, e.Message, e.StackTrace);
#endif
                }

                if (stopRequested)
                {
                    //Console.WriteLine("Scheduler stopping!");
                    break;
                }
            }

            stopWh.Set();

            isWorker = false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (queue)
                return queue.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            lock (queue)
            {
                queue.AddLast(task);

                //Console.WriteLine("Queued task, setting wh.");
                wh.Set();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (!isWorker)
            {
                return false;
            }

            if (taskWasPreviouslyQueued)
            {
                TryDequeue(task);
            }

            var result = TryExecuteTask(task);

            return result;
        }

        protected override bool TryDequeue(Task task)
        {
            bool result = false;
            lock (queue)
                result = queue.Remove(task);

            return result;
        }

        public void Dispose()
        {
            stopRequested = true;
            stopWh = new AutoResetEvent(false);
            wh.Set();
            stopWh.WaitOne();
            stopWh.Close();
            wh.Close();
            worker.Join();
        }
    }
}
