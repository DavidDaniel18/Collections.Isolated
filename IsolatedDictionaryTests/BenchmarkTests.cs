using Collections.Isolated.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Collections.Isolated.Context;

namespace IsolatedDictionaryTests;

public sealed class BenchmarkTests : IntegrationTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public BenchmarkTests(ITestOutputHelper outputHelper, ITestOutputHelper testOutputHelper) : base(outputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void CompareAddPerformanceAsync()
    {
        var logger = Scope.ServiceProvider.GetRequiredService<ILogger<DictionaryContext<string>>>();
        var customDictionary = Scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
        var standardDictionary = new Dictionary<string, string>();
        var stopwatch = new Stopwatch();

        // Test custom dictionary add
        stopwatch.Start();
        customDictionary.AddOrUpdateAsync("key", "value");
        stopwatch.Stop();
        var customAddTime = stopwatch.ElapsedTicks;

        // Test standard dictionary add
        stopwatch.Restart();
        standardDictionary.Add("key", "value");
        stopwatch.Stop();
        var standardAddTime = stopwatch.ElapsedTicks;

        // Output the times to the test output
        logger.LogInformation($"Custom Dictionary AddOrUpdateAsync Time: {customAddTime} ticks");
        logger.LogInformation($"Standard Dictionary Add Time: {standardAddTime} ticks");

        // Optionally, assert based on performance criteria
        // Assert.True(customAddTime < someThreshold);
    }

    [Fact]
    public void CompareAddPerformanceAsync_concurrentdict()
    {
        var logger = Scope.ServiceProvider.GetRequiredService<ILogger<DictionaryContext<string>>>();
        var customDictionary = Scope.ServiceProvider.GetRequiredService<IDictionaryContext<string>>();
        var standardDictionary = new ConcurrentDictionary<string, string>();
        var stopwatch = new Stopwatch();

        // Test custom dictionary add
        stopwatch.Start();
        customDictionary.AddOrUpdateAsync("key", "value");
        stopwatch.Stop();
        var customAddTime = stopwatch.ElapsedTicks;

        // Test standard dictionary add
        stopwatch.Restart();
        standardDictionary.TryAdd("key", "value");
        stopwatch.Stop();
        var standardAddTime = stopwatch.ElapsedTicks;

        // Output the times to the test output
        logger.LogInformation($"Custom Dictionary AddOrUpdateAsync Time: {customAddTime} ticks");
        logger.LogInformation($"Standard Dictionary Add Time: {standardAddTime} ticks");

        // Optionally, assert based on performance criteria
        // Assert.True(customAddTime < someThreshold);
    }

    [Fact]
    public async Task CompareGetPerformanceAsync()
    {
        var logger = Scope.ServiceProvider.GetRequiredService<ILogger<DictionaryContext<string>>>();
        var customDictionary = Scope.ServiceProvider.GetService<IDictionaryContext<string>>();
        var standardDictionary = new Dictionary<string, string> { { "key", "value" } };
        var stopwatch = new Stopwatch();

        // Prepare - ensure both dictionaries have the item to retrieve
        await customDictionary.AddOrUpdateAsync("key", "value");
        await customDictionary.SaveChangesAsync();

        // Test custom dictionary get
        stopwatch.Restart();
        await customDictionary.TryGetAsync("key");
        stopwatch.Stop();
        var customGetTime = stopwatch.ElapsedTicks;

        // Test standard dictionary get
        stopwatch.Restart();
        standardDictionary.TryGetValue("key", out var value);
        stopwatch.Stop();
        var standardGetTime = stopwatch.ElapsedTicks;

        // Output the times to the test output
        logger.LogInformation($"Custom Dictionary TryGetAsync Time: {customGetTime} ticks");
        logger.LogInformation($"Standard Dictionary TryGetValue Time: {standardGetTime} ticks");

        // Optionally, assert based on performance criteria
        // Assert.True(customGetTime < someThreshold);
    }

    [Fact]
    public async Task CompareGetPerformanceAsync_concurrentdict()
    {
        var logger = Scope.ServiceProvider.GetRequiredService<ILogger<DictionaryContext<string>>>();
        var customDictionary = Scope.ServiceProvider.GetService<IDictionaryContext<string>>();
        var standardDictionary = new ConcurrentDictionary<string, string>();
        var stopwatch = new Stopwatch();

        // Prepare - ensure both dictionaries have the item to retrieve
        await customDictionary.AddOrUpdateAsync("key", "value");
        await customDictionary.SaveChangesAsync();
        standardDictionary.TryAdd("key", "value");

        // Test custom dictionary get
        stopwatch.Restart();
        await customDictionary.TryGetAsync("key");
        stopwatch.Stop();
        var customGetTime = stopwatch.ElapsedTicks;

        // Test standard dictionary get
        stopwatch.Restart();
        standardDictionary.TryGetValue("key", out var value);
        stopwatch.Stop();
        var standardGetTime = stopwatch.ElapsedTicks;

        // Output the times to the test output
        logger.LogInformation($"Custom Dictionary TryGetAsync Time: {customGetTime} ticks");
        logger.LogInformation($"Standard Dictionary TryGetValue Time: {standardGetTime} ticks");

        // Optionally, assert based on performance criteria
        // Assert.True(customGetTime < someThreshold);
    }
}