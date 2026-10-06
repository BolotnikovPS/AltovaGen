# AltovaGen

## What is AltovaGen?

This library provides a managed, AnyCPU .NET 8+ API compatible with `Altova.AltovaXML.dll` (the COM interop wrapper from Altova XMLSpy 2026). **Zero COM dependencies**, **fully cross-platform** (Windows, Linux, macOS).

## Features

| Engine | XSLT | XQuery | XSD | DTD | Comments |
|--------|------|--------|-----|-----|----------|
| **BCL** (default) | 1.0 | 1.0 subset (XPath 1.0) | 1.0 | Well-formedness | Built-in, no external dependencies |
| **SaxonCS-HE** (bundled) | 2.0/3.0 | — | — | — | `altova:evaluate` bridge; XQuery/XSD 1.1 not wired |

## Usage

### Dependency injection (recommended)

The public API is interfaces only — implementation classes are `internal`.
Register the services and resolve the facades:

```csharp
using AltovaGen.Compat;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAltovaGen();
using var provider = services.BuildServiceProvider();

// The IApplication facade mirrors the original Altova.AltovaXML surface
var app = provider.GetRequiredService<IApplication>();

app.XSLT1.InputXMLFromText = "<doc><name>Test</name></doc>";
app.XSLT1.XSLFromText = """
    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
      <xsl:output method="text"/>
      <xsl:template match="/"><xsl:value-of select="//name"/></xsl:template>
    </xsl:stylesheet>
    """;

string result = app.XSLT1.ExecuteAndGetResultAsString();
// result == "Test"
```

### XSLT 2.0/3.0

Use `IXSLT2` for stylesheets that need XSLT 2.0/3.0 or the Altova extension
namespace `xmlns:altova="http://www.w3.org/.../xslt-extensions"`. It is routed
to the bundled SaxonCS-HE engine:

```csharp
var xslt2 = provider.GetRequiredService<IXSLT2>();
xslt2.InputXMLFileName = "sales.xml";
xslt2.XSLFileName      = "sales-report.xslt";
xslt2.Execute("sales-report.fo");
```

### Validation

```csharp
var validator = provider.GetRequiredService<IXMLValidator>();
validator.InputXMLFromText = xmlContent;
validator.SchemaFromText   = xsdContent;

bool isValid = validator.IsValid();
```

Engines are constructor-injected into the router, so `IEngineRouter.SelectXsltEngine(2.0)`
returns the same `SaxonXsltEngine` instance the container registered — XSLT 2.0/3.0 (and
the `altova:evaluate` bridge) work out of the box, with no manual registration call.

To swap the Saxon XSLT engine or to wire XQuery 3.1 / XSD 1.1 engines (not bundled), use
the `Action<AltovaGenOptions>` overload — the DI counterpart of
`IEngineRouter.RegisterSaxonEngines`:

```csharp
services.AddAltovaGen(options =>
{
    options.SaxonXslt       = mySaxonXsltEngine;    // null ⇒ bundled SaxonCS-HE
    options.SaxonXQuery     = myXQuery31Engine;     // null ⇒ BCL XPath subset
    options.SaxonValidation = myXsd11Engine;        // null ⇒ XSD 1.1 throws
});
```

Registered services:

| Lifetime | Services |
|----------|----------|
| Singleton (stateless) | `IEngineRouter` → `EngineRouter`, `BclXsltEngine`, `BclXQueryEngine`, `BclValidatorEngine`, `SaxonXsltEngine` |
| Transient (per-operation state) | `IApplication`, `IXSLT1`, `IXSLT2`, `IXQuery`, `IXMLValidator` |

## Installation

```bash
dotnet add package AltovaGen
```

## Requirements

- .NET 8.0 or later
- AnyCPU / platform-agnostic (no x86 dependency)

The original 32-bit `Altova.AltovaXML.dll` and any Altova installation are **not**
required — neither at build time nor at runtime. AltovaGen has zero COM
dependencies and never loads that assembly.

## Building and testing

```bash
dotnet build AltovaGen.slnx -c Release
dotnet test  AltovaGen.slnx -c Release
```

## License

MIT License — see [LICENSE](LICENSE). The bundled SaxonCS-HE engine is Saxonica's
Home Edition and is distributed under its own open-source license (MPL-2.0); it is
consumed as a NuGet dependency and is not redistributed inside this package, and it
requires no license key. Review [Saxonica's terms](https://www.saxonica.com) for
your distribution model.
