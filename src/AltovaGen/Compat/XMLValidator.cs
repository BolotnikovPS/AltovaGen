// AltovaGen Compat — XMLValidator
using AltovaGen.Abstractions;
using AltovaGen.Models;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free replacement for XMLValidator.
/// Backed by the validation engine selected through <see cref="IEngineRouter"/>
/// (default: BCL XSD 1.0/DTD).
/// </summary>
internal class XMLValidator : IXMLValidator
{
    private readonly IEngineRouter _router;
    private string? _inputXmlFileName;
    private string? _schemaFileName;
    private string? _dtdFileName;
    private string? _inputXmlFromText;
    private string? _schemaFromText;
    private string? _dtdFromText;
    private int _treatXbrlAsErrors;
    private string _lastError = string.Empty;
    private bool _lastValid;

    public XMLValidator(IEngineRouter router) => _router = router;

    public string InputXMLFileName { set => _inputXmlFileName = value; }
    public string SchemaFileName { set => _schemaFileName = value; }
    public string DTDFileName { set => _dtdFileName = value; }
    public string InputXMLFromText { set => _inputXmlFromText = value; }
    public string SchemaFromText { set => _schemaFromText = value; }
    public string DTDFromText { set => _dtdFromText = value; }
    public string LastErrorMessage => _lastError;
    public int TreatXBRLInconsistenciesAsErrors { set => _treatXbrlAsErrors = value; }

    public bool IsValid()
    {
        var result = Validate(true);
        _lastError = result.LastErrorMessage ?? string.Empty;
        _lastValid = result.IsValid;
        return _lastValid;
    }

    public bool IsWellFormed()
    {
        var result = Validate(false);
        _lastError = result.LastErrorMessage ?? string.Empty;
        _lastValid = result.IsWellFormed;
        return _lastValid;
    }

    public bool IsValidWithExternalSchemaOrDTD()
    {
        bool hasExtSchema = !string.IsNullOrEmpty(_schemaFileName) || !string.IsNullOrEmpty(_schemaFromText);
        bool hasExtDtd = !string.IsNullOrEmpty(_dtdFileName) || !string.IsNullOrEmpty(_dtdFromText);

        if (!hasExtSchema && !hasExtDtd)
        {
            _lastError = "No external schema or DTD specified: set SchemaFileName/SchemaFromText or DTDFileName/DTDFromText.";
            return false;
        }

        var result = IsValid();
        return _lastValid;
    }

    private ValidationResult Validate(bool withSchemaOrDtd)
    {
        if (string.IsNullOrEmpty(_inputXmlFileName) && string.IsNullOrEmpty(_inputXmlFromText))
        {
            _lastError = "No input XML specified: set InputXMLFileName or InputXMLFromText.";
            return new ValidationResult { IsWellFormed = false, LastErrorMessage = _lastError };
        }

        var engine = _router.SelectValidationEngine(1.0, requireXsd11: false);

        // Well-formedness checks ignore any configured schema/DTD by design.
        var request = new ValidationRequest
        {
            InputXmlPath = !string.IsNullOrEmpty(_inputXmlFileName) ? _inputXmlFileName : null,
            InputXmlText = !string.IsNullOrEmpty(_inputXmlFromText) ? _inputXmlFromText : null,
            SchemaPath = withSchemaOrDtd && !string.IsNullOrEmpty(_schemaFileName) ? _schemaFileName : null,
            DtdPath = withSchemaOrDtd && !string.IsNullOrEmpty(_dtdFileName) ? _dtdFileName : null,
            SchemaText = withSchemaOrDtd && !string.IsNullOrEmpty(_schemaFromText) ? _schemaFromText : null,
            DtdText = withSchemaOrDtd && !string.IsNullOrEmpty(_dtdFromText) ? _dtdFromText : null,
            TreatXBRLInconsistenciesAsErrors = _treatXbrlAsErrors != 0
        };

        return engine.Validate(request);
    }
}