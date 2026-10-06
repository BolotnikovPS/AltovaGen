using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Abstractions;

/// <summary>
/// XQuery engine interface.
/// </summary>
public interface IXQueryEngine
{
    EngineCapabilities Capabilities { get; }
    XQueryResult Execute(XQueryRequest request);
}