using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Weikio.Host.Services.Sdk;

public class WeikioServiceBuilder
{
    private string _description;
    private string _name;
    private string _version;
    private Dictionary<string, string> _metadata;
    private List<Endpoint> _endpoints = new();
    private Action<IHostBuilder> _configureBuilder;
    private Action<IServiceCollection> _configureServices;

    private WeikioServiceBuilder(string name, string version)
    {
        _name = name;
        _version = version;
    }

    public WeikioServiceBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public WeikioServiceBuilder WithEndpoint(string name, Action<EndpointMessage> handler,
        IDictionary<string, string> metadata = default)
    {
        var func = new Func<EndpointMessage, Task>(ev =>
        {
            handler(ev);

            return Task.CompletedTask;
        });

        WithEndpoint(name, func, metadata);

        return this;
    }

    public WeikioServiceBuilder WithEndpoint(string name, Func<EndpointMessage, Task> handler,
        IDictionary<string, string> metadata = default)
    {
        var endpoint = new Endpoint(name, handler, metadata);

        _endpoints.Add(endpoint);

        return this;
    }

    public WeikioServiceBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    public WeikioServiceBuilder Configure(Action<IHostBuilder> builder)
    {
        _configureBuilder = builder;

        return this;
    }

    public WeikioServiceBuilder Deps(Action<IServiceCollection> services)
    {
        _configureServices = services;

        return this;
    }

    public static WeikioServiceBuilder Create(string name, string version)
    {
        return new WeikioServiceBuilder(name, version);
    }

    public static WeikioServiceBuilder<T> Create<T>()
    {
        return new WeikioServiceBuilder<T>();
    }

    public WeikioService Build()
    {
        var weikioService = new WeikioService(_name, _version, _description, _metadata, null, _configureBuilder,
            _configureServices);

        foreach (var endpoint in _endpoints)
        {
            weikioService.AddEndpoint(endpoint);
        }

        return weikioService;
    }
}

public class WeikioServiceBuilder<T>
{
    private Action<IHostBuilder> _configureBuilder;
    private Action<IServiceCollection> _configureServices;

    public WeikioService Build()
    {
        var serviceType = typeof(T);
        var serviceAttribute = serviceType.GetCustomAttributes(typeof(ServiceAttribute), false)
            .Cast<ServiceAttribute>()
            .FirstOrDefault();

        if (serviceAttribute == null)
        {
            throw new ArgumentException($"Service must contain {nameof(ServiceAttribute)} attribute");
        }

        var name = serviceAttribute.Name;
        var version = serviceAttribute.Version;
        var description = serviceAttribute.Description;
        var metadata = serviceAttribute.Metadata;

        var weikioService = new WeikioService(name, version, description, metadata, serviceType, _configureBuilder,
            _configureServices);

        var methods = serviceType.GetMethods();

        foreach (var method in methods)
        {
            // Check if the method is decorated with the Endpoint attribute
            var endpointAttribute = method.GetCustomAttributes(typeof(EndpointAttribute), true)
                .Cast<EndpointAttribute>().FirstOrDefault();

            if (endpointAttribute == null)
            {
                continue;
            }

            var endpointName = endpointAttribute.Name;
            var endpointMetadata = endpointAttribute.Metadata;
            var endpointMaxDegreeOfParallelism = endpointAttribute.MaxDegreeOfParallelism;

            Endpoint EndpointFactory(IServiceProvider provider)
            {
                var runner = provider.GetRequiredService<T>();

                Func<EndpointMessage, Task> handler = null;

                if (typeof(Task).IsAssignableFrom(method.ReturnType))
                {
                    handler = async msg =>
                    {
                        var methodTask = (Task)method.Invoke(runner, [msg]);
                        if (methodTask != null)
                        {
                            await methodTask;
                        }
                    };
                }
                else
                {
                    if (method.ReturnType == typeof(void))
                    {
                        handler = msg =>
                        {
                            method.Invoke(runner, [msg]);

                            return Task.CompletedTask;
                        };
                    }
                    else
                    {
                        throw new Exception(
                            $"Not supported return type. Method {method.Name}, Return type: {method.ReturnType}, Type: {serviceType.Name}. Only Task and void are supported");
                    }
                }

                var endpoint = new Endpoint(endpointName, handler, endpointMetadata, endpointMaxDegreeOfParallelism);

                return endpoint;
            }

            weikioService.AddEndpoint(EndpointFactory);
        }

        return weikioService;
    }

    public WeikioServiceBuilder<T> Configure(Action<IHostBuilder> configureBuilder)
    {
        _configureBuilder = configureBuilder;

        return this;
    }

    public WeikioServiceBuilder<T> Deps(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices;

        return this;
    }
}
