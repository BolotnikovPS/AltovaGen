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