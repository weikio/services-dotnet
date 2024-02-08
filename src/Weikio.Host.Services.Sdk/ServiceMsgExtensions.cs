using Newtonsoft.Json;

namespace Weikio.Host.Services.Sdk;

public static class ServiceMsgExtensions
{
    public static string DataAsString(this EndpointMessage msg)
    {
        var result = System.Text.Encoding.UTF8.GetString(msg.Data);

        return result ?? "";
    }

    public static T DataAsJson<T>(this EndpointMessage msg)
    {
        var str = msg.DataAsString();

        if (string.IsNullOrWhiteSpace(str))
        {
            return default;
        }

        var result = JsonConvert.DeserializeObject<T>(str);

        return result;
    }
}