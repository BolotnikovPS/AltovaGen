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

    /// <summary>
    /// Asynchronous transformation. The default implementation delegates to the
    /// synchronous <see cref="Transform"/>; engines that perform file I/O override it to
    /// use asynchronous file APIs and honour the <paramref name="cancellationToken"/>.
    /// </summary>
    Task<TransformResult> TransformAsync(TransformRequest request, CancellationToken cancellationToken = default);
}
