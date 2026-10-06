using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using AltovaGen.Abstractions;
using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Engine.Bcl;

/// <summary>
/// XML Schema 1.0 / DTD / well-formedness validation engine using .NET BCL
/// <see cref="XmlReaderSettings"/>. Does not support XSD 1.1.
///
/// Validation problems are reported through the <c>ValidationEventHandler</c> callback
/// instead of throwing, so a schema-invalid or DTD-invalid document is distinguished from
/// a document that is not well-formed: <see cref="ValidationResult.IsWellFormed"/> stays
/// <c>true</c> for the former and <c>false</c> for the latter. Truly malformed XML still
/// surfaces as an <see cref="XmlException"/> while reading.
/// </summary>
internal sealed class BclValidatorEngine : IValidationEngine
{
    private static readonly Regex RootOpenTag = new(
        @"<(?!/|\?|!)(?<name>[A-Za-z_][\w.:-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public EngineCapabilities Capabilities => new EngineCapabilities(
        XsltVersion: 0, // No XSLT
        XQueryVersion: 0, // No XQuery
        XsdVersion: 1.0,
        CanValidateXsd11: false,
        SupportedFeatures: new[] { "XSD 1.0", "DTD", "WF validation" });

    public ValidationResult Validate(ValidationRequest request)
    {
        try
        {
            if (request.IsFromFiles)
            {
                return ValidateFromFile(request);
            }

            if (request.IsFromText)
            {
                return ValidateFromText(request);
            }

            return Failure("Invalid request: must specify either file paths or text content.", "REQ001", isWellFormed: false);
        }
        catch (XmlException ex)
        {
            return NotWellFormed(ex);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, "VALIDATION_ERROR", isWellFormed: false);
        }
    }

    private static ValidationResult ValidateFromFile(ValidationRequest request)
    {
        if (!string.IsNullOrEmpty(request.SchemaPath))
        {
            return ValidateSchema(inputPath: request.InputXmlPath!, inputText: null, schemaUri: request.SchemaPath!, schemaText: null);
        }

        if (!string.IsNullOrEmpty(request.DtdPath))
        {
            return ValidateDtd(ReadText(request.InputXmlPath!), ReadText(request.DtdPath!));
        }

        return CheckWellFormed(inputPath: request.InputXmlPath!, inputText: null);
    }

    private static ValidationResult ValidateFromText(ValidationRequest request)
    {
        if (!string.IsNullOrEmpty(request.SchemaText))
        {
            return ValidateSchema(inputPath: null, inputText: request.InputXmlText!, schemaUri: null, schemaText: request.SchemaText!);
        }

        if (!string.IsNullOrEmpty(request.DtdText))
        {
            return ValidateDtd(request.InputXmlText!, request.DtdText!);
        }

        return CheckWellFormed(inputPath: null, inputText: request.InputXmlText!);
    }

    private static ValidationResult ValidateSchema(string? inputPath, string? inputText, string? schemaUri, string? schemaText)
    {
        var schemaSet = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };

        if (!string.IsNullOrEmpty(schemaUri))
        {
            // The string-url overload resolves xsd:import/xsd:include relative to the
            // schema's own location and disposes its resources internally.
            schemaSet.Add(null, schemaUri);
        }
        else
        {
            using var schemaReader = XmlReader.Create(
                new StringReader(schemaText!),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            schemaSet.Add(null, schemaReader);
        }

        var settings = new XmlReaderSettings
        {
            Schemas = schemaSet,
            ValidationType = ValidationType.Schema,
            DtdProcessing = DtdProcessing.Prohibit
        };

        return RunValidation(inputPath, inputText, settings);
    }

    private static ValidationResult ValidateDtd(string inputXml, string dtdText)
    {
        var combined = CombineWithDoctype(inputXml, dtdText);

        // The DTD is supplied inline, so external entity resolution is refused:
        // this keeps the validation self-contained and avoids XXE through a hostile DTD.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.DTD,
            XmlResolver = null
        };

        return RunValidation(inputPath: null, inputText: combined, settings);
    }

    private static ValidationResult RunValidation(string? inputPath, string? inputText, XmlReaderSettings settings)
    {
        var errors = new List<Diagnostic>();
        settings.ValidationEventHandler += (_, e) =>
        {
            // XmlSchemaException carries LineNumber/LinePosition; warnings (schema
            // "could not find schema information") must not fail the validation.
            errors.Add(new Diagnostic(
                Message: e.Message,
                Line: e.Exception?.LineNumber ?? -1,
                Column: e.Exception?.LinePosition ?? -1,
                Engine: "BCL",
                Code: string.Empty,
                IsWarning: e.Severity == XmlSeverityType.Warning));
        };

        using (var reader = !string.IsNullOrEmpty(inputPath)
                   ? XmlReader.Create(inputPath!, settings)
                   : XmlReader.Create(new StringReader(inputText!), settings))
        {
            while (reader.Read()) { /* read through the whole document */ }
        }

        foreach (var error in errors)
        {
            if (!error.IsWarning)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsWellFormed = true,
                    LastErrorMessage = error.ToString(),
                    Diagnostics = errors.ToArray()
                };
            }
        }

        return new ValidationResult
        {
            IsValid = true,
            IsWellFormed = true,
            Diagnostics = errors.ToArray()
        };
    }

    private static ValidationResult CheckWellFormed(string? inputPath, string? inputText)
    {
        using var reader = !string.IsNullOrEmpty(inputPath)
            ? XmlReader.Create(inputPath!, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit })
            : XmlReader.Create(new StringReader(inputText!), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

        while (reader.Read()) { /* read through the whole document */ }

        return new ValidationResult
        {
            IsValid = true,
            IsWellFormed = true,
            Diagnostics = Array.Empty<Diagnostic>()
        };
    }

    /// <summary>
    /// Injects the supplied DTD as the internal subset of a synthetic DOCTYPE so the
    /// document can be validated against it even though it carries no DOCTYPE of its own.
    /// If the document already declares a DOCTYPE, the provided DTD is not injected and
    /// the document's own declarations are authoritative (a second DOCTYPE would be a
    /// well-formedness error). External entity references inside the DTD are not resolved
    /// (see <see cref="ValidateDtd"/>).
    /// </summary>
    private static string CombineWithDoctype(string xml, string dtd)
    {
        if (string.IsNullOrEmpty(xml) || xml.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
        {
            return xml;
        }

        var root = RootOpenTag.Match(xml);
        if (!root.Success)
        {
            return xml; // no root element — the well-formedness check below reports the error
        }

        return xml.Insert(root.Index, $"<!DOCTYPE {root.Groups["name"].Value} [\n{dtd}\n]>\n");
    }

    private static ValidationResult NotWellFormed(XmlException ex) => new()
    {
        IsValid = false,
        IsWellFormed = false,
        LastErrorMessage = $"XML parsing error: {ex.Message}",
        Diagnostics = new[] { new Diagnostic(ex.Message, ex.LineNumber, ex.LinePosition, "BCL") }
    };

    private static ValidationResult Failure(string message, string code, bool isWellFormed) => new()
    {
        IsValid = false,
        IsWellFormed = isWellFormed,
        LastErrorMessage = message,
        Diagnostics = new[] { new Diagnostic(message, Code: code) }
    };

    private static string ReadText(string path) => File.ReadAllText(path);
}
