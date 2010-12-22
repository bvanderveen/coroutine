# Coroutine

Coroutine was created in 2010 by Benjamin van der Veen. Coroutine is dedicated to the public domain.

# Description

Coroutine allows you to create a `System.Threading.Tasks.Task<T>` which represents the iteration of an iterator block. 

When the iterator block yields an object of the generic type used to create the coroutine Task, the coroutine Task completes with that object as its result.

If the iterator block throws an exception, the coroutine Task results in an error.

When the iterator block yields a Task, iteration does not continue until the Task successfully completes. If the yielded task results in an error, the coroutine Task results in an error.

Because the coroutine is represented by a Task, you may yield coroutines from within coroutines, allowing you to compose asynchronous operations in a natural way.

If you provide a TaskScheduler when creating a coroutine Task, the iterator block will be advanced on that TaskScheduler.

# Example

Coroutine was designed for use with [Kayak](http://github.com/kayak/kayak), a C# web server. A common use-case for Coroutine within Kayak is performing asynchronous IO. In the following example, a stream is read asynchronously and buffered into a string.

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