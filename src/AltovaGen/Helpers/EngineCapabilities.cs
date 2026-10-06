namespace AltovaGen.Helpers;

/// <summary>
/// Capabilities reported by an engine implementation.
/// Used by EngineRouter to choose the right implementation.
/// </summary>
public sealed record EngineCapabilities(
    double XsltVersion,
    double XQueryVersion,
    double XsdVersion,
    bool SupportsScriptExtensions = false,
    bool SupportsXBRL = false,
    bool SupportsJavaExtensions = false,
    bool SupportsDotNetExtensions = false,
    bool CanValidateXsd11 = false,
    IReadOnlyList<string>? SupportedFeatures = null)
{
    public static readonly EngineCapabilities Bcl = new(
        XsltVersion: 1.0,
        XQueryVersion: 0.0, // This preset describes the BCL *XSLT* engine; XQuery is a separate BclXQueryEngine
        XsdVersion: 1.0,
        SupportsScriptExtensions: false, // BCL: msxsl:script not supported in .NET 8+
        SupportsXBRL: false,
        SupportsJavaExtensions: false,
        SupportsDotNetExtensions: false,
        CanValidateXsd11: false,
        SupportedFeatures: new[] { "XSLT 1.0", "WF", "DTD", "XSD 1.0" });

    // The bundled SaxonCS-HE engine implements XSLT 2.0/3.0 only. XQuery 3.1 and
    // XSD 1.1 are NOT wired by default; advertise nothing they cannot deliver so the
    // router never silently falls back to a lesser engine for those workloads.
    public static readonly EngineCapabilities Saxon = new(
        XsltVersion: 3.0,
        XQueryVersion: 0.0,
        XsdVersion: 0.0,
        SupportsScriptExtensions: false,
        SupportsXBRL: false,
        SupportsJavaExtensions: false,
        SupportsDotNetExtensions: false, // fully managed; no CLR/script extension host
        CanValidateXsd11: false,
        SupportedFeatures: new[] { "XSLT 2.0", "XSLT 3.0", "Altova altova:evaluate extension bridge" });
}
