using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using System.Threading;

namespace Coroutine.Tests
{
    [TestFixture]
    public class ExampleTests
    {
        Stream stream;
        string testData;
        ManualResetEvent wh;
        string result;
        Exception exception;


        [SetUp]
        public void SetUp()
        {
            testData = "asdf blah blah blah vblah";
            stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));

            wh = new ManualResetEvent(false);
            result = null;
            exception = null;
        }

        [TearDown]
        public void TearDown()
        {
            wh.Close();
        }

        [Test]
        public void Coroutine()
        {
            CoroutineExample.ReadStreamToEnd(stream).GetEnumerator().AsContinuation<object>()(
                r =>
                {
                    result = (string)r;
                    wh.Set();
                },
                e =>
                {
                    exception = e;
                    wh.Set();
                });

            wh.WaitOne();

            Assert.IsNull(exception);
            Assert.AreEqual(testData, result);
        }

        [Test]
        public void Tpl()
        {
            TplExample.ReadStreamToEnd(stream).ContinueWith(t =>
            {
                try
                {
                    result = t.Result;
                }
                catch (Exception e)
                {
                    exception = e;
                }
                wh.Set();
            });

            wh.WaitOne();

            Assert.IsNull(exception);
            Assert.AreEqual(testData, result);
        }

        [Test]
        public void Rx()
        {
            RxExample.ReadStreamToEnd(stream).Subscribe(r =>
                {
                    result = r;
                    wh.Set();
                },
                e =>
                {
                    exception = e;
                    wh.Set();
                });

            wh.WaitOne();

            Assert.IsNull(exception);
            Assert.AreEqual(testData, result);
        }
    }
}
