using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.Service;
using Serilog;
using Serilog.Context;
using Serilog.Extensions.Logging;

namespace Weikio.Host.Services.Sdk;

public class WeikioHostedServiceRunner : IHostedService
{
    private readonly ILogger<WeikioHostedServiceRunner> _logger;
    private readonly WeikioService _service;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;

    private List<(ServiceEndpoint Endpoint, ActionBlock<ServiceMsgHandlerEventArgs> Action)> _endpointsAndActions =
        new();

    private readonly Service _natsService;

    internal WeikioHostedServiceRunner(ILogger<WeikioHostedServiceRunner> logger, WeikioService service,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _service = service;
        _serviceProvider = serviceProvider;

        _connection = serviceProvider.GetRequiredService<NatsConnectionFactory>().Create();
        var serviceGroup = new Group($"external.{service.Name}");

        foreach (var endpointFactory in service.Endpoints)
        {
            var endpoint = endpointFactory.Invoke(_serviceProvider);

            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Sink(new WeikioLogSink(serviceProvider))
                .CreateLogger();

            var endpointLogger = new SerilogLoggerFactory(serilogLogger)
                .CreateLogger<WeikioHostedServiceRunner>();

            var action = new ActionBlock<ServiceMsgHandlerEventArgs>(async args =>
                {
                    try
                    {
                        _logger.LogDebug(
                            "Endpoint {Endpoint} from Service {Service} is handling request from Subject {Subject}",
                            endpoint.Name, service.Name, args.Message.Subject);

                        var originalMsg = args.Message;
                        var wrappedMsg = new EndpointMessage(_connection, originalMsg, endpointLogger);

                        using (LogContext.Push(new WeikioLogEventEnricher(originalMsg.Header)))
                        {
                            await endpoint.Handler.Invoke(wrappedMsg);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to handle event");
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = endpoint.MaxDegreeOfParallelism
                });

            var serviceEndpoint = ServiceEndpoint.Builder()
                .WithGroup(serviceGroup)
                .WithEndpointName(endpoint.Name)
                .WithEndpointMetadata(endpoint.Metadata ?? new Dictionary<string, string>())
                .WithHandler((s, a) => { action.Post(a); })
                .Build();

            _endpointsAndActions.Add((serviceEndpoint, action));
        }

        var natsServiceBuilder = Service.Builder()
            .WithDescription(service.Description)
            .WithConnection(_connection)
            .WithName(service.Name)
            .WithVersion(service.Version)
            .WithMetadata(service.Metadata);

        foreach (var endpointAndAction in _endpointsAndActions)
        {
            var endpoint = endpointAndAction.Endpoint;
            natsServiceBuilder = natsServiceBuilder.AddServiceEndpoint(endpoint);
        }

        _natsService = natsServiceBuilder.Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Service {ServiceName} with Endpoint Count: {EndpointCount}", _natsService.Name,
            _endpointsAndActions.Count);
        _natsService.StartService();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service {ServiceName}", _natsService.Name);

        _natsService.Stop();

        foreach (var endpointsAndAction in _endpointsAndActions)
        {
            endpointsAndAction.Action.Complete();
            await endpointsAndAction.Action.Completion;
        }

        _connection.Dispose();
    }
}
