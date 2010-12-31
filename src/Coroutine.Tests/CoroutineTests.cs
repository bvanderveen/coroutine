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
    public class CoroutineTests
    {
        ManualResetEventSlim wh;
        object result;
        Exception exception;

        [SetUp]
        public void SetUp()
        {
            wh = new ManualResetEventSlim(false);
            result = null;
            exception = null;
        }

        [TearDown]
        public void TearDown()
        {
            wh.Dispose();
        }

        [Test]
        public void Flat_ReturnsNull()
        {
            var mockDisposable = new Mock<IDisposable>();

            bool gotResult = false;

            ReturnsNullBlock(mockDisposable.Object).AsContinuation()
                (r => { gotResult = true; result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(gotResult);
            Assert.IsNull(result);
            Assert.IsNull(exception);
        }

        IEnumerable<object> ReturnsNullBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return null;
            }
        }

        [Test]
        public void Flat_ReturnsResult()
        {
            var mockDisposable = new Mock<IDisposable>();

            ReturnsResultBlock(mockDisposable.Object).AsContinuation()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.AreEqual("result", result);
            Assert.IsNull(exception);
        }

        IEnumerable<object> ReturnsResultBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return "result";
            }
        }

        [Test]
        public void Flat_ImmediateException()
        {
            var mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Exception caughtException = null;
            object result = null;
            Exception yieldedException = null;

            try
            {
                ImmediateExceptionBlock(exception, mockDisposable.Object).AsContinuation(_ => { })
                    (r => result = r, e => yieldedException = e);
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsNull(yieldedException);
            Assert.IsNull(result);
            Assert.AreEqual(exception, caughtException, "Exceptions differ.");
        }

        IEnumerable<object> ImmediateExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                throw exception;
            }
        }

        //[Test]
        public void Flat_DeferredException()
        {
            var mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Exception caughtException = null;
            object result = null;
            Exception yieldedException = null;

            try
            {
                DeferredExceptionBlock(exception, mockDisposable.Object).AsContinuation()
                    (r => { result = r; wh.Set(); }, e => { yieldedException = e; wh.Set(); });
                wh.Wait();
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsNull(caughtException, "Coroutine constructor threw up.");
            Assert.IsNull(result);
            Assert.AreEqual(yieldedException, exception);
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
        public void Nest_PropagesException()
        {
            var mockDisposable = new Mock<IDisposable>();

            bool gotResult = false;
            bool gotException = false;
            Nest_PropagatesExceptionBlock(mockDisposable.Object).AsContinuation()
                (r => { gotResult = true; result = r; wh.Set(); }, e => { gotException = true; exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsFalse(gotResult);
            Assert.IsTrue(gotException);
            Assert.IsNull(result);
            Assert.IsNotNull(exception);
            Assert.AreEqual("ContinuationException", exception.Message);
        }

        IEnumerable<object> Nest_PropagatesExceptionBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return Extensions.SetCurrentContinuation((r, e) => e(new Exception("ContinuationException")));
                var result = ContinuationState.current.GetResult<object>();
                yield return result;
            }
        }

        [Test]
        public void Nest_Completes()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_CompletesBlock(mockDisposable.Object).AsContinuation()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.AreEqual("result", result);
        }

        IEnumerable<object> Nest_CompletesBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return new Continuation((r, e) => r(null));
                yield return "result";
            }
        }

        [Test]
        public void Nest_ResultState()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ResultState(mockDisposable.Object).AsContinuation()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(exception);
            Assert.AreEqual("result", result);
        }

        IEnumerable<object> Nest_ResultState(IDisposable disposable)
        {
            using (disposable)
            {
                var cont = new Continuation((r, e) => r("result"));
                ContinuationState.SetContinuation(ContinuationState.current, cont);
                yield return ContinuationState.current;
                yield return ContinuationState.current.GetResult<string>();
            }
        }

        [Test]
        public void Nest_ResultStateTrampoline()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ResultStateTrampoline(mockDisposable.Object).AsContinuation(a => { new Thread(() => a()).Start(); })
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(exception);
            Assert.AreEqual("result", result);
        }

        IEnumerable<object> Nest_ResultStateTrampoline(IDisposable disposable)
        {
            using (disposable)
            {
                var cont = new Continuation((r, e) => r("result"));
                ContinuationState.SetContinuation(ContinuationState.current, cont);
                yield return ContinuationState.current;
                yield return ContinuationState.current.GetResult<string>();
            }
        }

        [Test]
        public void Nest_ExceptionState()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ExceptionState(mockDisposable.Object).AsContinuation()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(result);
            Assert.AreEqual("Nest_ExceptionState Exception", exception.Message);
        }

        IEnumerable<object> Nest_ExceptionState(IDisposable disposable)
        {
            using (disposable)
            {
                yield return Extensions.SetCurrentContinuation((r, e) => e(new Exception("Nest_ExceptionState Exception")));
                yield return ContinuationState.current.GetResult<string>();
            }
        }

        [Test]
        public void Nest_ExceptionStateTrampoline()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ExceptionStateTrampoline(mockDisposable.Object).AsContinuation(a => { new Thread(() => a()).Start(); })
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(result);
            Assert.AreEqual("Nest_ExceptionState Exception", exception.Message);
        }

        IEnumerable<object> Nest_ExceptionStateTrampoline(IDisposable disposable)
        {
            using (disposable)
            {
                var cont = new Continuation((r, e) => e(new Exception("Nest_ExceptionState Exception")));
                ContinuationState.SetContinuation(ContinuationState.current, cont);
                yield return ContinuationState.current;
                yield return ContinuationState.current.GetResult<string>();
            }
        }

        [Test]
        public void TaskCompletes()
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

            TaskCompletesBlock(subTask, mockDisposable.Object).AsContinuation()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(taskRun, "Task was not run.");
            Assert.IsNull(exception);
            Assert.AreEqual("result", result);
        }

        IEnumerable<object> TaskCompletesBlock(Action task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return Task.Factory.StartNew(task);
                yield return "result";
            }
        }

        //[Test]
        //public void ReturnsResultFromNestedCoroutine()
        //{
        //    var task = ReturnsResultFromNestedCoroutineBlock().StartCoroutineTask();
        //    task.Wait();

        //    Assert.IsTrue(task.IsCompleted);
        //    Assert.IsFalse(task.IsFaulted);
        //    Assert.AreEqual(52, task.Result);
        //}

        //public static IEnumerable<object> ReturnsResultFromNestedCoroutineBlock()
        //{
        //    var inner = ReturnsResultFromNestedCoroutineBlockInner().AsCoroutine<int>();
        //    yield return inner;
        //    Console.WriteLine("result is " + inner.Result);
        //    yield return inner.Result;
        //}

        //static IEnumerable<object> ReturnsResultFromNestedCoroutineBlockInner()
        //{
        //    // logic...
        //    yield return 52;
        //}

        //[Test]
        //public void PropagatesExceptionFromNestedCoroutine()
        //{
        //    Exception e = new Exception("Boo.");

        //    var task = PropagatesExceptionFromNestedCoroutineBlock(e)
        //            .AsContinuation(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
        //            .AsTask();
        //    Thread.SpinWait(10000);

        //    Assert.IsTrue(task.IsCompleted);
        //    Assert.IsTrue(task.IsFaulted);
        //    Assert.AreEqual(e, task.Exception.InnerException);
        //}

        //public static IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlock(Exception e)
        //{
        //    var inner = PropagatesExceptionFromNestedCoroutineBlockInner(e).AsCoroutine<int>();
        //    yield return inner;

        //    // should throw
        //    yield return inner.Result;
        //}

        //static IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlockInner(Exception e)
        //{
        //    throw e;
        //}
    }
}
