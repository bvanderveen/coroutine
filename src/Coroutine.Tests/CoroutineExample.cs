using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace Coroutine.Tests
{
    static class CoroutineExample
    {
        public static IEnumerable<object> ReadStreamToEnd(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            var buffer = new byte[1024];
            var bytesRead = 0;

            do
            {
                var read = stream.ReadAsync(buffer, 0, buffer.Length);
                yield return read;
                bytesRead = read.Result;


                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            while (bytesRead > 0);

            yield return sb.ToString();
        }

        static ContinuationState<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            return Extensions.AsContinuationState<int>(
                (cb, s) => stream.BeginRead(buffer, offset, count, cb, s),
                stream.EndRead);
        }
    }
}
