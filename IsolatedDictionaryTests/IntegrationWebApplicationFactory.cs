using Collections.Isolated.Registration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestApi;
using Xunit.Abstractions;
using static IsolatedDictionaryTests.SyncStoreTests;

namespace IsolatedDictionaryTests;

public sealed class IntegrationWebApplicationFactory(IntegrationWebApplicationFactory.TestOutput outputHelper) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .ConfigureAppConfiguration((_, config) =>
            {
                var appSettingsPath = "/app/appsettings.json";

                config.AddJsonFile(appSettingsPath, true, true);
            })
            .ConfigureServices((hostingContext, services) =>
            {
                services.AddSingleton(outputHelper);

                services.AddIsolatedDictionary(configurator =>
                {
                    configurator.AddStore<string>();
                    configurator.AddStore<HeapAllocation>();

                    configurator.TransactionTimeoutInMs = 1000;
                });
            })
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            });
    }

    public class Logger<T>(TestOutput testOutput) : ILogger<T>, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (testOutput.Lock) return;

            testOutput.OutputHelper?.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return testOutput.Lock is false;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public void Dispose()
        {
            testOutput.Lock = true;
            testOutput.OutputHelper = null;
        }
    }

    public class TestOutput(ITestOutputHelper outputHelper)
    {
        public ITestOutputHelper OutputHelper { get; set; } = outputHelper;

        internal bool Lock { get; set; }
    }
}