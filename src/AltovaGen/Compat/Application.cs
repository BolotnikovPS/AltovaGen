// AltovaGen Compat — Application

using AltovaGen.Abstractions;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free implementation of <see cref="IApplication"/>. Composed only
/// from the four facade interfaces; the DI container supplies the implementations
/// (register with <c>services.AddAltovaGen()</c>).
/// </summary>
internal sealed class Application : IApplication
{
    public Application(IXSLT1 xslt1, IXSLT2 xslt2, IXQuery xquery, IXMLValidator validator)
    {
        XSLT1 = xslt1 ?? throw new ArgumentNullException(nameof(xslt1));
        XSLT2 = xslt2 ?? throw new ArgumentNullException(nameof(xslt2));
        XQuery = xquery ?? throw new ArgumentNullException(nameof(xquery));
        XMLValidator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public IXSLT1 XSLT1 { get; }
    public IXSLT2 XSLT2 { get; }
    public IXQuery XQuery { get; }
    public IXMLValidator XMLValidator { get; }
}
