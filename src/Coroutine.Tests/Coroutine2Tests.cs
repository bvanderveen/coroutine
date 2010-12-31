using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading.Tasks;
using Moq;
using System.Threading;

namespace Coroutine.Tests
{
    [TestFixture]
    public class Coroutine2Tests
    {
        [Test]
        public void ReturnsNull()
        {
            var mockDisposable = new Mock<IDisposable>();

            var task = ReturnsNullBlock(mockDisposable.Object).StartCoroutineTask();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.IsNull(task.Result);
        }

        IEnumerable<object> ReturnsNullBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return null;
            }
        }

        [Test]
        public void ReturnsResult()
        {
            var mockDisposable = new Mock<IDisposable>();

            var task = ReturnsResultBlock(mockDisposable.Object).StartCoroutineTask();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual("result", task.Result);
        }

        IEnumerable<object> ReturnsResultBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return "result";
            }
        }

        [Test]
        public void ImmediateException()
        {
            var mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Task task = null;
            Exception caughtException = null;

            try
            {
                var block = ImmediateExceptionBlock(exception, mockDisposable.Object);

                task = block.AsContinuation(_ => { }).AsTask();
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.AreEqual(exception, caughtException, "Exceptions differ.");
        }

        IEnumerable<object> ImmediateExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                Console.WriteLine("throwing exception.");
                throw exception;
            }
        }

        [Test]
        public void DeferredException()
        {
            var mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Task task = null;
            Exception caughtException = null;

            try
            {
                task = DeferredExceptionBlock(exception, mockDisposable.Object)
                    //.AsCoroutine2<string>(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
                    //.AsTask();
                    .StartCoroutineTask();
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsNull(caughtException, "Coroutine constructor threw up.");
            Assert.IsNotNull(task.Exception, "Coroutine didn't have exception.");
            Assert.AreEqual(exception, task.Exception.InnerException, "Exceptions differ.");
        }

        IEnumerable<object> DeferredExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                yield return Task.Factory.StartNew(() => { });
                throw exception;
            }
        }

        [Test]
        public void RunsTask()
        {
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            mockDisposable.Setup(d => d.Dispose()).Callback(() => Console.WriteLine("Dispose."));

            bool taskRun = false;
            Action subTask = () =>
            {
                Thread.Sleep(0);
                Console.WriteLine("Sub task run.");
                taskRun = true;
            };

            var task = RunsTaskBlock(subTask, mockDisposable.Object).StartCoroutineTask();
            task.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.IsTrue(taskRun, "Task was not run.");
        }

        IEnumerable<object> RunsTaskBlock(Action task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return Task.Factory.StartNew(task);
                yield return null;
            }
        }

        [Test]
        public void PropagatesValueFromTask()
        {
            var mockDisposable = new Mock<IDisposable>();

            int value = 42;

            Func<int> subTask = () =>
            {
                Thread.Sleep(0); 
                return value;
            };

            var task = PropagatesValuesFromTaskBlock(subTask, mockDisposable.Object).StartCoroutineTask();
            task.Wait();
            
            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual(value, task.Result, "Values differ.");
        }

        IEnumerable<object> PropagatesValuesFromTaskBlock(Func<int> func, IDisposable disposable)
        {
            using (disposable)
            {
                var task = Task.Factory.StartNew(func);
                yield return task;
                yield return task.Result;
            }
        }

        [Test]
        public void ReturnsResultFromNestedCoroutine()
        {
            var task = ReturnsResultFromNestedCoroutineBlock().StartCoroutineTask();
            task.Wait();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.IsFaulted);
            Assert.AreEqual(52, task.Result);
        }

        public static IEnumerable<object> ReturnsResultFromNestedCoroutineBlock()
        {
            //Assert.AreEqual(typeof(SingleThreadedTaskScheduler), TaskScheduler.Current.GetType());
            var inner = ReturnsResultFromNestedCoroutineBlockInner().AsCoroutine2<int>();
            yield return inner;
            Console.WriteLine("result is " + inner.Result);
            yield return inner.Result;
        }

        static IEnumerable<object> ReturnsResultFromNestedCoroutineBlockInner()
        {
            // logic...
            yield return 52;
        }

        [Test]
        public void PropagatesExceptionFromNestedCoroutine()
        {
            Exception e = new Exception("Boo.");

            var task = PropagatesExceptionFromNestedCoroutineBlock(e)
                    .AsContinuation(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
                    .AsTask();
            Thread.SpinWait(10000);

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(e, task.Exception.InnerException);
        }

        public static IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlock(Exception e)
        {
            var inner = PropagatesExceptionFromNestedCoroutineBlockInner(e).AsCoroutine2<int>();
            yield return inner;

            // should throw
            yield return inner.Result;
        }

        static IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlockInner(Exception e)
        {
            throw e;
        }
    }
}
