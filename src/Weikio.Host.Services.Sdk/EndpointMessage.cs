using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.Service;
using Newtonsoft.Json;

namespace Weikio.Host.Services.Sdk;

public class EndpointMessage(IConnection connection, ServiceMsg msg, ILogger logger)
{
    public ILogger Logger { get; } = logger;

    public void Reply(byte[] response)
    {
        msg.Respond(connection, response, new MsgHeader()
        {
            { "Content-type", "application/octet-stream" }
        });
    }

    public void Reply(string response)
    {
        msg.Respond(connection, response, new MsgHeader()
        {
            { "Content-type", "text/plain" }
        });
    }

    public void Reply(object response)
    {
        var json = JsonConvert.SerializeObject(response, Formatting.Indented);
        msg.Respond(connection, json, new MsgHeader()
        {
            { "Content-type", "application/json" }
        });
    }

    public void ReplyError(string errorText, int errorCode)
    {
        msg.RespondStandardError(connection, errorText, errorCode);
    }

    public void ReplyError(string errorText)
    {
        msg.RespondStandardError(connection, errorText, 400);
    }

    public byte[] Data => msg.Data;
}