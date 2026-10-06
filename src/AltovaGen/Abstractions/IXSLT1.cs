// AltovaGen Compat — IXSLT1

namespace AltovaGen.Compat;

/// <summary>
/// Reproduces Altova.AltovaXML.IXSLT1 (COM PIA v12.3).
/// </summary>
public interface IXSLT1
{
    string InputXMLFileName { set; }
    string XSLFileName { set; }
    string InputXMLFromText { set; }
    string XSLFromText { set; }
    int XSLStackSize { set; }
    string LastErrorMessage { get; }
    int DotNetExtensionsEnabled { set; }
    int JavaExtensionsEnabled { set; }

    void Execute(string bstrOutputFileName);
    string ExecuteAndGetResultAsString();
    void AddExternalParameter(string bstrName, string bstrVal);
    void ClearExternalParameterList();
}