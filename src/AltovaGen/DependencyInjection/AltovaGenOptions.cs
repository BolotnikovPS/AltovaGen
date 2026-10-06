using AltovaGen.Abstractions;
using AltovaGen.Engine.Saxon;

namespace AltovaGen.DependencyInjection;

/// <summary>
/// Optional engine overrides for <c>AddAltovaGen(...)</c>.
/// </summary>
/// <remarks>
/// The bundled BCL engines and the fully managed <c>SaxonXsltEngine</c> are registered by
/// default. Use this to swap the Saxon XSLT engine for your own implementation or to plug
/// in XQuery 3.1 / XSD 1.1 engines, which are not bundled.
/// </remarks>
public sealed class AltovaGenOptions
{
    /// <summary>
    /// Replacement for the bundled XSLT 2.0/3.0 engine. When <see langword="null"/>,
    /// the container-registered <c>SaxonXsltEngine</c> is used.
    /// </summary>
    public IXsltEngine? SaxonXslt { get; set; }

    /// <summary>
    /// Optional XQuery 3.1 engine. When <see langword="null"/>, XQuery requests route to
    /// the BCL XPath-based implementation.
    /// </summary>
    public IXQueryEngine? SaxonXQuery { get; set; }

    /// <summary>
    /// Optional XSD 1.1 validation engine. When <see langword="null"/>, XSD 1.1
    /// validation requests throw <see cref="NotSupportedException"/>.
    /// </summary>
    public IValidationEngine? SaxonValidation { get; set; }

    /// <summary>
    /// Optional extension-function registry for the Saxon XSLT engine. When
    /// <see langword="null"/>, the bundled <see cref="SaxonExtensionFunctionRegistry"/>
    /// (the Altova <c>evaluate()</c> bridge) is used. Supply a custom registry to add
    /// project-specific extension functions without recompiling the package.
    /// </summary>
    public IExtensionFunctionRegistry? ExtensionFunctions { get; set; }
}
