using NATS.Client;
using Serilog.Core;
using Serilog.Events;

namespace Weikio.Host.Services.Sdk;

public class WeikioLogEventEnricher : ILogEventEnricher
{
    private readonly MsgHeader _header;

    public WeikioLogEventEnricher(MsgHeader header)
    {
        _header = header;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Weikio-Integration-Name",
            _header["Weikio-Integration-Name"]));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Weikio-Agent", _header["Weikio-Agent"]));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("Weikio-Environment", _header["Weikio-Environment"]));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Weikio-CorrId", _header["Weikio-CorrId"]));
    }
}