using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Weikio.Host.Services.Sdk;

public record Endpoint(
    string Name,
    Func<EndpointMessage, Task> Handler,
    IDictionary<string, string> Metadata = default,
    int MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded);