using AltovaGen.Abstractions;
using AltovaGen.Engine.Saxon;

namespace AltovaGen.Engine.Bcl;

/// <summary>
/// Default engine router. Selects BCL engines for XSLT 1.0 and routes XSLT 2.0/3.0
/// requests (and requests that need extension functions, such as Altova-generated
/// stylesheets using <c>xmlns:altova="http://www.altova.com/xslt-extensions"</c>)
/// to the bundled, fully managed SaxonCS-HE engine.
/// </summary>
/// <remarks>
/// The engines are constructor-injected — there is no static state — so the instances
/// registered in the DI container are exactly the ones this router uses.
/// </remarks>
internal sealed class EngineRouter : IEngineRouter
{
    private readonly IXsltEngine _bclXslt;
    private readonly IXQueryEngine _bclXQuery;
    private readonly IValidationEngine _bclValidator;

    private IXsltEngine? _saxonXslt;
    private IXQueryEngine? _saxonXQuery;
    private IValidationEngine? _saxonValidation;

    /// <summary>
    /// DI constructor: every engine comes from the container. The bundled
    /// <see cref="SaxonXsltEngine"/> ships with this package (SaxonCS-HE, no license key),
    /// so XSLT 2.0/3.0 is available out of the box — no startup registration,
    /// no COM registration, no configuration.
    /// </summary>
    public EngineRouter(
        BclXsltEngine bclXslt,
        BclXQueryEngine bclXQuery,
        BclValidatorEngine bclValidator,
        SaxonXsltEngine saxonXslt)
    {
        ArgumentNullException.ThrowIfNull(bclXslt);
        ArgumentNullException.ThrowIfNull(bclXQuery);
        ArgumentNullException.ThrowIfNull(bclValidator);
        ArgumentNullException.ThrowIfNull(saxonXslt);

        _bclXslt = bclXslt;
        _bclXQuery = bclXQuery;
        _bclValidator = bclValidator;
        _saxonXslt = saxonXslt;
    }

    /// <summary>
    /// Convenience constructor for direct, non-DI usage: builds the default engine set.
    /// Functionally equivalent to what <c>AddAltovaGen()</c> registers in the container.
    /// </summary>
    public EngineRouter()
        : this(new BclXsltEngine(), new BclXQueryEngine(), new BclValidatorEngine(), new SaxonXsltEngine())
    {
    }

    /// <summary>
    /// Registers (or replaces) the Saxon engines. Only the XSLT engine is provided by
    /// default; XQuery 3.1 / XSD 1.1 implementations can be plugged in here, either
    /// directly on the router or through <c>AddAltovaGen(options =&gt; ...)</c>.
    /// </summary>
    public void RegisterSaxonEngines(IXsltEngine? xslt, IXQueryEngine? xquery, IValidationEngine? validation)
    {
        _saxonXslt = xslt;
        _saxonXQuery = xquery;
        _saxonValidation = validation;
    }

    public IXsltEngine SelectXsltEngine(double requiredVersion, bool requireExtensions = false)
    {
        if (requiredVersion > 1.0 || requireExtensions)
        {
            // Never fall back to the XSLT 1.0-only BCL processor for a 2.0/3.0 or
            // extension-function request: that would silently produce wrong results.
            return _saxonXslt
                   ?? throw new NotSupportedException(
                       $"XSLT {requiredVersion} / extension functions require the Saxon engine, which is not registered.");
        }
        return _bclXslt;
    }

    public IXQueryEngine SelectXQueryEngine()
    {
        // The BCL XPath route is the only XQuery impl by default; a Saxon XQuery engine
        // only becomes available after explicit RegisterSaxonEngines(..., xquery, ...).
        return _saxonXQuery ?? _bclXQuery;
    }

    public IValidationEngine SelectValidationEngine(double xsdVersion, bool requireXsd11 = false)
    {
        if (requireXsd11 || xsdVersion > 1.0)
        {
            // Never fall back to XSD 1.0-only BCL validation for an XSD 1.1 request.
            return _saxonValidation
                   ?? throw new NotSupportedException(
                       "XSD 1.1 validation is not available: no Saxon XSD 1.1 engine is registered.");
        }
        return _bclValidator;
    }
}
