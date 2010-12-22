using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace Coroutine.Tests
{
    class CoroutineExample
    {
        public void ReadStreamToEnd(Stream stream)
        {
            ReadStreamToEndCoroutine(stream).AsCoroutine<string>().ContinueWith(t => "Stream contained: " + t.Result);
        }

        IEnumerable<object> ReadStreamToEndCoroutine(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            var buffer = new byte[1024];
            var bytesRead = 0;

            do
            {
                // yield a task which will read from the stream asynchronously
                var read = Task.Factory.FromAsync<int>((cb, s) =>
                    {
                        return stream.BeginRead(buffer, 0, buffer.Length, cb, s);
                    }, iasr => stream.EndRead(iasr), null);

                yield return read;

                // retrieve the result. this may throw an exception if
                // the task faulted. if we don't catch the exception,
                // the coroutine task will also fault.
                bytesRead = read.Result;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            while (bytesRead > 0);

            // yield an object of type string, this will be the result of the Coroutine Task
            yield return sb.ToString();
        }
    }
}
