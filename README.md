# Coroutine

Coroutine was created in 2010 by [Benjamin van der Veen](http://bvanderveen.com). Coroutine is dedicated to the public domain.

# Overview

Coroutine makes writing asynchronous code in C# as easy and natural as writing synchronous code. Usually, when writing asynchronous code, you have to provide a callback which gets executed when an asynchronous operation completes. Coroutine takes care of this for you and allows you to perform asynchronous operations in a single method, without lambda expressions, delegates, or callbacks.

   IEnumerable<object> MyAsyncOperation()
   {
       // construct an asynchronous operation
       var other = MyOtherAsyncOperation();
       
       // yield to it...
       yield return other;
       
       // ...execution continues after the other operation completes.
       
       // return a value.
       yield return "the result of other was " + other.Result;
   }

Coroutine defines an asynchronous operation as a delegate of the form `Action<Action<object>, Action<Exception>`&mdash;that is, a delegate which, when invoked, will eventually invoke one of the delegates passed to it as arguments, thereby providing the caller of the outer delegate with the result of the operation, or an exception.
    
The .NET Framework provides several constructs which can be adapted to a Coroutine-compatible asynchronous operation:

* The [Asynchronous Programming Model (APM)](apm) pattern (also known as the `IAsyncResult` pattern). 
* The `System.Threading.Tasks.Task` class provided by the [Task Parallel Library](http://msdn.microsoft.com/en-us/library/dd460717.aspx)
* The `System.IObservable<T>` interface provided by the [Reactive Framework](http://msdn.microsoft.com/en-us/devlabs/ee794896).
    
Coroutine includes support for all of these constructs.

# Behavior

Coroutine allows you to create an asynchronous operation which represents the iteration of an iterator block (a *coroutine*).

When the iterator block yields a regular value, the coroutine completes with that object. If the iterator block throws an exception, the coroutine results in an error.

When the iterator block yields an object which represents an asynchronous operation, iteration of the block does not continue until the yielded operation successfully completes. If the yielded operation results in an error, the coroutine results in an error.

Because the coroutine is itself an asynchronous operation, you may yield coroutines from within coroutines, allowing you to compose asynchronous operations in a natural way.

When starting a new coroutine, you may provide a trampoline delegate of the form `Action<Action>`. Whenever an asynchronous operation completes, the coroutine will call the trampoline delegate with a single argument. That argument is a delegate which will continue the iteration of the coroutines iterator block. In this way, you can control when and on what thread the coroutine advances.

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
    
    