// AltovaGen Compat — Application

using AltovaGen.Abstractions;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free implementation of <see cref="IApplication"/>. Composed only
/// from the four facade interfaces; the DI container supplies the implementations
/// (register with <c>services.AddAltovaGen()</c>).
/// </summary>
internal sealed class Application(
    IXSLT1 xslt1,
    IXSLT2 xslt2,
    IXQuery xquery,
    IXMLValidator validator) : IApplication
{
    public IXSLT1 XSLT1 { get; } = xslt1 ?? throw new ArgumentNullException(nameof(xslt1));
    public IXSLT2 XSLT2 { get; } = xslt2 ?? throw new ArgumentNullException(nameof(xslt2));
    public IXQuery XQuery { get; } = xquery ?? throw new ArgumentNullException(nameof(xquery));
    public IXMLValidator XMLValidator { get; } = validator ?? throw new ArgumentNullException(nameof(validator));
}
