using System.Collections.Concurrent;
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

            await dictionary.AddOrUpdateAsync("key", "value");

            Assert.Equal("value", await dictionary.TryGetAsync("key"));
        }

        [Fact]
        public async Task BasicAdd_Concurrent()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            Task.Run(() =>
            {
                Task.Delay(10).Wait();

                dictionary.SaveChangesAsync();
            });

            var getAsync = await dictionary2.TryGetAsync("key");

            Assert.Equal(null, getAsync);
        }

        [Fact]
        public async Task BasicAdd_Concurrent_reopen_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            await dictionary.SaveChangesAsync();

            await dictionary2.AddOrUpdateAsync("key", "value-modified");

            await dictionary2.SaveChangesAsync();

            var getAsync = await dictionary.TryGetAsync("key");

            Assert.Equal("value-modified", getAsync);
        }

        [Fact]
        public async Task BasicRemove_Concurrent_reopen_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            await dictionary.AddOrUpdateAsync("key", "value");

            await dictionary.SaveChangesAsync();

            await dictionary2.RemoveAsync("key");

            await dictionary2.SaveChangesAsync();

            var getAsync = await dictionary.TryGetAsync("key");

            Assert.Equal(null, getAsync);
        }

        [Fact]
        public async Task Concurrent_Uncomitted_write_works_on_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

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

            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var logger = Scope2!.ServiceProvider.GetRequiredService<ILogger<IDictionaryContext<string>>>();

            Dictionary<string, string> dict = new();

            for (int i = 0; i < 1_000; i++)
            {
                dict.Add(i.ToString(), i.ToString());
            }

            stopwatch.Start();

            foreach (var kv in dict)
            {
                await dictionary.AddOrUpdateAsync(kv.Key, kv.Value);
            }

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.ElapsedTicks}");

            stopwatch.Restart();

            await dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.ElapsedTicks}");

            stopwatch.Restart();

            var count = await dictionary2.CountAsync();

            logger.LogInformation($"QueryAsync: {stopwatch.ElapsedTicks}");

            Assert.Equal(1_000, count);
        }

        [Fact]
        public async Task SaveChanges_batch_Concurrency()
        {
            const int count = 1_000;

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

            await dictionary.AddOrUpdateRangeAsync(dict.Select(kv => (kv.Key, kv.Value)).ToList());

            logger.LogInformation($"AddOrUpdateAsync: {stopwatch.Elapsed}");

            await dictionary.SaveChangesAsync();

            logger.LogInformation($"SaveChangesAsync: {stopwatch.Elapsed}");

            var dictCount = await dictionary2.CountAsync();

            logger.LogInformation($"QueryAsync: {stopwatch.Elapsed}");

            Assert.Equal(count, dictCount);
        }

        [Fact]
        public async Task DeepClones()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<HeapAllocation>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<HeapAllocation>>();

            var heapAllocation = new HeapAllocation() { Value = "value"};

            await dictionary.AddOrUpdateAsync("key", heapAllocation);

            await dictionary.SaveChangesAsync();

            var heapAllocation2 = await dictionary2.TryGetAsync("key");

            heapAllocation.Value = "value2";

            await dictionary2.SaveChangesAsync();

            Assert.Equal("value2", heapAllocation.Value);
            Assert.Equal("value", heapAllocation2.Value);
        }

        //[Fact]
        //public async Task EventualConsistencyWithPredictedStateValidation()
        //{
        //    const int numberOfTransactions = 100;
        //    const int numberOfKeys = 100;
        //    var expectedState = new ConcurrentDictionary<string, string>();
        //    var tasks = new List<Task>();
        //    var random = new Random();

        //    // Initial setup: Populate both the expected state and the IDictionaryContext
        //    var setupScope = CreateScope();
        //    var setupDictionary = setupScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
        //    for (int keyIndex = 0; keyIndex < numberOfKeys; keyIndex++)
        //    {
        //        var initialValue = $"initialValue{keyIndex}";
        //        await setupDictionary.AddOrUpdateAsync($"key{keyIndex}", initialValue);
        //        expectedState[$"key{keyIndex}"] = initialValue;
        //    }
        //    await setupDictionary.SaveChangesAsync();

        //    // Define a lock object for updates to the expected state
        //    object lockObject = new object();

        //    // Apply updates
        //    for (int i = 0; i < numberOfTransactions; i++)
        //    {
        //        var transactionId = i; // Capture the loop variable
        //        tasks.Add(Task.Run(async () =>
        //        {
        //            await Task.Delay(random.Next(0, 10)); // Random delay to simulate unpredictability

        //            var keyIndex = random.Next(0, numberOfKeys);
        //            var key = $"key{keyIndex}";
        //            var newValue = $"value{transactionId}";

        //            var transactionScope = CreateScope();
        //            var dictionary = transactionScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
        //            await dictionary.AddOrUpdateAsync(key, newValue);
        //            await dictionary.SaveChangesAsync();

        //            // Update the expected state
        //            lock (lockObject)
        //            {
        //                expectedState[key] = newValue;
        //            }
        //        }));
        //    }

        //    // Wait for all transactions to complete
        //    await Task.WhenAll(tasks);

        //    // Validate the final state against the expected state
        //    var validationScope = CreateScope();
        //    var validationDictionary = validationScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
        //    foreach (var kvp in expectedState)
        //    {
        //        var observedValue = await validationDictionary.TryGetAsync(kvp.Key);
        //        Assert.Equal(kvp.Value, observedValue);
        //    }
        //}

        [Fact]
        public async Task SequentialTransactionsWithDependencyOnCommittedChanges()
        {
            var dictionary1 = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

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
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

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
                var t1 = t;
                var transactionTask = Task.Run(async () =>
                {
                    // Create a new scope for each transaction
                    using var scope = CreateScope();
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

                    // Each transaction reads, modifies, and updates a set of keys
                    for (int k = 0; k < numberOfKeysPerTransaction; k++)
                    {
                        int keyIndex = (t1 * numberOfKeysPerTransaction + k) % (numberOfTransactions * numberOfKeysPerTransaction);

                        if (keyIndex == 11)
                        {
                            Console.WriteLine();
                        }

                        string key = $"key{keyIndex}";
                        string newValue = $"updatedByTransaction{t1}";

                        // Simulate read-modify-write cycle within the transaction scope
                        var currentValue = await localDictionary.TryGetAsync(key);
                        var updatedValue = $"{currentValue}+{newValue}";
                        await localDictionary.AddOrUpdateAsync(key, updatedValue);
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

        [Fact]
        public async Task ConcurrentReadModifyWriteWithOverlappingTransactionsTest()
        {
            const int numberOfKeys = 100; // Total number of keys in the dictionary
            const int numberOfTransactions = 50; // Number of concurrent transactions
            const int keysPerTransaction = 20; // Number of keys each transaction will attempt to modify
            var random = new Random(); // For generating random overlaps among transactions

            // Initialize the dictionary with some values
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            for (int i = 0; i < numberOfKeys; i++)
            {
                await rootDictionary.AddOrUpdateAsync($"key{i}", $"value{i}");
            }
            await rootDictionary.SaveChangesAsync();

            // Define a task for each transaction
            var transactionTasks = new List<Task>();
            for (int t = 0; t < numberOfTransactions; t++)
            {
                var t1 = t;
                transactionTasks.Add(Task.Run(async () =>
                {
                    // Create a new scope for each transaction to simulate isolated workspaces
                    using var scope = CreateScope();
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

                    // Select a random subset of keys to modify to ensure overlapping transactions
                    var keysToModify = Enumerable.Range(0, numberOfKeys)
                                                  .OrderBy(_ => random.Next())
                                                  .Take(keysPerTransaction)
                                                  .ToList();

                    // Perform read-modify-write cycles on the selected keys
                    foreach (var keyIndex in keysToModify)
                    {
                        string key = $"key{keyIndex}";
                        var currentValue = await localDictionary.TryGetAsync(key);
                        if (currentValue != null)
                        {
                            var updatedValue = $"{currentValue}-modifiedByTransaction{t1}";
                            await localDictionary.AddOrUpdateAsync(key, updatedValue);
                        }
                    }

                    // Attempt to commit the transaction
                    await localDictionary.SaveChangesAsync();
                }));
            }

            // Wait for all transactions to complete
            await Task.WhenAll(transactionTasks);

            // Validation: Check for data integrity and consistency
            for (int i = 0; i < numberOfKeys; i++)
            {
                string key = $"key{i}";
                var value = await rootDictionary.TryGetAsync(key);
                // This check is simplified; in a real test, you'd want to ensure that
                // each value modification reflects the expected number of modifications
                // and that no data has been lost or corrupted.
                Assert.NotNull(value);
                Assert.True(value.Contains("modifiedByTransaction"), $"Value for {key} was not modified correctly.");
            }
        }

        [Fact]
        public async Task EnsureConcurrentTransactionsIntegrity()
        {
            const int numberOfKeys = 1000;
            const int numberOfTransactions = 100;
            var random = new Random();

            // Setup phase using DI
            using (var setupScope = CreateScope())
            {
                var setupDictionary = setupScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
                for (int i = 0; i < numberOfKeys; i++)
                {
                    await setupDictionary.AddOrUpdateAsync($"key{i}", $"value{i}");
                }
                await setupDictionary.SaveChangesAsync();
            }

            // Concurrent transactions
            var tasks = Enumerable.Range(0, numberOfTransactions).Select(_ => Task.Run(async () =>
            {
                using (var transactionScope = CreateScope())
                {
                    var transactionDictionary = transactionScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
                    for (int i = 0; i < numberOfKeys / 10; i++)
                    {
                        var keyIndex = random.Next(0, numberOfKeys);
                        await transactionDictionary.AddOrUpdateAsync($"key{keyIndex}", $"value{keyIndex}-modified");
                    }
                    await transactionDictionary.SaveChangesAsync();
                }
            })).ToList();

            await Task.WhenAll(tasks);

            // Validation
            using (var validationScope = CreateScope())
            {
                var validationDictionary = validationScope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
                var validationTasks = Enumerable.Range(0, numberOfKeys).Select(async keyIndex =>
                {
                    var value = await validationDictionary.TryGetAsync($"key{keyIndex}");
                    Assert.NotNull(value);
                    Assert.StartsWith($"value{keyIndex}", value);
                });

                await Task.WhenAll(validationTasks);
            }
        }

        [Fact]
        public async Task ConcurrentFIFOUpdatesTest()
        {
            const int numberOfKeys = 100; // Total number of keys
            const int numberOfTransactions = 50; // Number of concurrent transactions
            const int keysPerTransaction = 20; // Keys each transaction modifies
            var random = new Random(); // For generating random key subsets

            // Initialize the dictionary with initial values and timestamps
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            for (int i = 0; i < numberOfKeys; i++)
            {
                var initialValue = $"value{i};timestamp={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                await rootDictionary.AddOrUpdateAsync($"key{i}", initialValue);
            }
            await rootDictionary.SaveChangesAsync();

            // Concurrent transactions
            var transactionTasks = new List<Task>();
            for (int t = 0; t < numberOfTransactions; t++)
            {
                var transactionId = t;
                transactionTasks.Add(Task.Run(async () =>
                {
                    using var scope = CreateScope();
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
                    var keysToModify = Enumerable.Range(0, numberOfKeys)
                                                  .OrderBy(_ => random.Next())
                                                  .Take(keysPerTransaction)
                                                  .ToList();

                    foreach (var keyIndex in keysToModify)
                    {
                        string key = $"key{keyIndex}";
                        var currentValue = await localDictionary.TryGetAsync(key);
                        if (currentValue != null)
                        {
                            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            var updatedValue = $"{currentValue}-modifiedByTransaction{transactionId};timestamp={timestamp}";
                            await localDictionary.AddOrUpdateAsync(key, updatedValue);
                        }
                    }

                    await localDictionary.SaveChangesAsync();
                }));
            }

            await Task.WhenAll(transactionTasks);

            // Validate FIFO behavior
            for (int i = 0; i < numberOfKeys; i++)
            {
                string key = $"key{i}";
                var value = await rootDictionary.TryGetAsync(key);
                Assert.NotNull(value);

                // Extract and verify timestamps to ensure FIFO behavior
                var modifications = value.Split(new[] { "-modifiedByTransaction" }, StringSplitOptions.RemoveEmptyEntries)
                                         .Skip(1) // Skip the initial value
                                         .Select(mod => mod.Split(new[] { ";timestamp=" }, StringSplitOptions.RemoveEmptyEntries).Last())
                                         .Select(long.Parse)
                                         .ToList();

                Assert.True(modifications.SequenceEqual(modifications.OrderBy(x => x)), $"Updates to {key} did not occur in FIFO order.");
            }
        }


        [Fact]
        public async Task ConcurrentAddRemoveReadModifyTestWithOverlappingTransactions()
        {
            const int numberOfKeys = 100; // Total number of keys in the dictionary
            const int numberOfTransactions = 50; // Number of concurrent transactions
            const int keysPerTransaction = 20; // Number of keys each transaction will attempt to interact with
            var random = new Random(); // For generating random overlaps among transactions

            // Initialize the dictionary with some values
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            for (int i = 0; i < numberOfKeys; i++)
            {
                await rootDictionary.AddOrUpdateAsync($"key{i}", $"value{i}");
            }
            await rootDictionary.SaveChangesAsync();

            // Define a task for each transaction
            var transactionTasks = new List<Task>();
            for (int t = 0; t < numberOfTransactions; t++)
            {
                transactionTasks.Add(Task.Run(async () =>
                {
                    using var scope = CreateScope();
                    var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

                    // Select a random subset of keys for various operations
                    var keysToModify = Enumerable.Range(0, numberOfKeys)
                                                  .OrderBy(_ => random.Next())
                                                  .Take(keysPerTransaction)
                                                  .ToList();

                    // Randomly decide on an action: add, remove, or modify
                    foreach (var keyIndex in keysToModify)
                    {
                        string key = $"key{keyIndex}";
                        int action = random.Next(3); // Randomly choose between add(0), remove(1), or modify(2)
                        switch (action)
                        {
                            case 0: // Add or update
                                var addValue = $"addedOrUpdatedValue{t}_{keyIndex}";
                                await localDictionary.AddOrUpdateAsync(key, addValue);
                                break;
                            case 1: // Remove
                                await localDictionary.RemoveAsync(key);
                                break;
                            case 2: // Modify
                                var currentValue = await localDictionary.TryGetAsync(key);
                                if (currentValue != null)
                                {
                                    var updatedValue = $"{currentValue}-modifiedByTransaction{t}";
                                    await localDictionary.AddOrUpdateAsync(key, updatedValue);
                                }
                                break;
                        }
                    }

                    await localDictionary.SaveChangesAsync();
                }));
            }

            // Wait for all transactions to complete
            await Task.WhenAll(transactionTasks);

            // Validation phase: Check for data integrity and consistency
            // Note: This validation checks for the presence of modified values.
            // Additional checks may be needed to ensure removed keys are not present.
            for (int i = 0; i < numberOfKeys; i++)
            {
                string key = $"key{i}";
                var value = await rootDictionary.TryGetAsync(key);

                // Ensure that if a value exists, it has been modified or added correctly
                if (value != null)
                {
                    Assert.True(value.Contains("modifiedByTransaction") || value.Contains("addedOrUpdatedValue"),
                                $"Value for {key} was not correctly modified or added.");
                }
                // Note: Additional validation logic may be required to verify removals and other specific conditions.
            }
        }


        public class HeapAllocation
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}