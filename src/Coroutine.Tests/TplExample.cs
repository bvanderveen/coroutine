using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace Coroutine.Tests
{
    class TplExample
    {
        public static Task<string> ReadStreamToEnd(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            var buffer = new byte[1024];

            var tcs = new TaskCompletionSource<string>();

            StartRead(tcs, stream, sb, buffer);

            return tcs.Task;
        }

        static void StartRead(TaskCompletionSource<string> tcs, Stream stream, StringBuilder sb, byte[] buffer)
        {
            Task.Factory.FromAsync<int>(
                (cb, s) => stream.BeginRead(buffer, 0, buffer.Length, cb, s),
                iasr => stream.EndRead(iasr), null).ContinueWith(t => ReadCompleted(t, tcs, stream, sb, buffer));
        }

        static void ReadCompleted(Task<int> read, TaskCompletionSource<string> tcs, Stream stream, StringBuilder sb, byte[] buffer)
        {
            int bytesRead = 0;

            try
            {
                bytesRead = read.Result;
            }
            catch (Exception e)
            {
                tcs.SetException(e);
                return;
            }

            if (bytesRead == 0)
            {
                tcs.SetResult(sb.ToString());
            }
            else
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                StartRead(tcs, stream, sb, buffer);
            }
        }
    }
}
