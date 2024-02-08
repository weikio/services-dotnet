using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;

namespace Weikio.Host.Services.Sdk;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class EndpointAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    public Dictionary<string, string> Metadata { get; set; } = new();

    public int MaxDegreeOfParallelism { get; set; } = DataflowBlockOptions.Unbounded;
}