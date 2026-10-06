using AltovaGen.Helpers;

namespace AltovaGen.Models;

/// <summary>
/// Result object from XSLT transformation.
/// </summary>
public sealed class TransformResult
{
    public byte[]? Output { get; init; }
    public string? OutputText { get; init; }
    public string? LastErrorMessage { get; init; }
    public Diagnostic[] Diagnostics { get; init; } = Array.Empty<Diagnostic>();
    public bool IsSuccess => string.IsNullOrEmpty(LastErrorMessage);
    public bool OutputToStream => Output != null;
}