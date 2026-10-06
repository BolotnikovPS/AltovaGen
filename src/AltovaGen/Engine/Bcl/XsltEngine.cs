using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using AltovaGen.Abstractions;
using AltovaGen.Helpers;
using AltovaGen.Models;

namespace AltovaGen.Engine.Bcl;

/// <summary>
/// XSLT 1.0 engine using .NET BCL XslCompiledTransform.
/// Output is produced through a TextWriter so the stylesheet's xsl:output method
/// (xml / html / text) is honoured.
/// Does NOT support msxsl:script blocks (not supported in .NET 8+, SYSLIB0062).
/// </summary>
internal sealed class BclXsltEngine : IXsltEngine
{
    public EngineCapabilities Capabilities => EngineCapabilities.Bcl;

    public TransformResult Transform(TransformRequest request)
    {
        try
        {
            if (request.IsFromFiles)
            {
                return TransformFiles(request);
            }

            if (request.IsFromText)
            {
                return TransformText(request);
            }

            return new TransformResult
            {
                LastErrorMessage = "Invalid request: must specify either file paths or text content for both XML and XSL."
            };
        }
        catch (Exception ex)
        {
            return new TransformResult { LastErrorMessage = ex.Message };
        }
    }

    private static XsltSettings CreateSettings() => new()
    {
        // msxsl:script is obsolete in .NET 8+ [SYSLIB0062] and unsupported on every
        // OS — script blocks are never enabled. EnableScript defaults to false, so it
        // is intentionally not referenced here (the member is removed in .NET 10).
        EnableDocumentFunction = true
    };

    private TransformResult TransformFiles(TransformRequest request)
    {
        var xslt = new XslCompiledTransform();
        xslt.Load(request.XslPath!, CreateSettings(), new XmlUrlResolver());

        var args = BuildArgList(request.ExternalParameters);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using (var reader = XmlReader.Create(request.InputXmlPath!,
                   new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
        {
            // TextWriter overload => xsl:output method of the stylesheet governs serialization.
            xslt.Transform(reader, args, output);
        }

        var resultText = output.ToString();

        if (!string.IsNullOrEmpty(request.OutputPath))
        {
            File.WriteAllText(request.OutputPath, resultText, new UTF8Encoding(false));
        }

        return new TransformResult
        {
            OutputText = resultText,
            Output = new UTF8Encoding(false).GetBytes(resultText)
        };
    }

    private TransformResult TransformText(TransformRequest request)
    {
        var xslt = new XslCompiledTransform();
        using (var xslReader = XmlReader.Create(new StringReader(request.XslText!),
                   XmlReaderSettingsHelper.CreateForStringText()))
        {
            xslt.Load(xslReader, CreateSettings(), new XmlUrlResolver());
        }

        var args = BuildArgList(request.ExternalParameters);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using (var reader = XmlReader.Create(new StringReader(request.InputXmlText!),
                   XmlReaderSettingsHelper.CreateForStringText()))
        {
            xslt.Transform(reader, args, output);
        }

        var resultText = output.ToString();

        if (!string.IsNullOrEmpty(request.OutputPath))
        {
            File.WriteAllText(request.OutputPath, resultText, new UTF8Encoding(false));
        }

        return new TransformResult
        {
            OutputText = resultText,
            Output = new UTF8Encoding(false).GetBytes(resultText)
        };
    }

    /// <summary>
    /// Maps Altova-style external parameters (name → lexical value) onto an XsltArgumentList.
    /// Numeric-looking values are passed as xs:double / xs:boolean so that numeric
    /// comparisons in XSLT 1.0 behave like they do under AltovaXML.
    /// </summary>
    private static XsltArgumentList? BuildArgList(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var args = new XsltArgumentList();
        foreach (var (name, value) in parameters)
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            object typed = value;
            if (bool.TryParse(value, out var b))
            {
                typed = b;
            }
            else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                typed = d;
            }

            args.AddParam(name, string.Empty, typed);
        }

        return args;
    }
}
