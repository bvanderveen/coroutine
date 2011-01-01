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

Coroutine defines an asynchronous operation as a delegate of the form `Action<Action<T>, Action<Exception>`&mdash;that is, a delegate which, when invoked, will eventually invoke one of the delegates passed to it as arguments, thereby providing the caller of the outer delegate with the result of the operation, or an exception. In the context of this library, a delegate of this form is known as a *continuation*. To support operations which do not return a result value, delegates of the form `Action<Action, Action<Exception>` are also considered continuations.
    
The .NET Framework provides several constructs which can be adapted to a Coroutine-compatible continuation:

* The [Asynchronous Programming Model (APM)](apm) pattern (also known as the `IAsyncResult` pattern). 
* The `System.Threading.Tasks.Task` class provided by the [Task Parallel Library](http://msdn.microsoft.com/en-us/library/dd460717.aspx)
* The `System.IObservable<T>` interface provided by the [Reactive Framework](http://msdn.microsoft.com/en-us/devlabs/ee794896).
    
Coroutine includes support for all of these constructs.

# Behavior

Coroutine allows you to create an asynchronous operation which represents the iteration of an iterator block (a *coroutine*).

When the iterator block yields a value of the generic type it was initialized with, the coroutine continuation completes with that value. If the iterator block throws an exception, the coroutine continuation results in an error.

When the iterator block yields a continuation, the iteration of the block does not continue until continuation completes. The iteration of the block will continue regardless of if the continuation yields an exception, so you much check for this.

Because the coroutine can be represented as a continuation, you may yield coroutines from within coroutines, allowing you to compose asynchronous operations in a natural, familiar way, which resembles synchronous programming.

When starting a new coroutine, you may provide a trampoline delegate of the form `Action<Action>`. Whenever an asynchronous operation completes, the coroutine will call the trampoline delegate with a single argument. That argument is a delegate which will continue the iteration of the coroutines iterator block. In this way, you can control when and on what thread the coroutine advances.

# Example

Coroutine was designed for use with [Kayak](http://github.com/kayak/kayak), a C# web server. A common use-case for Coroutine within Kayak is performing asynchronous IO. In the following example, a stream is read asynchronously and buffered into a string. Notice that the async operation that's being yielded is an instance of `ContinuationState<int>`. This class creates a new continuation which wraps the original, allowing `ContinuationState` to capture the result or exception of the continuation, and expose it so that the enumerator block scope may act upon them.

    public void ReadStreamToEnd(Stream stream)
    {
        ReadStreamToEndCoroutine(stream).AsContinuation<string>()
            (r => Console.WriteLine("Stream contained: " + r),
            e => Console.WriteLine("Exception while reading: " + e.Message));
    }

    public static IEnumerable<object> ReadStreamToEndCoroutine(Stream stream)
    {
        StringBuilder sb = new StringBuilder();
        var buffer = new byte[1024];
        var bytesRead = 0;

        do
        {
            
            // get an object which represents an asynchronous read
            var read = stream.ReadAsync(buffer, 0, buffer.Length); 
            yield return read; // yield to it
            bytesRead = read.Result; // returns result or throws exception

            sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }
        while (bytesRead > 0);

        yield return sb.ToString();
    }

    static ContinuationState<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
    {
        return ConinuationState.FromAsync<int>(
            (cb, s) => stream.BeginRead(buffer, offset, count, cb, s),
            stream.EndRead);
    }
    
    