using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Weikio.Host.Services.Sdk;

public class WeikioService(
    string name,
    string version,
    string description,
    Dictionary<string, string> metadata = null,
    Type serviceType = null,
    Action<IHostBuilder> configureBuilder = null,
    Action<IServiceCollection> configureServices = null)
{
    public string Name { get; } = name;
    public string Version { get; } = version;
    public string Description { get; } = description;
    public List<Func<IServiceProvider, Operation>> Operations { get; } = new();
    public Dictionary<string, string> Metadata { get; init; } = metadata ?? new();
    public Type ServiceType { get; } = serviceType;

    public Action<IHostBuilder> ConfigureBuilder { get; } = configureBuilder;
    public Action<IServiceCollection> ConfigureServices { get; } = configureServices;

    public async Task StartAsync()
    {
        await WeikioServiceRunner.StartAsync(this);
    }

    public void AddOperation(string name, Action<OperationMessage> handler,
        IDictionary<string, string> metadata = default, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
    {
        var func = new Func<OperationMessage, Task>(ev =>
        {
            handler(ev);

            return Task.CompletedTask;
        });

        AddOperation(name, func, metadata, maxDegreeOfParallelism);
    }

    public void AddOperation(string name, Func<OperationMessage, Task> handler,
        IDictionary<string, string> metadata = default, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
    {
        var operation = new Operation(name, handler, metadata, maxDegreeOfParallelism);

        AddOperation(operation);
    }

    public void AddOperation(Operation operation)
    {
        Operations.Add(_ => operation);
    }

    public void AddOperation(Func<IServiceProvider, Operation> operation)
    {
        Operations.Add(operation);
    }
}
