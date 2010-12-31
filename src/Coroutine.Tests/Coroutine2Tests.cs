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
        public void ReturnsResult()
        {
            var mockDisposable = new Mock<IDisposable>();

            var task = CoroutineTests.ReturnsResultBlock(mockDisposable.Object).StartCoroutineTask<string>();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual("result", task.Result);
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
                var block = CoroutineTests.ImmediateExceptionBlock(exception, mockDisposable.Object);

                task = block.AsContinuation<string>(_ => { }).AsTask();
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.AreEqual(exception, caughtException, "Exceptions differ.");
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
                task = CoroutineTests.DeferredExceptionBlock(exception, mockDisposable.Object)
                    //.AsCoroutine2<string>(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
                    //.AsTask();
                    .StartCoroutineTask<string>();
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

            var task = CoroutineTests.RunsTaskBlock(subTask, mockDisposable.Object).StartCoroutineTask<object>();
            task.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.IsTrue(taskRun, "Task was not run.");
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

            var task = CoroutineTests.PropagatesValuesFromTaskBlock(subTask, mockDisposable.Object).StartCoroutineTask<int>();
            task.Wait();
            
            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual(value, task.Result, "Values differ.");
        }

        [Test]
        public void Covar()
        {
            Continuation<object> c = new Continuation<Tuple<int>>((r, e) => r(new Tuple<int>(0)));

            Assert.IsTrue(c.GetType().IsGenericType);
            Assert.IsTrue(c.GetType().GetGenericTypeDefinition() == typeof(Continuation<>));
            Assert.

            var inner = ReturnsResultFromNestedCoroutineBlockInner().AsCoroutine2<int>();
            Assert.IsTrue(inner is IContinuationState<int>);
        }

        [Test]
        public void ReturnsResultFromNestedCoroutine()
        {
            var task = ReturnsResultFromNestedCoroutineBlock().StartCoroutineTask<int>();
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
            yield return 52;
        }

        [Test]
        public void PropagatesExceptionFromNestedCoroutine()
        {
            Exception e = new Exception("Boo.");

            var task = CoroutineTests.PropagatesExceptionFromNestedCoroutineBlock(e)
                    .AsContinuation<string>(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
                    .AsTask();
            Thread.SpinWait(10000);

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(e, task.Exception.InnerException);
        }
    }
}
