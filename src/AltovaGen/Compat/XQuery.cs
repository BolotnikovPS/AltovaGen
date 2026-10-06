// AltovaGen Compat — XQuery
using AltovaGen.Abstractions;
using AltovaGen.Models;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free replacement for XQuery.
/// Backed by the XQuery engine selected through <see cref="IEngineRouter"/>
/// (default: the BCL XPath-based XQuery 1.0 subset).
/// </summary>
internal class XQuery : IXQuery
{
    private readonly IEngineRouter _router;
    private string? _xQueryFileName;
    private string? _inputXmlFileName;
    private string? _xQueryFromText;
    private string? _inputXmlFromText;
    private string _outputEncoding = "UTF-8";
    private bool _outputIndent = true;
    private string _outputMethod = "xml";
    private bool _outputOmitXmlDeclaration;
    private int _dotNetExtensions;
    private int _javaExtensions;
    private string _lastError = string.Empty;
    private readonly List<KeyValuePair<string, string>> _variables = new();
    private readonly List<KeyValuePair<string, string>> _variablesAsXPath = new();

    public XQuery(IEngineRouter router) => _router = router;

    public string XQueryFileName { set => _xQueryFileName = value; }
    public string InputXMLFileName { set => _inputXmlFileName = value; }
    public string XQueryFromText { set => _xQueryFromText = value; }
    public string InputXMLFromText { set => _inputXmlFromText = value; }
    public string OutputEncoding { get => _outputEncoding; set => _outputEncoding = value ?? "UTF-8"; }
    public bool OutputIndent { get => _outputIndent; set => _outputIndent = value; }
    public string OutputMethod { get => _outputMethod; set => _outputMethod = value ?? "xml"; }
    public bool OutputOmitXMLDeclaration { get => _outputOmitXmlDeclaration; set => _outputOmitXmlDeclaration = value; }
    public string LastErrorMessage => _lastError;
    public int DotNetExtensionsEnabled { set => _dotNetExtensions = value; }
    public int JavaExtensionsEnabled { set => _javaExtensions = value; }

    public void AddExternalVariable(string bstrName, string bstrVal)
    {
        if (bstrName is null) throw new ArgumentNullException(nameof(bstrName));
        _variables.RemoveAll(p => p.Key == bstrName);
        _variables.Add(new KeyValuePair<string, string>(bstrName, bstrVal ?? string.Empty));
    }

    public void ClearExternalVariableList() => _variables.Clear();

    public void AddExternalVariableAsXPath(string bstrName, string bstrValueExpression)
    {
        if (bstrName is null) throw new ArgumentNullException(nameof(bstrName));
        _variablesAsXPath.RemoveAll(p => p.Key == bstrName);
        _variablesAsXPath.Add(new KeyValuePair<string, string>(bstrName, bstrValueExpression ?? string.Empty));
    }

    public void Execute(string bstrOutputFileName)
    {
        Run(bstrOutputFileName);
    }

    public string ExecuteAndGetResultAsString()
    {
        var result = Run(outputPath: null);
        return result.OutputText ?? string.Empty;
    }

    private XQueryResult Run(string? outputPath)
    {
        _lastError = string.Empty;

        bool queryFromFile = !string.IsNullOrEmpty(_xQueryFileName);
        bool queryFromText = !string.IsNullOrEmpty(_xQueryFromText);
        bool inputFromFile = !string.IsNullOrEmpty(_inputXmlFileName);
        bool inputFromText = !string.IsNullOrEmpty(_inputXmlFromText);

        if (!queryFromFile && !queryFromText)
        {
            _lastError = "No XQuery specified: set XQueryFileName or XQueryFromText.";
            return new XQueryResult { LastErrorMessage = _lastError };
        }

        if (!inputFromFile && !inputFromText)
        {
            _lastError = "No input XML specified: set InputXMLFileName or InputXMLFromText.";
            return new XQueryResult { LastErrorMessage = _lastError };
        }

        if (_dotNetExtensions != 0 || _javaExtensions != 0)
        {
            _lastError = "Extension functions are not supported: this implementation is 100% managed " +
                         "with no COM/script host.";
            return new XQueryResult { LastErrorMessage = _lastError };
        }

        var request = new XQueryRequest
        {
            XQueryPath = queryFromFile ? _xQueryFileName : null,
            XQueryText = queryFromText ? _xQueryFromText : null,
            InputXmlPath = inputFromFile ? _inputXmlFileName : null,
            InputXmlText = inputFromText ? _inputXmlFromText : null,
            OutputPath = outputPath,
            OutputEncoding = _outputEncoding,
            OutputIndent = _outputIndent,
            OutputMethod = _outputMethod,
            OutputOmitXmlDeclaration = _outputOmitXmlDeclaration,
            ExternalVariables = _variables.Count == 0 ? null : _variables.ToDictionary(p => p.Key, p => p.Value),
            ExternalVariablesAsXPath = _variablesAsXPath.Count == 0 ? null : _variablesAsXPath.ToDictionary(p => p.Key, p => p.Value)
        };

        var engine = _router.SelectXQueryEngine();
        var result = engine.Execute(request);

        if (!result.IsSuccess)
        {
            _lastError = result.LastErrorMessage ?? "XQuery execution failed.";
        }
        else if (!string.IsNullOrEmpty(outputPath) && result.OutputText is null && File.Exists(outputPath))
        {
            result = new XQueryResult
            {
                OutputText = File.ReadAllText(outputPath),
                Output = File.ReadAllBytes(outputPath)
            };
        }

        return result;
    }
}