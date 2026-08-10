using System.Collections.Concurrent;

namespace Morpheus.Tests;

public class RandomBagTests
{
    [Fact]
    public async Task Random_PreservesBagCyclesDuringConcurrentCalls()
    {
        string[] items = ["alpha", "beta", "gamma", "delta"];
        const int workerCount = 8;
        const int cyclesPerWorker = 250;
        var bag = new RandomBag([.. items]);
        var results = new ConcurrentBag<string>();
        int readyWorkers = 0;
        var readyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task[] workers = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(async () =>
            {
                if (Interlocked.Increment(ref readyWorkers) == workerCount)
                    readyGate.SetResult();

                await startGate.Task;

                for (int cycle = 0; cycle < cyclesPerWorker; cycle++)
                {
                    foreach (string ignored in items)
                        results.Add(bag.Random());
                }
            }))
            .ToArray();

        await readyGate.Task;
        startGate.SetResult();
        await Task.WhenAll(workers);

        int expectedCountPerItem = workerCount * cyclesPerWorker;
        Assert.Equal(expectedCountPerItem * items.Length, results.Count);
        foreach (string item in items)
            Assert.Equal(expectedCountPerItem, results.Count(result => result == item));
    }
}