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
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

            Assert.Equal("value", dictionary.TryGet("key"));
        }

        [Fact]
        public async Task BasicAdd_Concurrent()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

                Assert.Null(dictionary2.TryGet("key"));

            await dictionary.SaveChangesAsync();

            Assert.Equal("value", dictionary2.TryGet("key"));
        }

        [Fact]
        public async Task Concurrent_dirty_write_allowed()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

            Assert.Null(dictionary2.TryGet("key"));

            await dictionary.SaveChangesAsync();

            Exception? exception = null;

            try
            {
                dictionary2.AddOrUpdate("key", "value");
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.Null(exception);
        }

        [Fact]
        public async Task Concurrent_Uncomitted_write_works_on_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

            await dictionary.SaveChangesAsync();

            dictionary2.AddOrUpdate("key", "value2");

            Assert.Equal("value2", dictionary2.TryGet("key"));

            await dictionary2.SaveChangesAsync();

            Assert.Equal("value2", dictionary.TryGet("key"));
        }

        [Fact]
        public async Task SaveChanges_Concurrency()
        {
            Stopwatch stopwatch = new();

            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var logger = Scope2!.ServiceProvider.GetRequiredService<ILogger<IDictionaryContext<string>>>();

            Dictionary<string, string> dict = new();

            for (int i = 0; i < 10_000; i++)
            {
                dict.Add(i.ToString(), i.ToString());
            }

            stopwatch.Start();
            
            Parallel.ForEach(dict, (kv, _) => dictionary.AddOrUpdate(kv.Key, kv.Value));

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.Elapsed}");

            await dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.Elapsed}");

            var count = dictionary2.Count();

            logger.LogInformation($"QueryAsync: {stopwatch.Elapsed}");

            Assert.Equal(10_000, count);
        }

        [Fact]
        public async Task SaveChanges_batch_Concurrency()
        {
            const int count = 1_000_000;

            Stopwatch stopwatch = new();

            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var logger = Scope2!.ServiceProvider.GetRequiredService<ILogger<IDictionaryContext<string>>>();

            Dictionary<string, string> dict = new();

            for (int i = 0; i < count; i++)
            {
                dict.Add(i.ToString(), i.ToString());
            }

            stopwatch.Start();

            dictionary.AddOrUpdateRange(dict.Select(kv => (kv.Key, kv.Value)).ToList());

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.Elapsed}");

            _ = dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.Elapsed}");

            var dictCount = dictionary2.Count();

            logger.LogInformation($"QueryAsync: {stopwatch.Elapsed}");

            Assert.Equal(count, dictCount);
        }

        [Fact]
        public async Task DeepClones()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<HeapAllocation>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<HeapAllocation>>();

            dictionary.AddOrUpdate("key", new HeapAllocation() {Value = "value"});

            await dictionary.SaveChangesAsync();

            var heapAllocation = dictionary2.TryGet("key");

            heapAllocation.Value = "value2";

            var heapAllocation2 = dictionary.TryGet("key");

            Assert.Equal("value", heapAllocation2.Value);

            dictionary2.AddOrUpdate("key", heapAllocation);

            await dictionary2.SaveChangesAsync();

            heapAllocation = dictionary2.TryGet("key");

            Assert.Equal("value2", heapAllocation.Value);
            Assert.Equal("value", heapAllocation2.Value);
        }

        [Fact]
        public async Task InterleavedTransactionsWithDependency()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            // Transaction 1 starts and updates a key
            dictionary.AddOrUpdate("key", "initial");
            await dictionary.SaveChangesAsync();

            // Transaction 2 starts, reads the key, and plans to update it based on the read value
            var valueT2Start = dictionary2.TryGet("key");
            Assert.Equal("initial", valueT2Start);

            // Transaction 1 updates the key again before Transaction 2 commits its change
            dictionary.AddOrUpdate("key", "update1");
            await dictionary.SaveChangesAsync();

            bool retryTransaction2 = false;
            try
            {
                dictionary2.AddOrUpdate("key", valueT2Start + "+update2");

                // Attempt to commit Transaction 2's changes
                await dictionary2.SaveChangesAsync();
            }
            catch
            {
                retryTransaction2 = true;
            }

            if (retryTransaction2)
            {
                // Re-read the latest state and retry the operation
                var latestValue = dictionary2.TryGet("key");
                dictionary2.AddOrUpdate("key", latestValue + "+update2");
                await dictionary2.SaveChangesAsync();
            }

            // Final verification as before
            var finalValue = dictionary.TryGet("key");
            Assert.Equal("update1+update2", finalValue); // Adjust the expected value based on actual system behavior
        }

        [Fact]
        public async Task SequentialTransactionsWithDependencyOnCommittedChanges()
        {
            var dictionary1 = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            // Transaction 1 updates 'key1' and commits the change
            dictionary1.AddOrUpdate("key1", "value1");
            await dictionary1.SaveChangesAsync(); // Ensure the change is committed

            // Transaction 2 begins after Transaction 1's commit, ensuring visibility of 'key1's update
            var key1Value = dictionary2.TryGet("key1");
            Assert.Equal("value1", key1Value); // 'key1' update by Transaction 1 should be visible to Transaction 2

            // Transaction 2 updates 'key2' based on 'key1's committed value, then updates 'key1'
            dictionary2.AddOrUpdate("key2", key1Value + "+updateFromT2");
            dictionary2.AddOrUpdate("key1", "value1UpdatedByT2");
            await dictionary2.SaveChangesAsync(); // Commit Transaction 2's changes

            // Verify the final state reflects the sequential updates from both transactions
            var finalKey1Value = dictionary1.TryGet("key1");
            var finalKey2Value = dictionary1.TryGet("key2");

            Assert.Equal("value1UpdatedByT2", finalKey1Value); // Reflects Transaction 2's update
            Assert.Equal("value1+updateFromT2", finalKey2Value); // Reflects Transaction 2's dependent update on 'key2'
        }

        [Fact]
        public async Task HighConcurrencyStressTestWithScopedTransactions()
        {
            const int numberOfTransactions = 100; // Number of concurrent transactions
            const int numberOfKeysPerTransaction = 10; // Keys operated on by each transaction
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            // Prepopulate the dictionary with initial values
            for (int i = 0; i < numberOfTransactions * numberOfKeysPerTransaction; i++)
            {
                rootDictionary.AddOrUpdate($"key{i}", $"initial{i}");
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
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

                    // Each transaction reads, modifies, and updates a set of keys
                    for (int k = 0; k < numberOfKeysPerTransaction; k++)
                    {
                        int keyIndex = (t * numberOfKeysPerTransaction + k) % (numberOfTransactions * numberOfKeysPerTransaction);
                        string key = $"key{keyIndex}";
                        string newValue = $"updatedByTransaction{t}";

                        // Simulate read-modify-write cycle within the transaction scope
                        //var currentValue = await localDictionary.TryGetAsync(key);
                        //var updatedValue = $"{currentValue}+{newValue}";
                        localDictionary.AddOrUpdate(key, newValue);
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
                var value = rootDictionary.TryGet(key);
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