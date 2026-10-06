namespace AltovaGen.Models;

/// <summary>
/// Request object for XSLT transformation operations.
/// </summary>
public sealed class TransformRequest
{
    public string? InputXmlPath { get; init; }
    public string? XslPath { get; init; }
    public string? InputXmlText { get; init; }
    public string? XslText { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalParameters { get; init; }
    public string? OutputPath { get; init; }
    public string? InitialTemplateName { get; init; }
    public string? InitialTemplateMode { get; init; }
    public StackSizeHint StackSizeHint { get; set; } = StackSizeHint.Default;

    public bool IsFromFiles => !string.IsNullOrEmpty(InputXmlPath) && !string.IsNullOrEmpty(XslPath);
    public bool IsFromText => !string.IsNullOrEmpty(InputXmlText) && !string.IsNullOrEmpty(XslText);
}
