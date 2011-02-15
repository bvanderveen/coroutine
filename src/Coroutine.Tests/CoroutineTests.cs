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

            ReturnsNullBlock(mockDisposable.Object).AsContinuation<object>()
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
        public void Flat_CompletesOnBreak()
        {
            var mockDisposable = new Mock<IDisposable>();

            bool gotResult = false;

            ReturnsNullBlock(mockDisposable.Object).AsContinuation()
                (() => { gotResult = true; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(gotResult);
            Assert.IsNull(result);
            Assert.IsNull(exception);
        }

        IEnumerable<object> CompletesOnBreakBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield break;
            }
        }

        [Test]
        public void Flat_ReturnsResult()
        {
            var mockDisposable = new Mock<IDisposable>();

            ReturnsResultBlock(mockDisposable.Object).AsContinuation<object>()
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
                ImmediateExceptionBlock(exception, mockDisposable.Object).AsContinuation<object>(_ => { })
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
                DeferredExceptionBlock(exception, mockDisposable.Object).AsContinuation<object>()
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
        public void Nest_Completes()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_CompletesBlock(mockDisposable.Object).AsContinuation<object>()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.AreEqual("result", result);
        }

        IEnumerable<object> Nest_CompletesBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return new ContinuationState<object>((r, e) => r(null));
                yield return "result";
            }
        }

        [Test]
        public void Nest_ResultState()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ResultState(mockDisposable.Object).AsContinuation<object>()
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
                var state = new ContinuationState<object>((r, e) => r("result"));
                yield return state;
                yield return state.Result;
            }
        }

        [Test]
        public void Nest_ResultStateTrampoline()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ResultStateTrampoline(mockDisposable.Object).AsContinuation<object>(a => { new Thread(() => a()).Start(); })
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
                var state = new ContinuationState<object>((r, e) => r("result"));
                yield return state;
                yield return state.Result;
            }
        }

        [Test]
        public void Nest_ExceptionState()
        {
            var mockDisposable = new Mock<IDisposable>();

            bool gotResult = false;
            bool gotException = false;
            Nest_ExceptionState(mockDisposable.Object).AsContinuation<object>()
                (r => { gotResult = true; result = r; wh.Set(); }, e => { gotException = true; exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsFalse(gotResult);
            Assert.IsTrue(gotException);
            Assert.IsNull(result);
            Assert.IsNotNull(exception);
            Assert.AreEqual("Nest_ExceptionState Exception", exception.InnerException.InnerException.Message);
        }

        IEnumerable<object> Nest_ExceptionState(IDisposable disposable)
        {
            using (disposable)
            {
                var state = new ContinuationState<object>((r, e) => e(new Exception("Nest_ExceptionState Exception")));
                yield return state;
                yield return state.Result;
            }
        }

        [Test]
        public void Nest_ExceptionStateTrampoline()
        {
            var mockDisposable = new Mock<IDisposable>();

            Nest_ExceptionStateTrampoline(mockDisposable.Object).AsContinuation<object>(a => { new Thread(() => a()).Start(); })
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(result);
            Assert.AreEqual("Nest_ExceptionState Exception", exception.InnerException.InnerException.Message);
        }

        IEnumerable<object> Nest_ExceptionStateTrampoline(IDisposable disposable)
        {
            using (disposable)
            {
                var state = new ContinuationState<object>((r, e) => e(new Exception("Nest_ExceptionState Exception")));
                yield return state;
                yield return state.Result;
            }
        }

        [Test]
        public void TaskCompletes()
        {
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            //mockDisposable.Setup(d => d.Dispose()).Callback(() => Console.WriteLine("Dispose."));

            bool taskRun = false;
            Action subTask = () =>
            {
                Thread.Sleep(0);
                Console.WriteLine("Sub task run.");
                taskRun = true;
            };

            TaskCompletesBlock(subTask, mockDisposable.Object).AsContinuation<object>()
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

        [Test]
        public void NestedBlock_ReturnsResultFromNestedBlock()
        {
            var mockDisposable0 = new Mock<IDisposable>();
            var mockDisposable1 = new Mock<IDisposable>();
            var o = new object();

            NestedBlock_ReturnsResultFromNestedBlock(mockDisposable0.Object, mockDisposable1.Object, o).AsContinuation<object>()
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            mockDisposable0.Verify(d => d.Dispose(), Times.Once());
            mockDisposable1.Verify(d => d.Dispose(), Times.Once());

            Assert.IsNull(exception);
            Assert.AreEqual(o, result);
        }

        public static IEnumerable<object> NestedBlock_ReturnsResultFromNestedBlock(IDisposable d0,IDisposable d1, object o)
        {
            using (d0)
            {
                var inner = ReturnsResultFromNestedCoroutineBlockInner(d1, o).AsCoroutine<object>();
                yield return inner;
                //Console.WriteLine("jkl?");
                yield return inner.Result;
                //Console.WriteLine("jkl");
            }
        }

        static IEnumerable<object> ReturnsResultFromNestedCoroutineBlockInner(IDisposable d, object o)
        {
            using (d)
            {
                //Console.WriteLine("asdf?");
                yield return o;
                //Console.WriteLine("asdf...");
                yield return "asdf";
                //Console.WriteLine("asdf");
            }
        }

        [Test]
        public void NestedBlock_PropagatesExceptionFromNestedBlock()
        {
            var mockDisposable0 = new Mock<IDisposable>();
            var mockDisposable1 = new Mock<IDisposable>();
            Exception ex = new Exception("Boo.");

            var mockDisposable = new Mock<IDisposable>();

            NestedBlock_PropagatesExceptionFromNestedBlock(ex, mockDisposable0.Object, mockDisposable1.Object)
                .AsContinuation<object>(a => { Thread.Sleep(0); ThreadPool.QueueUserWorkItem(_ => a()); })
                (r => { result = r; wh.Set(); }, e => { exception = e; wh.Set(); });
            wh.Wait();

            Assert.IsNull(result);
            Assert.IsNotNull(exception);
            Assert.AreEqual(ex, exception.InnerException);
        }

        public static IEnumerable<object> NestedBlock_PropagatesExceptionFromNestedBlock(Exception e, IDisposable d0, IDisposable d1)
        {
            using (d0)
            {
                var inner = PropagatesExceptionFromNestedCoroutineBlockInner(e, d1).AsCoroutine<int>();
                yield return inner;
                yield return inner.Result; // should throw
            }
        }

        static IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlockInner(Exception e, IDisposable d)
        {
            using (d)
            {
                throw e;
            }
        }
    }
}
