using AltovaGen.Models;

namespace AltovaGen.Abstractions;

/// <summary>
/// Router that selects the appropriate engine based on request and capabilities.
/// </summary>
public interface IEngineRouter
{
    IXsltEngine SelectXsltEngine(double requiredVersion, bool requireExtensions = false);

    IXQueryEngine SelectXQueryEngine();

    IValidationEngine SelectValidationEngine(double xsdVersion, bool requireXsd11 = false);

    /// <summary>
    /// Selects an XSLT engine and transforms asynchronously. The default implementation
    /// selects via <see cref="SelectXsltEngine"/> and calls <see cref="IXsltEngine.TransformAsync"/>.
    /// </summary>
    Task<TransformResult> TransformAsync(double requiredVersion, TransformRequest request,
        bool requireExtensions = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects an XQuery engine and executes asynchronously. The default implementation
    /// selects via <see cref="SelectXQueryEngine"/> and calls <see cref="IXQueryEngine.ExecuteAsync"/>.
    /// </summary>
    Task<XQueryResult> ExecuteAsync(XQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects a validation engine and validates asynchronously. The default implementation
    /// selects via <see cref="SelectValidationEngine"/> and calls <see cref="IValidationEngine.ValidateAsync"/>.
    /// </summary>
    Task<ValidationResult> ValidateAsync(double xsdVersion, ValidationRequest request,
        bool requireXsd11 = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers (or replaces) the engines used for the XSLT 2.0/3.0 route and for the
    /// optional XQuery 3.1 / XSD 1.1 routes. Passing <see langword="null"/> for an engine
    /// removes it, which makes the corresponding route throw
    /// <see cref="NotSupportedException"/> instead of silently falling back to a weaker
    /// processor.
    /// </summary>
    /// <remarks>
    /// Call this during configuration (for example via
    /// <c>AddAltovaGen(options =&gt; ...)</c>), before the router is used concurrently.
    /// </remarks>
    void RegisterSaxonEngines(IXsltEngine? xslt, IXQueryEngine? xquery, IValidationEngine? validation);
}