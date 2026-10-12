using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AltovaGen.Abstractions;
using AltovaGen.Helpers;
using AltovaGen.Models;
using Saxon.Api;

namespace AltovaGen.Engine.Saxon;

/// <summary>
/// XSLT 2.0 / 3.0 engine backed by SaxonCS-HE (Saxonica Home Edition), a fully managed,
/// cross-platform, COM-free processor.
///
/// This is the engine that makes Altova-generated stylesheets runnable:
/// <list type="bullet">
///   <item>XSLT 2.0/3.0 constructs — <c>xsl:for-each-group</c>, sequence arithmetic,
///     <c>xsl:function</c>, <c>xsl:result-document</c>, typed variables, <c>xsl:evaluate</c>.</item>
///   <item>XPath 2.0/3.1 expressions, including <c>sum(for $i in ... return ...)</c>.</item>
///   <item>The Altova extension namespace
///     <c>xmlns:altova="http://www.altova.com/xslt-extensions"</c> via
///     <see cref="AltovaEvaluateFunction"/> — no Altova runtime, no COM, no registry.</item>
/// </list>
///
/// Output is serialized by Saxon itself, so the stylesheet's <c>xsl:output</c> properties
/// (method, encoding, indentation, DOCTYPE, CDATA sections) are honoured byte for byte;
/// the produced bytes are what <c>Output</c> returns and what is written to
/// <see cref="TransformRequest.OutputPath"/>.
///
/// Notes:
/// <list type="bullet">
///   <item>A <see cref="Processor"/> is created per call because Saxon processors are not
///     thread-safe; concurrent transforms are therefore safe.</item>
///   <item><see cref="TransformRequest.StackSizeHint"/> is not used by this engine (the
///     managed Saxon processor manages its own stacks).</item>
///   <item>External parameters are supplied as <c>xs:string</c>, matching Altova's
///     string-based <c>AddExternalParameter</c>; cast explicitly in the stylesheet when a
///     typed value is required.</item>
/// </list>
/// </summary>
internal sealed class SaxonXsltEngine : IXsltEngine
{
    private static readonly Regex XmlDeclarationEncoding = new(
        """^<\?xml[^>]*?encoding\s*=\s*["'](?<enc>[^"']+)["']""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IExtensionFunctionRegistry _extensionFunctions;

    /// <summary>
    /// Creates the engine with the built-in extension-function registry.
    /// </summary>
    public SaxonXsltEngine()
        : this(new SaxonExtensionFunctionRegistry())
    {
    }

    /// <summary>
    /// Creates the engine with a custom extension-function registry. Used by the DI
    /// container to inject <see cref="DependencyInjection.AltovaGenOptions.ExtensionFunctions"/>.
    /// </summary>
    public SaxonXsltEngine(IExtensionFunctionRegistry extensionFunctions)
    {
        _extensionFunctions = extensionFunctions ?? throw new ArgumentNullException(nameof(extensionFunctions));
    }

    /// <summary>
    /// SaxonCS-HE mutates process-wide static state while constructing a
    /// <see cref="Processor"/> and registering extension functions. Under concurrent
    /// transforms that registration is intermittently lost (the function then fails to
    /// resolve at compile time with XPST0017), so processor construction and extension
    /// registration must be serialized. Compilation and transformation after that point
    /// are per-call and remain thread-safe.
    /// </summary>
    private static readonly object ProcessorGate = new();

    static SaxonXsltEngine()
    {
        // Altova output is frequently windows-1251; register the legacy code pages so the
        // declared encoding can be honoured when decoding the serialized bytes.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public EngineCapabilities Capabilities => EngineCapabilities.Saxon;

    public TransformResult Transform(TransformRequest request)
    {
        if (!request.IsFromFiles && !request.IsFromText)
        {
            return new TransformResult
            {
                LastErrorMessage = "Invalid request: must specify either file paths or text content for both XML and XSL."
            };
        }

        var diagnostics = new List<Diagnostic>();

        try
        {
            var bytes = TransformCore(request, diagnostics);

            var failure = FirstError(diagnostics);
            if (failure is not null)
            {
                return new TransformResult
                {
                    LastErrorMessage = failure.Value.ToString(),
                    Diagnostics = diagnostics.ToArray()
                };
            }

            if (!string.IsNullOrEmpty(request.OutputPath))
            {
                File.WriteAllBytes(request.OutputPath!, bytes);
            }

            return new TransformResult
            {
                Output = bytes,
                OutputText = Decode(bytes),
                Diagnostics = diagnostics.ToArray()
            };
        }
        catch (Exception ex)
        {
            return new TransformResult
            {
                LastErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                    ? "XSLT transformation failed."
                    : ex.Message,
                Diagnostics = diagnostics.ToArray()
            };
        }
    }

    public async Task<TransformResult> TransformAsync(TransformRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.IsFromFiles && !request.IsFromText)
        {
            return new TransformResult
            {
                LastErrorMessage = "Invalid request: must specify either file paths or text content for both XML and XSL."
            };
        }

        var diagnostics = new List<Diagnostic>();

        try
        {
            // The Saxon transform itself is CPU-bound and has no asynchronous equivalent;
            // the cancellation token is still observed before the work and before writing.
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = TransformCore(request, diagnostics);

            var failure = FirstError(diagnostics);
            if (failure is not null)
            {
                return new TransformResult
                {
                    LastErrorMessage = failure.Value.ToString(),
                    Diagnostics = diagnostics.ToArray()
                };
            }

            if (!string.IsNullOrEmpty(request.OutputPath))
            {
                await File.WriteAllBytesAsync(request.OutputPath!, bytes, cancellationToken).ConfigureAwait(false);
            }

            return new TransformResult
            {
                Output = bytes,
                OutputText = Decode(bytes),
                Diagnostics = diagnostics.ToArray()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TransformResult
            {
                LastErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                    ? "XSLT transformation failed."
                    : ex.Message,
                Diagnostics = diagnostics.ToArray()
            };
        }
    }

    /// <summary>
    /// Performs the Saxon transform and returns the serialized output bytes, collecting
    /// diagnostics into <paramref name="diagnostics"/>. Extracted so the synchronous and
    /// asynchronous entry points share the same (CPU-bound) transform body.
    /// </summary>
    private byte[] TransformCore(TransformRequest request, List<Diagnostic> diagnostics)
    {
        // false => not schema-aware (Home Edition). Construction and extension
        // registration are serialized: see ProcessorGate.
        Processor processor;
        lock (ProcessorGate)
        {
            processor = new Processor(false);
            foreach (var function in _extensionFunctions.Functions)
            {
                processor.RegisterExtensionFunction(function);
            }
        }

        var executable = Compile(processor, request, diagnostics);
        var transformer = executable.Load30();
        transformer.ErrorReporter = error => Collect(error, diagnostics);

        if (!string.IsNullOrEmpty(request.InitialTemplateMode))
        {
            transformer.InitialMode = CreateQName(request.InitialTemplateMode!);
        }

        var parameters = BuildParameters(request.ExternalParameters);
        if (parameters.Count > 0)
        {
            transformer.SetStylesheetParameters(parameters);
        }

        // Relative xsl:result-document URIs resolve against the primary output location.
        if (!string.IsNullOrEmpty(request.OutputPath))
        {
            transformer.BaseOutputURI = new Uri(Path.GetFullPath(request.OutputPath!)).AbsoluteUri;
        }

        var source = BuildSource(processor, request);

        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            var serializer = processor.NewSerializer(buffer);

            transformer.GlobalContextItem = source;
            if (!string.IsNullOrEmpty(request.InitialTemplateName))
            {
                transformer.CallTemplate(CreateQName(request.InitialTemplateName!), serializer);
            }
            else
            {
                transformer.ApplyTemplates(source, serializer);
            }

            bytes = buffer.ToArray();
        }

        return bytes;
    }

    private static XsltExecutable Compile(Processor processor, TransformRequest request, List<Diagnostic> diagnostics)
    {
        var compiler = processor.NewXsltCompiler();
        compiler.ErrorReporter = error => Collect(error, diagnostics);

        if (!string.IsNullOrEmpty(request.XslPath))
        {
            // Compiling from a URI keeps the stylesheet base URI, so xsl:import/xsl:include
            // and doc() references resolve relative to the stylesheet.
            return compiler.Compile(new Uri(Path.GetFullPath(request.XslPath!)));
        }

        using var reader = new StringReader(request.XslText!);
        return compiler.Compile(reader);
    }

    private static XdmNode BuildSource(Processor processor, TransformRequest request)
    {
        var builder = processor.NewDocumentBuilder();

        if (!string.IsNullOrEmpty(request.InputXmlPath))
        {
            return builder.Build(new Uri(Path.GetFullPath(request.InputXmlPath!)));
        }

        // Text input is already decoded: the XML declaration encoding is ignored, matching
        // Altova's Unicode string handling.
        using var reader = new StringReader(request.InputXmlText!);
        return builder.Build(reader);
    }

    /// <summary>
    /// Maps Altova-style external parameters (name to lexical value) onto stylesheet
    /// parameters. Values stay strings, exactly like AltovaXML's AddExternalParameter.
    /// </summary>
    private static Dictionary<QName, XdmValue> BuildParameters(IReadOnlyDictionary<string, string>? parameters)
    {
        var result = new Dictionary<QName, XdmValue>();
        if (parameters is null)
        {
            return result;
        }

        foreach (var (name, value) in parameters)
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result[new QName(name)] = new XdmAtomicValue(value ?? string.Empty);
        }

        return result;
    }

    private static QName CreateQName(string name)
    {
        var trimmed = name.Trim();

        // Clark notation {uri}local is accepted as-is; otherwise the name is treated as an
        // unprefixed QName (Saxon throws a descriptive error for unknown prefixes).
        return trimmed.StartsWith('{') ? QName.FromClarkName(trimmed) : new QName(trimmed);
    }

    private static Diagnostic ToDiagnostic(Error error) => new(
        Message: error.Message ?? "Unknown Saxon error.",
        Engine: "Saxon",
        Code: error.ErrorCode?.ToString() ?? string.Empty,
        IsWarning: error.IsWarning);

    private static void Collect(Error error, List<Diagnostic> diagnostics) =>
        diagnostics.Add(ToDiagnostic(error));

    private static Diagnostic? FirstError(List<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            // Warnings are reported but do not fail the transform; classification comes
            // from Saxon's Error.IsWarning, not from matching the word "warning" in the text.
            if (diagnostic.IsError)
            {
                return diagnostic;
            }
        }

        return null;
    }

    /// <summary>
    /// Decodes serialized output back to a string for the COM-compatible
    /// ExecuteAndGetResultAsString() surface, honouring BOMs and the encoding declared by
    /// the serializer.
    /// </summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 256));
        var match = XmlDeclarationEncoding.Match(head);
        if (match.Success)
        {
            try
            {
                return Encoding.GetEncoding(match.Groups["enc"].Value).GetString(bytes);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                // Unknown encoding name: fall back to UTF-8 below.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
