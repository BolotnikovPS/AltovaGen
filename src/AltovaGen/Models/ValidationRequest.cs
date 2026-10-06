namespace AltovaGen.Models;

/// <summary>
/// Validation request object.
/// </summary>
public sealed class ValidationRequest
{
    public string? InputXmlPath { get; init; }
    public string? SchemaPath { get; init; }
    public string? DtdPath { get; init; }
    public string? InputXmlText { get; init; }
    public string? SchemaText { get; init; }
    public string? DtdText { get; init; }
    public bool TreatXBRLInconsistenciesAsErrors { get; set; } = false;

    /// <summary>True when the XML input comes from a file (schema/DTD are optional — without
    /// them the request degrades to a well-formedness check, matching AltovaXML behavior).</summary>
    public bool IsFromFiles => !string.IsNullOrEmpty(InputXmlPath);

    /// <summary>True when the XML input comes from an in-memory string.</summary>
    public bool IsFromText => !string.IsNullOrEmpty(InputXmlText);

    /// <summary>True when an explicit schema or DTD was supplied (file or text).</summary>
    public bool HasSchemaOrDtd =>
        !string.IsNullOrEmpty(SchemaPath) || !string.IsNullOrEmpty(DtdPath) ||
        !string.IsNullOrEmpty(SchemaText) || !string.IsNullOrEmpty(DtdText);
}