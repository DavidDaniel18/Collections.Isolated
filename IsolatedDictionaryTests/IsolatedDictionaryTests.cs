using System.Diagnostics;
using Collections.Isolated.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace IsolatedDictionaryTests
{
    public class IsolatedDictionaryTests : IntegrationTest
    {
        public IsolatedDictionaryTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }


        [Fact]
        public async Task BasicAdd()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            Assert.Equal("value", await dictionary.TryGetAsync("key"));
        }

        [Fact]
        public async Task BasicAdd_Concurrent()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            Assert.Null(await dictionary2.TryGetAsync("key"));

            await dictionary.SaveChangesAsync();

            Assert.Equal("value", await dictionary2.TryGetAsync("key"));
        }

        [Fact]
        public async Task Concurrent_dirty_write()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            Assert.Null(await dictionary2.TryGetAsync("key"));

            await dictionary.SaveChangesAsync();

            Exception? exception = null;

            try
            {
                await dictionary2.AddOrUpdateAsync("key", "value");
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
        }

        [Fact]
        public async Task Concurrent_Uncomitted_write_works_on_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            await dictionary.SaveChangesAsync();

            await dictionary2.AddOrUpdateAsync("key", "value2");

            Assert.Equal("value2", await dictionary2.TryGetAsync("key"));

            await dictionary2.SaveChangesAsync();

            Assert.Equal("value2", await dictionary.TryGetAsync("key"));
        }

        [Fact]
        public async Task SaveChanges_Concurrency()
        {
            Stopwatch stopwatch = new();

            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var logger = Scope2!.ServiceProvider.GetRequiredService<ILogger<IDictionaryContext<string, string>>>();

            Dictionary<string, string> dict = new();

            for (int i = 0; i < 10_000; i++)
            {
                dict.Add(i.ToString(), i.ToString());
            }

            stopwatch.Start();

            await Parallel.ForEachAsync(dict, async (kv, _) => await dictionary.AddOrUpdateAsync(kv.Key, kv.Value));

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.Elapsed}");

            _ = dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.Elapsed}");

            var collection = await dictionary2.QueryAsync(enumerable => enumerable);

            logger.LogInformation($"QueryAsync: {stopwatch.Elapsed}");

            Assert.Equal(10_000, collection.Count);
        }

        [Fact]
        public async Task SaveChanges_batch_Concurrency()
        {
            const int count = 1_000_000;

            Stopwatch stopwatch = new();

            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            var logger = Scope2!.ServiceProvider.GetRequiredService<ILogger<IDictionaryContext<string, string>>>();

            Dictionary<string, string> dict = new();

            for (int i = 0; i < count; i++)
            {
                dict.Add(i.ToString(), i.ToString());
            }

            stopwatch.Start();

            await dictionary.AddOrUpdateRange(dict.Select(kv => (kv.Key, kv.Value)).ToList());

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.Elapsed}");

            _ = dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.Elapsed}");

            var collection = await dictionary2.QueryAsync(enumerable => enumerable);

            logger.LogInformation($"QueryAsync: {stopwatch.Elapsed}");

            Assert.Equal(count, collection.Count);
        }

        [Fact]
        public async Task DeepClones()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, HeapAllocation>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, HeapAllocation>>();

            await dictionary.AddOrUpdateAsync("key", new HeapAllocation() {Value = "value"});

            await dictionary.SaveChangesAsync();

            var heapAllocation = await dictionary2.TryGetAsync("key");

            heapAllocation.Value = "value2";

            var heapAllocation2 = await dictionary.TryGetAsync("key");

            Assert.Equal("value", heapAllocation2.Value);

            await dictionary2.AddOrUpdateAsync("key", heapAllocation);

            await dictionary2.SaveChangesAsync();

            heapAllocation = await dictionary2.TryGetAsync("key");

            Assert.Equal("value2", heapAllocation.Value);
            Assert.Equal("value", heapAllocation2.Value);
        }

        [Fact]
        public async Task InterleavedTransactionsWithDependency()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();
            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            // Transaction 1 starts and updates a key
            await dictionary.AddOrUpdateAsync("key", "initial");
            await dictionary.SaveChangesAsync();

            // Transaction 2 starts, reads the key, and plans to update it based on the read value
            var valueT2Start = await dictionary2.TryGetAsync("key");
            Assert.Equal("initial", valueT2Start);

            // Transaction 1 updates the key again before Transaction 2 commits its change
            await dictionary.AddOrUpdateAsync("key", "update1");
            await dictionary.SaveChangesAsync();

            bool retryTransaction2 = false;
            try
            {
                await dictionary2.AddOrUpdateAsync("key", valueT2Start + "+update2");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("colliding with another transaction"))
            {
                retryTransaction2 = true;
            }

            if (retryTransaction2)
            {
                // Re-read the latest state and retry the operation
                var latestValue = await dictionary2.TryGetAsync("key");
                await dictionary2.AddOrUpdateAsync("key", latestValue + "+update2");
                await dictionary2.SaveChangesAsync();
            }

            // Final verification as before
            var finalValue = await dictionary.TryGetAsync("key");
            Assert.Equal("update1+update2", finalValue); // Adjust the expected value based on actual system behavior
        }

        [Fact]
        public async Task SequentialTransactionsWithDependencyOnCommittedChanges()
        {
            var dictionary1 = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();
            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            // Transaction 1 updates 'key1' and commits the change
            await dictionary1.AddOrUpdateAsync("key1", "value1");
            await dictionary1.SaveChangesAsync(); // Ensure the change is committed

            // Transaction 2 begins after Transaction 1's commit, ensuring visibility of 'key1's update
            var key1Value = await dictionary2.TryGetAsync("key1");
            Assert.Equal("value1", key1Value); // 'key1' update by Transaction 1 should be visible to Transaction 2

            // Transaction 2 updates 'key2' based on 'key1's committed value, then updates 'key1'
            await dictionary2.AddOrUpdateAsync("key2", key1Value + "+updateFromT2");
            await dictionary2.AddOrUpdateAsync("key1", "value1UpdatedByT2");
            await dictionary2.SaveChangesAsync(); // Commit Transaction 2's changes

            // Verify the final state reflects the sequential updates from both transactions
            var finalKey1Value = await dictionary1.TryGetAsync("key1");
            var finalKey2Value = await dictionary1.TryGetAsync("key2");

            Assert.Equal("value1UpdatedByT2", finalKey1Value); // Reflects Transaction 2's update
            Assert.Equal("value1+updateFromT2", finalKey2Value); // Reflects Transaction 2's dependent update on 'key2'
        }

        [Fact]
        public async Task HighConcurrencyStressTestWithScopedTransactions()
        {
            const int numberOfTransactions = 100; // Number of concurrent transactions
            const int numberOfKeysPerTransaction = 10; // Keys operated on by each transaction
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

            // Prepopulate the dictionary with initial values
            for (int i = 0; i < numberOfTransactions * numberOfKeysPerTransaction; i++)
            {
                await rootDictionary.AddOrUpdateAsync($"key{i}", $"initial{i}");
            }
            await rootDictionary.SaveChangesAsync();

            // Define a task for each transaction, each within its own scope
            var transactionTasks = new List<Task>();
            for (int t = 0; t < numberOfTransactions; t++)
            {
                var transactionTask = Task.Run(async () =>
                {
                    // Create a new scope for each transaction
                    using var scope = base.CreateScope();
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string, string>>();

                    // Each transaction reads, modifies, and updates a set of keys
                    for (int k = 0; k < numberOfKeysPerTransaction; k++)
                    {
                        int keyIndex = (t * numberOfKeysPerTransaction + k) % (numberOfTransactions * numberOfKeysPerTransaction);
                        string key = $"key{keyIndex}";
                        string newValue = $"updatedByTransaction{t}";

                        // Simulate read-modify-write cycle within the transaction scope
                        //var currentValue = await localDictionary.TryGetAsync(key);
                        //var updatedValue = $"{currentValue}+{newValue}";
                        await localDictionary.AddOrUpdateAsync(key, newValue);
                    }
                    await localDictionary.SaveChangesAsync();
                });
                transactionTasks.Add(transactionTask);
            }

            // Wait for all transactions to complete
            await Task.WhenAll(transactionTasks);

            // Validate the final state of the dictionary
            for (int i = 0; i < numberOfTransactions * numberOfKeysPerTransaction; i++)
            {
                string key = $"key{i}";
                var value = await rootDictionary.TryGetAsync(key);
                // Ensure that the value reflects updates from one or more transactions.
                Assert.Contains($"updatedByTransaction", value); // Simplified check; adjust based on expected outcome
            }
        }

        public class HeapAllocation
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}