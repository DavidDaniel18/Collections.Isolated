using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace IsolatedDictionaryTests;

public abstract class IntegrationTest : IAsyncLifetime
{
    private IntegrationWebApplicationFactory _factory;
    private readonly IntegrationWebApplicationFactory.TestOutput _testOutput;

    protected IServiceScope? Scope;
    protected IServiceScope? Scope2;

    protected IntegrationTest(ITestOutputHelper outputHelper)
    {
        _testOutput = new IntegrationWebApplicationFactory.TestOutput(outputHelper);
    }

    public Task InitializeAsync()
    {
        _factory = new IntegrationWebApplicationFactory(_testOutput);

        Scope = _factory.Services.CreateScope();

        Scope2 = _factory.Services.CreateScope();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Scope is not null)
        {
            Scope.Dispose();
        }

        _testOutput.Lock = true;

        return Task.CompletedTask;
    }

    protected IServiceScope CreateScope()
    {
        return _factory.Services.CreateScope();
    }
}