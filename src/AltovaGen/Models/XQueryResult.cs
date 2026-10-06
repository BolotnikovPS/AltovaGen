using AltovaGen.Helpers;

namespace AltovaGen.Models;

/// <summary>
/// Result object from XQuery execution.
/// </summary>
public sealed class XQueryResult
{
    public byte[]? Output { get; init; }
    public string? OutputText { get; init; }
    public string? LastErrorMessage { get; init; }
    public Diagnostic[] Diagnostics { get; init; } = Array.Empty<Diagnostic>();
    public bool IsSuccess => string.IsNullOrEmpty(LastErrorMessage);
}
