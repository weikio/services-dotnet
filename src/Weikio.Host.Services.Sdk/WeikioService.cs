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
    public List<Func<IServiceProvider, Endpoint>> Endpoints { get; } = new();
    public Dictionary<string, string> Metadata { get; init; } = metadata ?? new();
    public Type ServiceType { get; } = serviceType;

    public Action<IHostBuilder> ConfigureBuilder { get; } = configureBuilder;
    public Action<IServiceCollection> ConfigureServices { get; } = configureServices;

    public async Task StartAsync()
    {
        await WeikioServiceRunner.StartAsync(this);
    }

    public void AddEndpoint(string name, Action<EndpointMessage> handler,
        IDictionary<string, string> metadata = default, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
    {
        var func = new Func<EndpointMessage, Task>(ev =>
        {
            handler(ev);

            return Task.CompletedTask;
        });

        AddEndpoint(name, func, metadata, maxDegreeOfParallelism);
    }

    public void AddEndpoint(string name, Func<EndpointMessage, Task> handler,
        IDictionary<string, string> metadata = default, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
    {
        var endpoint = new Endpoint(name, handler, metadata, maxDegreeOfParallelism);

        AddEndpoint(endpoint);
    }

    public void AddEndpoint(Endpoint endpoint)
    {
        Endpoints.Add(_ => endpoint);
    }

    public void AddEndpoint(Func<IServiceProvider, Endpoint> endpoint)
    {
        Endpoints.Add(endpoint);
    }
}