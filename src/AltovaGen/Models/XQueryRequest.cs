namespace AltovaGen.Models;

/// <summary>
/// Request object for XQuery operations.
/// Supports external variables with string values or XPath expressions.
/// </summary>
public sealed class XQueryRequest
{
    public string? XQueryPath { get; init; }
    public string? XQueryText { get; init; }
    public string? InputXmlPath { get; init; }
    public string? InputXmlText { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalVariables { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalVariablesAsXPath { get; init; }
    public string? OutputPath { get; init; }
    public string OutputEncoding { get; set; } = "UTF-8";
    public bool OutputIndent { get; set; } = true;
    public string OutputMethod { get; set; } = "xml";
    public bool OutputOmitXmlDeclaration { get; set; } = false;
    public bool DotNetExtensionsEnabled { get; set; } = false;
    public bool JavaExtensionsEnabled { get; set; } = false;

    public bool IsFromFiles => !string.IsNullOrEmpty(InputXmlPath) && !string.IsNullOrEmpty(XQueryPath);
    public bool IsFromText => !string.IsNullOrEmpty(InputXmlText) && !string.IsNullOrEmpty(XQueryText);
}
