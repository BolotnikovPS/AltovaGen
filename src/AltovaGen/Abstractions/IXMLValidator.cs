// AltovaGen Compat — IXMLValidator

namespace AltovaGen.Abstractions;

/// <summary>
/// Reproduces Altova.AltovaXML.IXMLValidator (COM PIA v12.3).
/// </summary>
public interface IXMLValidator
{
    string InputXMLFileName { set; }
    string SchemaFileName { set; }
    string DTDFileName { set; }
    string InputXMLFromText { set; }
    string SchemaFromText { set; }
    string DTDFromText { set; }
    string LastErrorMessage { get; }
    int TreatXBRLInconsistenciesAsErrors { set; }

    bool IsValid();
    bool IsWellFormed();
    bool IsValidWithExternalSchemaOrDTD();

    /// <summary>
    /// Asynchronous variant of <see cref="IsValid"/> that uses async file I/O and
    /// observes <paramref name="cancellationToken"/>.
    /// </summary>
    Task<bool> IsValidAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronous variant of <see cref="IsWellFormed"/>.
    /// </summary>
    Task<bool> IsWellFormedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronous variant of <see cref="IsValidWithExternalSchemaOrDTD"/>.
    /// </summary>
    Task<bool> IsValidWithExternalSchemaOrDTDAsync(CancellationToken cancellationToken = default);
}