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
                // synchronous example--would work! probably gum up your throughput though
                //bytesRead = stream.Read(buffer, 0, buffer.Length);

                //var read = stream.ReadAsync(buffer, 0, buffer.Length);
                //yield return read;
                //bytesRead = read.GetResult<int>();

                var read = stream.ReadAsync(buffer, 0, buffer.Length);
                yield return read;
                Console.WriteLine("read " + read.Result);
                bytesRead = read.Result;

                // impossible.
                //Exception ex = null;
                //yield return stream.ReadAsyncC(buffer, 0, buffer.Length)(n => bytesRead = n, e => ex = e); 

                //var read = stream.ReadAsyncTask(buffer, 0, buffer.Length);
                //yield return read;
                //bytesRead = read.Result;


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

        static ContinuationState<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            return new ContinuationState<int>(Extensions.AsContinuation(
                (cb, s) => stream.BeginRead(buffer, offset, count, cb, s),
                stream.EndRead));
        }
    }
}
