using System.Diagnostics;
using Collections.Isolated.Abstractions;
using Collections.Isolated.Context;
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

            Assert.Equal("value", await dictionary.TryGetAsync("key"));
        }

        [Fact]
        public async Task BasicAdd_Concurrent()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

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

            dictionary.AddOrUpdate("key", "value");

            await dictionary.SaveChangesAsync();

            dictionary2.AddOrUpdate("key", "value-modified");

            await dictionary2.SaveChangesAsync();

            var getAsync = await dictionary.TryGetAsync("key");

            Assert.Equal("value-modified", getAsync);
        }

        [Fact]
        public async Task BasicRemove_Concurrent_reopen_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

            await dictionary.SaveChangesAsync();

            dictionary2.Remove("key");

            await dictionary2.SaveChangesAsync();

            var getAsync = await dictionary.TryGetAsync("key");

            Assert.Equal(null, getAsync);
        }

        [Fact]
        public async Task Concurrent_Uncomitted_write_works_on_transaction()
        {
            var dictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            var dictionary2 = Scope2!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

            dictionary.AddOrUpdate("key", "value");

            await dictionary.SaveChangesAsync();

            dictionary2.AddOrUpdate("key", "value2");

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
                dictionary.AddOrUpdate(kv.Key, kv.Value);
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

            dictionary.AddOrUpdateRange(dict.Select(kv => (kv.Key, kv.Value)).ToList());

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

            dictionary.AddOrUpdate("key", heapAllocation);

            await dictionary.SaveChangesAsync();

            var heapAllocation2 = await dictionary2.TryGetAsync("key");

            heapAllocation.Value = "value2";

            await dictionary2.SaveChangesAsync();

            Assert.Equal("value2", heapAllocation.Value);
            Assert.Equal("value", heapAllocation2.Value);
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
            var key1Value = await dictionary2.TryGetAsync("key1");
            Assert.Equal("value1", key1Value); // 'key1' update by Transaction 1 should be visible to Transaction 2

            // Transaction 2 updates 'key2' based on 'key1's committed value, then updates 'key1'
            dictionary2.AddOrUpdate("key2", key1Value + "+updateFromT2");
            dictionary2.AddOrUpdate("key1", "value1UpdatedByT2");
            await dictionary2.SaveChangesAsync(); // Commit Transaction 2's changes

            // Verify the final state reflects the sequential updates from both transactions
            var finalKey1Value = await dictionary1.TryGetAsync("key1");
            var finalKey2Value = await dictionary1.TryGetAsync("key2");

            if (finalKey1Value.Equals("value1UpdatedByT2") is false)
            {
                Console.WriteLine();
            }

            Assert.Equal("value1UpdatedByT2", finalKey1Value); // Reflects Transaction 2's update
            Assert.Equal("value1+updateFromT2", finalKey2Value); // Reflects Transaction 2's dependent update on 'key2'
        }

        [Fact]
        public async Task HighConcurrencyStressTestWithScopedTransactions()
        {
            const int numberOfTransactions = 100; // Number of concurrent transactions
            const int numberOfKeysPerTransaction = 10; // Keys operated on by each transaction
            var rootDictionary = Scope!.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
            var logger = Scope!.ServiceProvider.GetRequiredService <ILogger<IDictionaryContext<string>>> ();

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
                var t1 = t;

                var transactionTask = Task.Run(async () => await Function(t1, numberOfKeysPerTransaction, numberOfTransactions));
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

            await rootDictionary.SaveChangesAsync();

            await Function(numberOfTransactions - 1, numberOfTransactions * numberOfKeysPerTransaction, 1);

            // Validate the final state of the dictionary
            for (int i = 0; i < numberOfTransactions * numberOfKeysPerTransaction; i++)
            {
                string key = $"key{i}";
                var value = await rootDictionary.TryGetAsync(key);
                // Ensure that the value reflects updates from one or more transactions.
                Assert.Contains($"updatedByTransaction{numberOfTransactions-1}", value); // Simplified check; adjust based on expected outcome
            }

            await rootDictionary.SaveChangesAsync();

            async Task Function(int t1, int nbKeyPerTransaction, int nbOfTransaction)
            {
                // Create a new scope for each transaction
                using var scope = CreateScope();
                var localDictionary = scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();

                // Each transaction reads, modifies, and updates a set of keys
                for (int k = 0; k < nbKeyPerTransaction; k++)
                {
                    int keyIndex = (t1 * nbKeyPerTransaction + k) % (nbOfTransaction * nbKeyPerTransaction);

                    logger.LogInformation((keyIndex / 100).ToString());

                    string key = $"key{keyIndex}";
                    string newValue = $"updatedByTransaction{t1}";

                    // Simulate read-modify-write cycle within the transaction scope
                    var currentValue = await localDictionary.TryGetAsync(key);
                    var updatedValue = $"{currentValue}+{newValue}";
                    localDictionary.AddOrUpdate(key, updatedValue);
                }

                await localDictionary.SaveChangesAsync();
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
                    setupDictionary.AddOrUpdate($"key{i}", $"value{i}");
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
                        transactionDictionary.AddOrUpdate($"key{keyIndex}", $"value{keyIndex}-modified");
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

        public class HeapAllocation
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}