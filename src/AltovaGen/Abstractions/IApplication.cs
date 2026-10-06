// AltovaGen Compat — IApplication

using AltovaGen.Compat;

namespace AltovaGen.Abstractions;

/// <summary>
/// Reproduces Altova.AltovaXML.IApplication (COM PIA v12.3).
/// Entry point that exposes the four processing engines.
/// </summary>
public interface IApplication
{
    IXSLT1 XSLT1 { get; }
    IXSLT2 XSLT2 { get; }
    IXQuery XQuery { get; }
    IXMLValidator XMLValidator { get; }
}