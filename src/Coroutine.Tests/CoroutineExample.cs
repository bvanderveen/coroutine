using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace Coroutine.Tests
{
    static class CoroutineExample
    {
        // this thing is roughly equivalent to:
        //public async Task<string> ReadStreamToEnd(Stream stream)
        //{
        //    ...

        //    var bytesRead = await steam.ReadAsync(buffer, 0, buffer.Length);
            
        //    ...
        //}
        public static IEnumerable<object> ReadStreamToEnd(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            var buffer = new byte[1024];
            var bytesRead = 0;

            do
            {
                //var read = stream.ReadAsync(buffer, 0, buffer.Length);
                //yield return read;

                //bytesRead = read.GetResult<int>();

                var read = stream.ReadAsyncTask(buffer, 0, buffer.Length);
                yield return read;

                bytesRead = read.Result;

                //bytesRead = stream.Read(buffer, 0, buffer.Length);

                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            while (bytesRead > 0);

            yield return sb.ToString();
        }

        static Task<int> ReadAsyncTask(this Stream stream, byte[] buffer, int offset, int count)
        {
            return Task.Factory.FromAsync<int>(
                (cb, s) => stream.BeginRead(buffer, 0, buffer.Length, cb, s),
                iasr => stream.EndRead(iasr), null);
        }

        static ContinuationState ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            return Extensions.SetCurrentContinuation(Extensions.AsContinuation(
                (cb, s) => stream.BeginRead(buffer, offset, count, cb, s),
                stream.EndRead));
        }
    }
}
