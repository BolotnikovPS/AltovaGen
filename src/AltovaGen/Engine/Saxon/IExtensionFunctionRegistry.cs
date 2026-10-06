using Saxon.Api;

namespace AltovaGen.Engine.Saxon;

/// <summary>
/// Supplies the Saxon extension functions registered on every processor created by
/// <see cref="SaxonXsltEngine"/>.
/// </summary>
/// <remarks>
/// The bundled registry provides the Altova <c>evaluate()</c> bridge under every
/// namespace URI Altova tools use. Plug in your own implementation through
/// <see cref="DependencyInjection.AltovaGenOptions.ExtensionFunctions"/> to add custom
/// extension functions (for example, project-specific XBRL helpers) without recompiling
/// the package.
/// </remarks>
public interface IExtensionFunctionRegistry
{
    /// <summary>
    /// The extension functions to register on each Saxon processor, in registration order.
    /// </summary>
    IReadOnlyList<ExtensionFunctionDefinition> Functions { get; }
}
