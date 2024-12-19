using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Operation
{
    public DateTime Timestamp { get; }
    public string ThreadId { get; }
    public string ResourceId { get; }
    public string Action { get; }

    public Operation(string threadId, string resourceId, string action)
    {
        Timestamp = DateTime.Now;
        ThreadId = threadId;
        ResourceId = resourceId;
        Action = action;
    }
}

class ConflictLog
{
    private readonly ConcurrentDictionary<string, Operation> _resourceLocks = new ConcurrentDictionary<string, Operation>();
    private readonly List<Operation> _log = new List<Operation>();
    private readonly ConcurrentQueue<(Operation, Operation)> _conflicts = new ConcurrentQueue<(Operation, Operation)>();

    private readonly object _logLock = new object();

    public bool TryAddOperation(Operation operation)
    {
        lock (_logLock)
        {
            if (_resourceLocks.TryGetValue(operation.ResourceId, out var existingOperation))
            {
                if ((operation.Timestamp - existingOperation.Timestamp).TotalMilliseconds < 50)
                {
                    _conflicts.Enqueue((existingOperation, operation));
                    return false;
                }
            }

            _resourceLocks[operation.ResourceId] = operation;
            _log.Add(operation);
            return true;
        }
    }

    public void ResolveConflicts()
    {
        while (_conflicts.TryDequeue(out var conflict))
        {
            Console.WriteLine($"Conflict detected: Resource {conflict.Item1.ResourceId} between threads {conflict.Item1.ThreadId} and {conflict.Item2.ThreadId}.");

            if (conflict.Item1.Timestamp <= conflict.Item2.Timestamp)
            {
                Console.WriteLine($"Resolving conflict in favor of thread {conflict.Item1.ThreadId}.");
            }
            else
            {
                Console.WriteLine($"Resolving conflict in favor of thread {conflict.Item2.ThreadId}.");
                _resourceLocks[conflict.Item2.ResourceId] = conflict.Item2;
            }
        }
    }

    public void PrintLog()
    {
        lock (_logLock)
        {
            Console.WriteLine("Log of operations:");
            foreach (var operation in _log)
            {
                Console.WriteLine($"[{operation.Timestamp:O}] Thread {operation.ThreadId} on Resource {operation.ResourceId}: {operation.Action}");
            }
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        var conflictLog = new ConflictLog();
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            int threadIndex = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var operation = new Operation($"Thread-{threadIndex}", $"Resource-{j % 3}", "Write");
                    if (!conflictLog.TryAddOperation(operation))
                    {
                        Console.WriteLine($"Thread-{threadIndex}: Conflict detected for resource {operation.ResourceId}.");
                    }

                    Thread.Sleep(new Random().Next(10, 100));
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        conflictLog.ResolveConflicts();
        conflictLog.PrintLog();
    }
}
