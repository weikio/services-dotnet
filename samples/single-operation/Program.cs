using Microsoft.Extensions.Logging;
using Weikio.Host.Services.Sdk;

var service = WeikioServiceBuilder.Create("hello-world", "1.0.0")
    .WithDescription("Hello World Test Service")
    .WithOperation("greet", message =>
    {
        var input = message.DataAsString();
        message.Logger.LogInformation("Processing incoming message with content {Data}", input);
        
        var output = $"Hello there {input}";
        
        message.Reply(output);
    })
    .Build();

Console.WriteLine("Service Starting, press ctrl+c to exit");

await service.StartAsync();
