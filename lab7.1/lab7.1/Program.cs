using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

class Program
{
    // Кількість доступних ресурсів
    private static int cpuResources = 2;
    private static int ramResources = 4;
    private static int diskResources = 2;

    // Семафори для кожного ресурсу
    private static SemaphoreSlim cpuSemaphore = new SemaphoreSlim(cpuResources);
    private static SemaphoreSlim ramSemaphore = new SemaphoreSlim(ramResources);
    private static SemaphoreSlim diskSemaphore = new SemaphoreSlim(diskResources);

    // Черга пріоритетів
    private static readonly object priorityLock = new object();
    private static SortedList<int, Queue<Action>> priorityQueue = new SortedList<int, Queue<Action>>();

    static void Main()
    {
        // Створення потоків із різними пріоритетами
        var threads = new List<Thread>
        {
            new Thread(() => RequestResources(1, 1, 1, 1)), // Потік з високим пріоритетом
            new Thread(() => RequestResources(2, 1, 2, 1)),
            new Thread(() => RequestResources(3, 2, 1, 1)),
            new Thread(() => RequestResources(1, 0, 1, 1)), // Потік з низьким пріоритетом
        };

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        Console.WriteLine("Симуляцiя завершена.");
    }

    static void RequestResources(int priority, int cpuNeeded, int ramNeeded, int diskNeeded)
    {
        lock (priorityLock)
        {
            if (!priorityQueue.ContainsKey(priority))
            {
                priorityQueue.Add(priority, new Queue<Action>());
            }

            priorityQueue[priority].Enqueue(() => AccessResources(cpuNeeded, ramNeeded, diskNeeded));

            // Спроба виконати потік, якщо ресурси доступні
            ProcessQueue();
        }
    }

    static void ProcessQueue()
    {
        foreach (var key in priorityQueue.Keys.ToList())
        {
            var queue = priorityQueue[key];

            if (queue.Count > 0)
            {
                var action = queue.Peek();

                // Запускаємо дію, якщо є доступні ресурси
                if (TryExecuteAction(action))
                {
                    queue.Dequeue();
                    break;
                }
            }
        }
    }

    static bool TryExecuteAction(Action action)
    {
        try
        {
            action.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void AccessResources(int cpuNeeded, int ramNeeded, int diskNeeded)
    {
        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Запит ресурсiв: CPU={cpuNeeded}, RAM={ramNeeded}, Disk={diskNeeded}");

        bool resourcesAcquired = false;

        while (!resourcesAcquired)
        {
            resourcesAcquired = cpuSemaphore.Wait(0) && ramSemaphore.Wait(0) && diskSemaphore.Wait(0);

            if (!resourcesAcquired)
            {
                // Звільнення частково зайнятих ресурсів
                if (resourcesAcquired)
                {
                    cpuSemaphore.Release();
                    ramSemaphore.Release();
                    diskSemaphore.Release();
                }
                Thread.Sleep(100); // Очікування перед повторною спробою
            }
        }

        try
        {
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Ресурси видiленi: CPU={cpuNeeded}, RAM={ramNeeded}, Disk={diskNeeded}");
            Thread.Sleep(2000); // Використання ресурсів
        }
        finally
        {
            cpuSemaphore.Release(cpuNeeded);
            ramSemaphore.Release(ramNeeded);
            diskSemaphore.Release(diskNeeded);

            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Ресурси звiльнено.");
        }
    }
}