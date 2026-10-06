using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Abstractions;

/// <summary>
/// XML validation engine interface.
/// </summary>
public interface IValidationEngine
{
    EngineCapabilities Capabilities { get; }
    ValidationResult Validate(ValidationRequest request);
}