using AltovaGen.Helpers;

namespace AltovaGen.Models;

/// <summary>
/// Validation result object.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public bool IsWellFormed { get; init; }
    public string? LastErrorMessage { get; init; }
    public Diagnostic[] Diagnostics { get; init; } = Array.Empty<Diagnostic>();
    public bool IsSuccess => IsValid && string.IsNullOrEmpty(LastErrorMessage);
}
