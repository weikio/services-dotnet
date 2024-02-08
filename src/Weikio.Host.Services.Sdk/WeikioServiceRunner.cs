using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using Serilog;

namespace Weikio.Host.Services.Sdk;

public class WeikioServiceRunner
{
    public static async Task StartAsync(params WeikioService[] weikioServices)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var args = Environment.GetCommandLineArgs();
        
        var hostBuilder = new HostBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddJsonFile("appsettings.json", true);
                builder.AddEnvironmentVariables();
                builder.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(LogLevel.Trace);
                });
                
                services.AddSingleton<ConnectionFactory>();
                services.AddSingleton<NatsConnectionFactory>();

                foreach (var weikioService in weikioServices)
                {
                    if (weikioService.ServiceType != null)
                    {
                        services.AddTransient(weikioService.ServiceType);
                    }

                    services.AddTransient<IHostedService>(provider =>
                    {
                        var logger = provider.GetRequiredService<ILogger<WeikioHostedServiceRunner>>();

                        var result = new WeikioHostedServiceRunner(logger, weikioService, provider);

                        return result;
                    });

                    if (weikioService.ConfigureServices != null)
                    {
                        weikioService.ConfigureServices.Invoke(services);
                    }
                }
            })
            .UseSerilog();

        foreach (var weikioService in weikioServices)
        {
            if (weikioService.ConfigureBuilder != null)
            {
                weikioService.ConfigureBuilder.Invoke(hostBuilder);
            }
        }

        await hostBuilder.RunConsoleAsync();
    }
}