using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Abstractions;

/// <summary>
/// Base engine interface for XSLT transformations.
/// </summary>
public interface IXsltEngine
{
    EngineCapabilities Capabilities { get; }
    TransformResult Transform(TransformRequest request);
}