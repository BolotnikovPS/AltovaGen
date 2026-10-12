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

    /// <summary>
    /// Asynchronous validation. The default implementation delegates to the synchronous
    /// <see cref="Validate"/>; engines that perform file I/O override it to use asynchronous
    /// file APIs and honour the <paramref name="cancellationToken"/>.
    /// </summary>
    Task<ValidationResult> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken = default);
}
