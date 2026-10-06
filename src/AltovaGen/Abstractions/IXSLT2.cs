// AltovaGen Compat — IXSLT2

namespace AltovaGen.Compat;

/// <summary>
/// Reproduces Altova.AltovaXML.IXSLT2 (COM PIA v12.3).
/// </summary>
public interface IXSLT2 : IXSLT1
{
    string InitialTemplateName { set; }
    string InitialTemplateMode { set; }
}