using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Coroutine.Tests
{
    static class RxExample
    {
        public static IObservable<string> ReadStreamToEnd(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            var buffer = new byte[1024];

            return Observable.Create<string>(o =>
                {
                    StartRead(stream, sb, buffer, o);
                    return () => { };
                });
        }

        static void StartRead(Stream stream, StringBuilder sb, byte[] buffer, IObserver<string> observer)
        {
            int bytesRead = 0;
            Exception exception = null;
            stream.ReadAsync(buffer, 0, buffer.Length).Subscribe(n => bytesRead = n, e => exception = e, () =>
                {
                    if (exception != null)
                        observer.OnError(exception);

                    if (bytesRead == 0)
                        observer.OnNext(sb.ToString());
                    else
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        StartRead(stream, sb, buffer, observer);
                    }
                });
        }

        static IObservable<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            return Observable.FromAsyncPattern<int>(
                (cb, s) => stream.BeginRead(buffer, offset, count, cb, s), 
                stream.EndRead)();
        }
    }
}
