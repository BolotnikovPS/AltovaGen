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

    /// <summary>
    /// Asynchronous execution. The default implementation delegates to the synchronous
    /// <see cref="Execute"/>; engines that perform file I/O override it to use asynchronous
    /// file APIs and honour the <paramref name="cancellationToken"/>.
    /// </summary>
    Task<XQueryResult> ExecuteAsync(XQueryRequest request, CancellationToken cancellationToken = default);
}
