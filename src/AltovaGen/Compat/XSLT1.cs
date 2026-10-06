// AltovaGen Compat — XSLT1
using AltovaGen.Abstractions;
using AltovaGen.Models;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free replacement for XSLT1.
/// Backed by the XSLT engine selected through <see cref="IEngineRouter"/>.
/// </summary>
internal class XSLT1 : IXSLT1
{
    private readonly IEngineRouter _router;
    private string? _inputXmlFileName;
    private string? _xslFileName;
    private string? _inputXmlFromText;
    private string? _xslFromText;
    private int _stackSize;
    private int _dotNetExtensions;
    private int _javaExtensions;
    private string _lastError = string.Empty;
    private readonly List<KeyValuePair<string, string>> _parameters = new();

    public XSLT1(IEngineRouter router) => _router = router;

    public string InputXMLFileName { set => _inputXmlFileName = value; }
    public string XSLFileName { set => _xslFileName = value; }
    public string InputXMLFromText { set => _inputXmlFromText = value; }
    public string XSLFromText { set => _xslFromText = value; }
    public int XSLStackSize { set => _stackSize = value; }
    public string LastErrorMessage => _lastError;

    // COM VARIANT_BOOL semantics: 0 = false, non-zero = true.
    public int DotNetExtensionsEnabled { set => _dotNetExtensions = value; }
    public int JavaExtensionsEnabled { set => _javaExtensions = value; }

    protected int DotNetExtensions => _dotNetExtensions;
    protected int JavaExtensions => _javaExtensions;

    public void AddExternalParameter(string bstrName, string bstrVal)
    {
        if (bstrName is null) throw new ArgumentNullException(nameof(bstrName));
        _parameters.RemoveAll(p => p.Key == bstrName);
        _parameters.Add(new KeyValuePair<string, string>(bstrName, bstrVal ?? string.Empty));
    }

    public void ClearExternalParameterList() => _parameters.Clear();

    public void Execute(string bstrOutputFileName)
    {
        // Original returns void; errors surface via LastErrorMessage (already set in Run).
        Run(bstrOutputFileName);
    }

    public string ExecuteAndGetResultAsString()
    {
        var result = Run(outputPath: null);
        return result.OutputText ?? string.Empty;
    }

    public async Task ExecuteAsync(string bstrOutputFileName, CancellationToken cancellationToken = default)
    {
        await RunAsync(bstrOutputFileName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExecuteAndGetResultAsStringAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(outputPath: null, cancellationToken).ConfigureAwait(false);
        return result.OutputText ?? string.Empty;
    }

    protected virtual double RequiredXsltVersion => 1.0;

    /// <summary>
    /// Detects Altova-generated stylesheets that declare or call the Altova XSLT
    /// extension namespace (<c>xmlns:altova="http://www.altova.com/xslt-extensions"</c>).
    /// Such stylesheets are routed to the Saxon engine, which registers the managed
    /// <c>altova:evaluate()</c> bridge; the BCL XSLT 1.0 processor cannot resolve
    /// Altova extension functions.
    /// </summary>
    private bool UsesAltovaExtensionNamespace()
    {
        const string altovaExtensionMarker = "altova.com/xslt-extensions";

        if (!string.IsNullOrEmpty(_xslFromText))
        {
            return _xslFromText.Contains(altovaExtensionMarker, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(_xslFileName) && File.Exists(_xslFileName))
        {
            try
            {
                return File.ReadAllText(_xslFileName)
                    .Contains(altovaExtensionMarker, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                // The engine will report a file-level error below if the file is unreadable.
            }
        }

        return false;
    }

    private async Task<bool> UsesAltovaExtensionNamespaceAsync(CancellationToken cancellationToken)
    {
        const string altovaExtensionMarker = "altova.com/xslt-extensions";

        if (!string.IsNullOrEmpty(_xslFromText))
        {
            return _xslFromText.Contains(altovaExtensionMarker, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(_xslFileName) && File.Exists(_xslFileName))
        {
            try
            {
                var text = await File.ReadAllTextAsync(_xslFileName, cancellationToken).ConfigureAwait(false);
                return text.Contains(altovaExtensionMarker, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                // The engine will report a file-level error below if the file is unreadable.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        return false;
    }

    protected virtual TransformRequest BuildRequest(string? outputPath) => new()
    {
        InputXmlPath = _inputXmlFileName,
        XslPath = _xslFileName,
        InputXmlText = string.IsNullOrEmpty(_inputXmlFileName) ? _inputXmlFromText : null,
        XslText = string.IsNullOrEmpty(_xslFileName) ? _xslFromText : null,
        OutputPath = outputPath,
        ExternalParameters = _parameters.Count == 0 ? null : _parameters.ToDictionary(p => p.Key, p => p.Value),
        StackSizeHint = _stackSize >= 16 * 1024 ? StackSizeHint.ExtraLarge
            : _stackSize >= 4 * 1024 ? StackSizeHint.Large
            : StackSizeHint.Default
    };

    private TransformResult Run(string? outputPath)
    {
        _lastError = string.Empty;
        var validationError = ValidateRequest();
        if (validationError is not null)
        {
            return new TransformResult { LastErrorMessage = validationError };
        }

        var engine = _router.SelectXsltEngine(RequiredXsltVersion, UsesAltovaExtensionNamespace());
        var request = BuildRequest(outputPath);
        var result = engine.Transform(request);
        return FinalizeResult(result, outputPath);
    }

    private async Task<TransformResult> RunAsync(string? outputPath, CancellationToken cancellationToken)
    {
        _lastError = string.Empty;
        var validationError = ValidateRequest();
        if (validationError is not null)
        {
            return new TransformResult { LastErrorMessage = validationError };
        }

        var usesAltova = await UsesAltovaExtensionNamespaceAsync(cancellationToken).ConfigureAwait(false);
        var engine = _router.SelectXsltEngine(RequiredXsltVersion, usesAltova);
        var request = BuildRequest(outputPath);
        var result = await engine.TransformAsync(request, cancellationToken).ConfigureAwait(false);
        return await FinalizeResultAsync(result, outputPath, cancellationToken).ConfigureAwait(false);
    }

    private string? ValidateRequest()
    {
        if (string.IsNullOrEmpty(_inputXmlFileName) && string.IsNullOrEmpty(_inputXmlFromText))
        {
            _lastError = "No input XML specified: set InputXMLFileName or InputXMLFromText.";
            return _lastError;
        }

        if (string.IsNullOrEmpty(_xslFileName) && string.IsNullOrEmpty(_xslFromText))
        {
            _lastError = "No stylesheet specified: set XSLFileName or XSLFromText.";
            return _lastError;
        }

        if (_dotNetExtensions != 0 || _javaExtensions != 0)
        {
            _lastError = "Extension functions are not supported: this implementation is 100% managed " +
                         "with no COM/script host. EnableScript (msxsl:script) is unsupported in .NET 8+ [SYSLIB0062].";
            return _lastError;
        }

        return null;
    }

    private TransformResult FinalizeResult(TransformResult result, string? outputPath)
    {
        if (!result.IsSuccess)
        {
            _lastError = result.LastErrorMessage ?? "Transformation failed.";
        }
        else
        {
            // If the engine wrote to a file, read it back so Execute behaves like the COM version
            // (which writes the file) and ExecuteAndGetResultAsString returns the text.
            if (!string.IsNullOrEmpty(outputPath) && result.OutputText is null && File.Exists(outputPath))
            {
                result = new TransformResult
                {
                    OutputText = File.ReadAllText(outputPath),
                    Output = File.ReadAllBytes(outputPath)
                };
            }
        }

        return result;
    }

    private async Task<TransformResult> FinalizeResultAsync(TransformResult result, string? outputPath, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            _lastError = result.LastErrorMessage ?? "Transformation failed.";
        }
        else
        {
            if (!string.IsNullOrEmpty(outputPath) && result.OutputText is null && File.Exists(outputPath))
            {
                result = new TransformResult
                {
                    OutputText = await File.ReadAllTextAsync(outputPath, cancellationToken).ConfigureAwait(false),
                    Output = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false)
                };
            }
        }

        return result;
    }
}