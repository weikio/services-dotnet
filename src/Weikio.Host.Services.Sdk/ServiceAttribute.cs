using System;
using System.Collections.Generic;

namespace Weikio.Host.Services.Sdk;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public sealed class ServiceAttribute(string name, string version, string description) : Attribute
{
    public string Name { get; } = name;
    public string Version { get; } = version;
    public string Description { get; } = description;

    public Dictionary<string, string> Metadata { get; set; } = new();
}