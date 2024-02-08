using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace Weikio.Host.Services.Sdk
{
    public class NatsConnectionFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NatsConnectionFactory> _logger;
        private readonly IConfiguration _configuration;

        public NatsConnectionFactory(IServiceProvider serviceProvider, ILogger<NatsConnectionFactory> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        public IConnection Create()
        {
            return Create(false);
        }

        public IConnection Create(int timeout)
        {
            return Create(false, timeout);
        }

        public IConnection Create(bool silent)
        {
            var timeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
            var timeoutFromConfiguration = _configuration["Weikio:Nats:Timeout"];

            if (timeoutFromConfiguration != null)
            {
                timeout = int.Parse(timeoutFromConfiguration);
            }

            return Create(silent, timeout);
        }

        public IConnection Create(bool silent, int timeout)
        {
            try
            {
                var config = _serviceProvider.GetRequiredService<IConfiguration>();
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Timeout = timeout;

                var logger = _serviceProvider.GetRequiredService<ILogger<NatsConnectionFactory>>();

                opts.ClosedEventHandler = (sender, args) =>
                {
                    logger.LogTrace("NATS Connection closed");
                };

                opts.ServerDiscoveredEventHandler = (sender, args) =>
                {
                    logger.LogTrace("NATS Server discovered");
                };

                opts.DisconnectedEventHandler = (sender, args) =>
                {
                    logger.LogTrace("NATS Connection disconnected");
                };

                opts.ReconnectedEventHandler = (sender, args) =>
                {
                    logger.LogTrace("NATS Connection reconnected");
                };

                opts.LameDuckModeEventHandler = (sender, args) =>
                {
                    logger.LogTrace("NATS Connection lame duck mode");
                };

                var url = config["Weikio:Nats:Url"];
                var username = config["Weikio:Nats:Username"];
                var password = config["Weikio:Nats:Password"];

                opts.Url = string.IsNullOrWhiteSpace(url) ? "nats://localhost:4222" : url;

                if (!string.IsNullOrWhiteSpace(username))
                {
                    opts.User = username;
                }

                if (!string.IsNullOrWhiteSpace(password))
                {
                    opts.Password = password;
                }

                var factory = _serviceProvider.GetRequiredService<ConnectionFactory>();
                var result = factory.CreateConnection(opts);

                return result;
            }
            catch (System.Exception e)
            {
                if (silent == false)
                {
                    _logger.LogError(e, "Failed to create NATS Connection");
                }

                throw;
            }
        }
    }
}
