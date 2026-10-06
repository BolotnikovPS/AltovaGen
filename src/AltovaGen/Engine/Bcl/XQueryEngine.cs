using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using AltovaGen.Abstractions;
using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Engine.Bcl;

/// <summary>
/// Minimal XQuery 1.0 engine using the .NET BCL XPath/XQuery subset.
/// The BCL does not ship a full XQuery processor. This implementation covers
/// the common case: a query that is an XPath expression evaluated against the
/// input document, returned as its string value (UTF-8 without BOM when written to
/// <see cref="XQueryRequest.OutputPath"/>). The serializer options (encoding, method,
/// indentation, omit-xml-declaration) are not applied on this route because the result
/// is a raw XPath value, not a serialized document.
/// </summary>
internal sealed class BclXQueryEngine : IXQueryEngine
{
    public EngineCapabilities Capabilities => new EngineCapabilities(
        XsltVersion: 0,
        XQueryVersion: 1.0,
        XsdVersion: 1.0,
        CanValidateXsd11: false,
        SupportedFeatures: new[] { "XPath-based XQuery 1.0 subset" });

    public XQueryResult Execute(XQueryRequest request)
    {
        try
        {
            if (!request.IsFromFiles && !request.IsFromText)
            {
                return new XQueryResult
                {
                    LastErrorMessage = "Invalid request: must specify query and input (files or text).",
                    Diagnostics = new[] { new Diagnostic("Invalid request", Code: "REQ002") }
                };
            }

            // Load input document
            XPathDocument doc;
            if (!string.IsNullOrEmpty(request.InputXmlPath))
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
                using var reader = XmlReader.Create(request.InputXmlPath!, settings);
                doc = new XPathDocument(reader);
            }
            else
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
                using var reader = XmlReader.Create(new StringReader(request.InputXmlText!), settings);
                doc = new XPathDocument(reader);
            }

            // Get query text (XPath)
            string query = request.XQueryPath ?? request.XQueryText!;
            var nav = doc.CreateNavigator();

            // Apply external variables (simple string substitution is NOT done here —
            // variables require a real XQuery engine; we report them unsupported on BCL route)
            if (request.ExternalVariables is { Count: > 0 } || request.ExternalVariablesAsXPath is { Count: > 0 })
            {
                return new XQueryResult
                {
                    LastErrorMessage = "External variables require the Saxon engine (XQuery 3.1). The BCL XPath route does not support variables.",
                    Diagnostics = new[] { new Diagnostic("External variables unsupported on BCL route", Code: "XQ-VAR-001") }
                };
            }

            object result = nav.Evaluate(query) ?? string.Empty;
            string output = result switch
            {
                XPathNodeIterator it => CollectNodeSet(it),
                string s => s,
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => result.ToString() ?? string.Empty
            };

            if (string.IsNullOrEmpty(request.OutputPath))
            {
                return new XQueryResult
                {
                    OutputText = output,
                    Diagnostics = Array.Empty<Diagnostic>()
                };
            }

            System.IO.File.WriteAllText(request.OutputPath, output, new System.Text.UTF8Encoding(false));
            return new XQueryResult
            {
                OutputText = output,
                Diagnostics = Array.Empty<Diagnostic>()
            };
        }
        catch (XPathException ex)
        {
            return new XQueryResult
            {
                LastErrorMessage = $"XPath/XQuery error: {ex.Message}",
                Diagnostics = new[] { new Diagnostic(ex.Message, Code: "XQ001") }
            };
        }
        catch (Exception ex)
        {
            return new XQueryResult
            {
                LastErrorMessage = ex.Message,
                Diagnostics = new[] { new Diagnostic(ex.Message, Code: "XQ002") }
            };
        }
    }

    private static string CollectNodeSet(XPathNodeIterator it)
    {
        var sb = new System.Text.StringBuilder();
        while (it.MoveNext())
        {
            sb.Append(it.Current!.OuterXml);
        }
        return sb.ToString();
    }
}
