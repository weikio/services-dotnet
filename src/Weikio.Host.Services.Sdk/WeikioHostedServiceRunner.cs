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

    private List<(ServiceEndpoint Operation, ActionBlock<ServiceMsgHandlerEventArgs> Action)> _operationsAndActions =
        new();

    private readonly Service _natsService;

    internal WeikioHostedServiceRunner(ILogger<WeikioHostedServiceRunner> logger, WeikioService service,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _service = service;
        _serviceProvider = serviceProvider;

        _connection = serviceProvider.GetRequiredService<NatsConnectionFactory>().Create(false, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, $"service-{service.Name}/{service.Version}");
        var serviceGroup = new Group($"external.{service.Name}");

        foreach (var operationFactory in service.Operations)
        {
            var operation = operationFactory.Invoke(_serviceProvider);

            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Sink(new WeikioLogSink(_connection))
                .CreateLogger();

            var operationLogger = new SerilogLoggerFactory(serilogLogger)
                .CreateLogger<WeikioHostedServiceRunner>();

            var action = new ActionBlock<ServiceMsgHandlerEventArgs>(async args =>
                {
                    try
                    {
                        _logger.LogDebug(
                            "Operation {Operation} from Service {Service} is handling request from Subject {Subject}",
                            operation.Name, service.Name, args.Message.Subject);

                        var originalMsg = args.Message;
                        var wrappedMsg = new OperationMessage(_connection, originalMsg, operationLogger);

                        using (LogContext.Push(new WeikioLogEventEnricher(originalMsg.Header)))
                        {
                            await operation.Handler.Invoke(wrappedMsg);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to handle event");
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = operation.MaxDegreeOfParallelism
                });

            var serviceEndpoint = ServiceEndpoint.Builder()
                .WithGroup(serviceGroup)
                .WithEndpointName(operation.Name)
                .WithEndpointMetadata(operation.Metadata ?? new Dictionary<string, string>())
                .WithHandler((s, a) => { action.Post(a); })
                .Build();

            _operationsAndActions.Add((serviceEndpoint, action));
        }

        var natsServiceBuilder = Service.Builder()
            .WithDescription(service.Description)
            .WithConnection(_connection)
            .WithName(service.Name)
            .WithVersion(service.Version)
            .WithMetadata(service.Metadata);

        foreach (var operationAndAction in _operationsAndActions)
        {
            var operation = operationAndAction.Operation;
            natsServiceBuilder = natsServiceBuilder.AddServiceEndpoint(operation);
        }

        _natsService = natsServiceBuilder.Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Service {ServiceName} with Operation Count: {OperationCount}", _natsService.Name,
            _operationsAndActions.Count);
        _natsService.StartService();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service {ServiceName}", _natsService.Name);

        _natsService.Stop();

        foreach (var operationAndAction in _operationsAndActions)
        {
            operationAndAction.Action.Complete();
            await operationAndAction.Action.Completion;
        }
        
        try
        {
            _connection.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}
