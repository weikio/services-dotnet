using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Weikio.Host.Services.Sdk;

public class WeikioLogSink(IConnection connection) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var formatter = new CompactJsonFormatter();
        
        using (var sw = new StringWriter())
        {
            formatter.Format(logEvent, sw);
            
            var logMessage = sw.ToString();
            
            if (logEvent.Properties.ContainsKey("Weikio-Integration-Name") == false)
            {
                return;
            }
            
            var integrationName = GetPropertyValue("Weikio-Integration-Name");
            var agent = GetPropertyValue("Weikio-Agent");
            var environment = GetPropertyValue("Weikio-Environment");
            var correlationId = GetPropertyValue("Weikio-CorrId");
            
            if (string.IsNullOrWhiteSpace(integrationName) || string.IsNullOrWhiteSpace(agent) || string.IsNullOrWhiteSpace(environment) ||
                string.IsNullOrWhiteSpace(correlationId))
            {
                return;
            }
            
            var subject =
                $"il.{DateTime.Now:yyyyMMdd}.{integrationName}.{agent}.{environment}.{correlationId}.{logEvent.Level.ToString()}";
            
            try
            {
                connection.Publish(subject, System.Text.Encoding.UTF8.GetBytes(logMessage));
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "Failed to write Log Message to NATS");
            }
        }
        
        string GetPropertyValue(string propertyName)
        {
            if (logEvent.Properties[propertyName] is ScalarValue { Value: not null } val)
            {
                return val.Value.ToString();
            }
            
            return "";
        }
    }
}
