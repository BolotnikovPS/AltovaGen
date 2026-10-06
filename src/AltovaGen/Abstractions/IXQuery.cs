// AltovaGen Compat — IXQuery

namespace AltovaGen.Abstractions;

/// <summary>
/// Reproduces Altova.AltovaXML.IXQuery (COM PIA v12.3).
/// </summary>
public interface IXQuery
{
    string XQueryFileName { set; }
    string InputXMLFileName { set; }
    string XQueryFromText { set; }
    string InputXMLFromText { set; }
    string OutputEncoding { get; set; }
    bool OutputIndent { get; set; }
    string OutputMethod { get; set; }
    bool OutputOmitXMLDeclaration { get; set; }
    string LastErrorMessage { get; }
    int DotNetExtensionsEnabled { set; }
    int JavaExtensionsEnabled { set; }

    void Execute(string bstrOutputFileName);
    string ExecuteAndGetResultAsString();
    void AddExternalVariable(string bstrName, string bstrVal);
    void ClearExternalVariableList();
    void AddExternalVariableAsXPath(string bstrName, string bstrValueExpression);
}
