using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using AltovaGen.Abstractions;
using AltovaGen.Compat;
using AltovaGen.DependencyInjection;
using AltovaGen.Engine.Bcl;
using AltovaGen.Engine.Saxon;
using AltovaGen.Helpers;
using AltovaGen.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AltovaGen.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAltovaGen_RegistersEnginesRouterAndFacades()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<EngineRouter>(provider.GetRequiredService<IEngineRouter>());
        Assert.IsType<SaxonXsltEngine>(provider.GetRequiredService<SaxonXsltEngine>());
        Assert.IsType<BclXsltEngine>(provider.GetRequiredService<BclXsltEngine>());
        Assert.IsType<BclXQueryEngine>(provider.GetRequiredService<BclXQueryEngine>());
        Assert.IsType<BclValidatorEngine>(provider.GetRequiredService<BclValidatorEngine>());

        Assert.IsType<Application>(provider.GetRequiredService<IApplication>());
        Assert.IsType<XSLT1>(provider.GetRequiredService<IXSLT1>());
        Assert.IsType<XSLT2>(provider.GetRequiredService<IXSLT2>());
        Assert.IsType<XQuery>(provider.GetRequiredService<IXQuery>());
        Assert.IsType<XMLValidator>(provider.GetRequiredService<IXMLValidator>());
    }

    [Fact]
    public void AddAltovaGen_RouterAndEnginesAreSingletons_FacadesAreTransient()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IEngineRouter>(),
                    provider.GetRequiredService<IEngineRouter>());
        Assert.Same(provider.GetRequiredService<SaxonXsltEngine>(),
                    provider.GetRequiredService<SaxonXsltEngine>());

        Assert.NotSame(provider.GetRequiredService<IXQuery>(),
                       provider.GetRequiredService<IXQuery>());
        Assert.NotSame(provider.GetRequiredService<IApplication>(),
                       provider.GetRequiredService<IApplication>());
    }

    [Fact]
    public void AddAltovaGen_ResolvedXslt1Facade_TransformsThroughWiredRouter()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        var xslt = provider.GetRequiredService<IXSLT1>();
        xslt.InputXMLFromText = "<root><item>Hello</item></root>";
        xslt.XSLFromText = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text"/>
              <xsl:template match="/">
                <xsl:value-of select="root/item"/>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal("Hello", result.Trim());
        Assert.Equal(string.Empty, xslt.LastErrorMessage);
    }

    [Fact]
    public void AddAltovaGen_ResolvedApplication_ExposesAllFacades()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        var app = provider.GetRequiredService<IApplication>();

        Assert.NotNull(app.XSLT1);
        Assert.NotNull(app.XSLT2);
        Assert.NotNull(app.XQuery);
        Assert.NotNull(app.XMLValidator);
    }

    [Fact]
    public void AddAltovaGen_WiresBundledSaxonEngineIntoTheRouter()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IEngineRouter>();
        var engine = router.SelectXsltEngine(2.0);

        // XSLT 2.0/3.0 works out of the box: no manual RegisterSaxonEngines call needed.
        Assert.IsType<SaxonXsltEngine>(engine);
        Assert.Same(provider.GetRequiredService<SaxonXsltEngine>(), engine);
        Assert.Same(engine, router.SelectXsltEngine(3.0, requireExtensions: true));
    }

    [Fact]
    public void AddAltovaGen_DefaultRouter_KeepsXQueryOnBclAndRefusesXsd11()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IEngineRouter>();

        Assert.IsType<BclXQueryEngine>(router.SelectXQueryEngine());
        Assert.Throws<NotSupportedException>(() => router.SelectValidationEngine(1.1, requireXsd11: true));
    }

    [Fact]
    public void AddAltovaGen_WithOptions_RegistersCustomSaxonEngines()
    {
        var customXslt = new StubXsltEngine();
        var customXQuery = new StubXQueryEngine();
        var customValidation = new StubValidationEngine();

        var services = new ServiceCollection();
        services.AddAltovaGen(options =>
        {
            options.SaxonXslt = customXslt;
            options.SaxonXQuery = customXQuery;
            options.SaxonValidation = customValidation;
        });
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IEngineRouter>();

        Assert.Same(customXslt, router.SelectXsltEngine(3.0, requireExtensions: true));
        Assert.Same(customXQuery, router.SelectXQueryEngine());
        Assert.Same(customValidation, router.SelectValidationEngine(1.1, requireXsd11: true));

        // XSLT 1.0 / XSD 1.0 keep using the BCL engines.
        Assert.IsType<BclXsltEngine>(router.SelectXsltEngine(1.0));
    }

    [Fact]
    public void AddAltovaGen_Options_NullXslt_FallsBackToBundledSaxonEngine()
    {
        var services = new ServiceCollection();
        services.AddAltovaGen(options => options.SaxonXslt = null);
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IEngineRouter>();

        // A null option means "no override": the bundled SaxonCS-HE engine is used.
        Assert.IsType<SaxonXsltEngine>(router.SelectXsltEngine(2.0));
    }

    private sealed class StubXsltEngine : IXsltEngine
    {
        public EngineCapabilities Capabilities => EngineCapabilities.Saxon;

        public TransformResult Transform(TransformRequest request) => throw new NotSupportedException();

        public Task<TransformResult> TransformAsync(TransformRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubXQueryEngine : IXQueryEngine
    {
        public EngineCapabilities Capabilities => EngineCapabilities.Saxon;

        public XQueryResult Execute(XQueryRequest request) => throw new NotSupportedException();

        public Task<XQueryResult> ExecuteAsync(XQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubValidationEngine : IValidationEngine
    {
        public EngineCapabilities Capabilities => EngineCapabilities.Saxon;

        public ValidationResult Validate(ValidationRequest request) => throw new NotSupportedException();

        public Task<ValidationResult> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public class XsltTests
{
    [Fact]
    public void Xslt1_TransformTextToText_ReturnsExpectedOutput()
    {
        var xslt = new XSLT1(new EngineRouter());
        xslt.InputXMLFromText = "<root><item>Hello</item></root>";
        xslt.XSLFromText = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text"/>
              <xsl:template match="/">
                <xsl:value-of select="root/item"/>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal("Hello", result.Trim());
        Assert.Equal(string.Empty, xslt.LastErrorMessage);
    }

    [Fact]
    public void Xslt1_WithExternalParameter_UsesParameterValue()
    {
        var xslt = new XSLT1(new EngineRouter());
        xslt.InputXMLFromText = "<root/>";
        xslt.XSLFromText = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text"/>
              <xsl:param name="who" select="'world'"/>
              <xsl:template match="/">
                <xsl:value-of select="concat('Hello ', $who)"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        xslt.AddExternalParameter("who", "AltovaGen");

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal("Hello AltovaGen", result.Trim());
    }

    [Fact]
    public void Xslt2_AltovaEvaluate_AcceptsNodeArgument_AndUsesItsStringValue()
    {
        // Regression: altova:evaluate() on a *node* must use the node's string value,
        // not its XML markup (XdmNode.ToString() returns "<expr>2+3</expr>", which would
        // fail to compile as an XPath expression).
        var xslt = new XSLT2(new EngineRouter());
        xslt.InputXMLFromText = "<root><expr>2+3</expr></root>";
        xslt.XSLFromText = """
            <xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:altova="http://www.altova.com/xslt-extensions">
              <xsl:output method="text"/>
              <xsl:template match="/">
                <xsl:value-of select="altova:evaluate(/*/expr)"/>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal("5", result.Trim());
        Assert.Equal(string.Empty, xslt.LastErrorMessage);
    }
}

public class XQueryTests
{
    [Fact]
    public void XQuery_BasicPathQuery_ReturnsNodes()
    {
        var xquery = new XQuery(new EngineRouter());
        xquery.InputXMLFromText = "<catalog><book id='1'>A</book><book id='2'>B</book></catalog>";
        xquery.XQueryFromText = "/catalog/book/text()";

        var result = xquery.ExecuteAndGetResultAsString();

        Assert.Equal("AB", result.Replace("\n", "").Replace(" ", ""));
        Assert.Equal(string.Empty, xquery.LastErrorMessage);
    }

    [Fact]
    public void XQuery_FlworSyntax_ReturnsErrorOnBclRoute()
    {
        var xquery = new XQuery(new EngineRouter());
        xquery.InputXMLFromText = "<catalog><book>A</book></catalog>";
        xquery.XQueryFromText = "for $b in /catalog/book return $b/text()";

        var result = xquery.ExecuteAndGetResultAsString();

        Assert.Equal("", result);
        Assert.NotEqual(string.Empty, xquery.LastErrorMessage);
    }
}

public class ValidationTests
{
    [Fact]
    public void ValidXml_IsWellFormed_ReturnsTrue()
    {
        var validator = new XMLValidator(new EngineRouter());
        validator.InputXMLFromText = "<root><child>value</child></root>";

        var valid = validator.IsWellFormed();

        Assert.True(valid);
        Assert.Equal(string.Empty, validator.LastErrorMessage);
    }

    [Fact]
    public void MalformedXml_IsWellFormed_ReturnsFalseAndSetsError()
    {
        var validator = new XMLValidator(new EngineRouter());
        validator.InputXMLFromText = "<root><child>value</root>";

        var valid = validator.IsWellFormed();

        Assert.False(valid);
        Assert.NotEqual(string.Empty, validator.LastErrorMessage);
    }

    [Fact]
    public void ValidXml_AgainstXsd_ReturnsTrue()
    {
        var validator = new XMLValidator(new EngineRouter());
        validator.InputXMLFromText = "<person><name>Alice</name></person>";
        validator.SchemaFromText = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="person">
                <xs:complexType>
                  <xs:sequence>
                    <xs:element name="name" type="xs:string"/>
                  </xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """;

        var valid = validator.IsValid();

        Assert.True(valid);
        Assert.Equal(string.Empty, validator.LastErrorMessage);
    }

    [Fact]
    public void InvalidXml_AgainstXsd_ReturnsFalse()
    {
        var validator = new XMLValidator(new EngineRouter());
        validator.InputXMLFromText = "<person><name>Alice</name><age>oops</age></person>";
        validator.SchemaFromText = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="person">
                <xs:complexType>
                  <xs:sequence>
                    <xs:element name="name" type="xs:string"/>
                  </xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """;

        var valid = validator.IsValid();

        Assert.False(valid);
        Assert.NotEqual(string.Empty, validator.LastErrorMessage);
    }

    [Fact]
    public void BclValidator_SchemaInvalidDocument_ReportsLineInfo_AndKeepsWellFormedTrue()
    {
        // Regression: a schema-invalid (but well-formed) document must be reported as
        // IsWellFormed=true with position info in the message, not swallowed as a bare
        // XmlSchemaException falling back to the generic handler.
        var engine = new AltovaGen.Engine.Bcl.BclValidatorEngine();
        var request = new ValidationRequest
        {
            InputXmlText = "<person><name>Alice</name><age>oops</age></person>",
            SchemaText = """
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                  <xs:element name="person">
                    <xs:complexType>
                      <xs:sequence>
                        <xs:element name="name" type="xs:string"/>
                      </xs:sequence>
                    </xs:complexType>
                  </xs:element>
                </xs:schema>
                """
        };

        var result = engine.Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.IsWellFormed);
        Assert.Contains("line", result.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

public class EngineTests
{
    [Fact]
    public void BclEngine_Capabilities_ReportsXsd10Only()
    {
        var engine = new AltovaGen.Engine.Bcl.BclValidatorEngine();

        Assert.Equal(1.0, engine.Capabilities.XsdVersion);
        Assert.True(engine.Capabilities.CanValidateXsd11 == false);
    }

    [Fact]
    public void Router_Xslt10_UsesBcl()
    {
        var router = new AltovaGen.Engine.Bcl.EngineRouter();

        var engine = router.SelectXsltEngine(1.0);

        Assert.IsType<AltovaGen.Engine.Bcl.BclXsltEngine>(engine);
    }
}

public class DeepSeekXmlIntegrationTests
{
    /// <summary>
    /// Integration tests over the real DeepSeek XML fixtures:
    /// - deepseek_xml_20261003_b25c49.xml: sales document (4 items, 26650000 total)
    /// - deepseek_xml_20261003_4d888a.xslt: the original Altova XSLT 2.0 stylesheet
    ///   that renders the sales document as XSL-FO for PDF, using
    ///   xmlns:altova="http://www.altova.com/xslt-extensions" and altova:evaluate()
    /// - sales-report-fo.xslt: XSLT 1.0 port of that stylesheet, runnable on the
    ///   managed BCL route
    ///
    /// The original stylesheet is executed as-is by XSLT2, which the router
    /// sends to the bundled SaxonCS-HE engine (XSLT 2.0/3.0). The altova:*
    /// namespace is served by a managed, COM-free emulation of the Altova extension
    /// functions, so no Altova COM server and no 32-bit process is involved.
    /// The XSLT 1.0 port is still covered, because it is what the BCL-only route
    /// produces.
    /// </summary>
    [Fact]
    public void DeepSeekXml_Xslt1_TransformSalesReport_ToPlainText()
    {
        // Arrange: Load the XML and XSLT files
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "deepseek_xml_20261003_b25c49.xml");

        // The original XSLT uses altova:evaluate() and FO elements (not supported by BCL XSLT 1.0)
        // So we create a simplified XSLT 1.0 version that extracts the same core data
        var simplifiedXsl = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text" encoding="UTF-8" indent="no"/>
              <xsl:template match="/">
                <xsl:text>Sales Report for </xsl:text>
                <xsl:value-of select="//document/title"/>
                <xsl:text>&#10;</xsl:text>
                <xsl:text>Sub: </xsl:text>
                <xsl:value-of select="//document/subtitle"/>
                <xsl:text>&#10;&#10;</xsl:text>
                <xsl:text>Total Items: </xsl:text>
                <xsl:value-of select="count(//items/item)"/>
                <xsl:text>&#10;</xsl:text>
                <xsl:text>Total Sum: </xsl:text>
                <xsl:value-of select="sum(//items/item/@price)"/>
                <xsl:text> rub.&#10;</xsl:text>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var xslt = new XSLT1(new EngineRouter());
        xslt.InputXMLFromText = File.ReadAllText(xmlPath);
        xslt.XSLFromText = simplifiedXsl;

        // Act
        var result = xslt.ExecuteAndGetResultAsString();

        // Assert
        Assert.Equal(string.Empty, xslt.LastErrorMessage);
        Assert.Contains("Sales Report", result);
        Assert.Contains("Отчёт по продажам за 2026 год", result);
        Assert.Contains("Total Items: 4", result);
    }


    private static readonly XNamespace FoNs = "http://www.w3.org/1999/XSL/Format";

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, fileName);

    /// <summary>
    /// Runs the real XML fixture through the XSLT 1.0 FO port and returns the
    /// parsed result. Parsing with XDocument is itself an assertion: it proves the
    /// transformation emitted a well-formed XML document (an XSL-FO instance),
    /// not plain text or an empty string.
    /// </summary>
    private static XDocument RunFoStylesheet()
    {
        var xslt = new XSLT1(new EngineRouter());
        xslt.InputXMLFromText = File.ReadAllText(FixturePath("deepseek_xml_20261003_b25c49.xml"));
        xslt.XSLFromText = File.ReadAllText(FixturePath("sales-report-fo.xslt"));

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal(string.Empty, xslt.LastErrorMessage);
        Assert.NotEqual(string.Empty, result);

        return XDocument.Parse(result);
    }

    /// <summary>
    /// Runs the ORIGINAL Altova XSLT 2.0 fixture (version="2.0",
    /// xmlns:altova="http://www.altova.com/xslt-extensions", altova:evaluate(),
    /// XSLT 2.0 sequence arithmetic) through XSLT2 and returns the parsed
    /// result. This is the stylesheet the user actually ships: it must produce a
    /// real XSL-FO instance, not an error and not an empty document.
    /// </summary>
    private static XDocument RunAltovaFoStylesheet()
    {
        var xslt = new XSLT2(new EngineRouter());
        xslt.InputXMLFromText = File.ReadAllText(FixturePath("deepseek_xml_20261003_b25c49.xml"));
        xslt.XSLFromText = File.ReadAllText(FixturePath("deepseek_xml_20261003_4d888a.xslt"));

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal(string.Empty, xslt.LastErrorMessage);
        Assert.NotEqual(string.Empty, result);

        return XDocument.Parse(result);
    }

    [Fact]
    public void DeepSeekXml_Xslt2_UsesSaxonEngine_NotBcl()
    {
        // XSLT2 must demand XSLT 2.0, so the router picks the Saxon engine
        // instead of the XSLT 1.0-only BCL processor.
        var router = new AltovaGen.Engine.Bcl.EngineRouter();

        var engine = router.SelectXsltEngine(2.0);

        Assert.IsType<AltovaGen.Engine.Saxon.SaxonXsltEngine>(engine);
        Assert.True(engine.Capabilities.XsltVersion >= 2.0);

        // An XSLT 1.0 request still stays on the BCL route.
        Assert.IsType<AltovaGen.Engine.Bcl.BclXsltEngine>(router.SelectXsltEngine(1.0));

        // A stylesheet that uses the Altova extension namespace is routed to Saxon
        // even when the caller only asks for XSLT 1.0.
        Assert.IsType<AltovaGen.Engine.Saxon.SaxonXsltEngine>(router.SelectXsltEngine(1.0, true));
    }

    [Fact]
    public void DeepSeekXml_Xslt2_GeneratesXslFo_Document()
    {
        var doc = RunAltovaFoStylesheet();

        Assert.Equal(FoNs + "root", doc.Root!.Name);

        var master = doc.Root
            .Element(FoNs + "layout-master-set")?
            .Element(FoNs + "simple-page-master");

        Assert.NotNull(master);
        Assert.Equal("A4", master!.Attribute("master-name")?.Value);
        Assert.Equal("29.7cm", master.Attribute("page-height")?.Value);
        Assert.Equal("21cm", master.Attribute("page-width")?.Value);

        var pageSequence = doc.Root.Element(FoNs + "page-sequence");
        Assert.NotNull(pageSequence);
        Assert.Equal("A4", pageSequence!.Attribute("master-reference")?.Value);

        var staticContent = pageSequence.Element(FoNs + "static-content");
        Assert.NotNull(staticContent);
        Assert.Equal("xsl-region-after", staticContent!.Attribute("flow-name")?.Value);
        Assert.Single(staticContent.Descendants(FoNs + "page-number"));

        var flow = pageSequence.Element(FoNs + "flow");
        Assert.NotNull(flow);
        Assert.Equal("xsl-region-body", flow!.Attribute("flow-name")?.Value);
        Assert.NotNull(flow.Element(FoNs + "table"));
    }

    [Fact]
    public void DeepSeekXml_Xslt2_FoTable_HasOneRowPerItemWithCorrectLineTotals()
    {
        // The line total is computed with XSLT 2.0 sequence arithmetic
        // (@price * quantity) - this only works on a real 2.0 processor.
        var doc = RunAltovaFoStylesheet();

        var rows = doc.Descendants(FoNs + "table-body")
                      .Elements(FoNs + "table-row")
                      .ToList();

        Assert.Equal(4, rows.Count);

        var expected = new[]
        {
            (Id: "1", Name: "Ноутбук Pro 15",    Qty: "120", Price: "100000", Line: "12000000"),
            (Id: "2", Name: "Смартфон X200",     Qty: "350", Price: "25000",  Line: "8750000"),
            (Id: "3", Name: "Планшет Tab Mini",  Qty: "80",  Price: "30000",  Line: "2400000"),
            (Id: "4", Name: "Наушники AirSound", Qty: "500", Price: "7000",   Line: "3500000")
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var cells = rows[i].Elements(FoNs + "table-cell")
                               .Select(cell => cell.Value.Trim())
                               .ToList();

            Assert.Equal(5, cells.Count);
            Assert.Equal(expected[i].Id, cells[0]);
            Assert.Equal(expected[i].Name, cells[1]);
            Assert.Equal(expected[i].Qty, cells[2]);
            Assert.Equal(expected[i].Price, cells[3]);
            Assert.Equal(expected[i].Line, cells[4]);
        }
    }

    [Fact]
    public void DeepSeekXml_Xslt2_FoTableFooter_ContainsGrandTotal()
    {
        // Grand total: sum(for $i in //items/item return $i/@price * $i/quantity)
        var doc = RunAltovaFoStylesheet();

        var footerCells = doc.Descendants(FoNs + "table-footer")
                             .Elements(FoNs + "table-row")
                             .Elements(FoNs + "table-cell")
                             .Select(cell => cell.Value.Trim())
                             .ToList();

        Assert.Equal(2, footerCells.Count);
        Assert.Equal("Итого:", footerCells[0]);
        // 100000*120 + 25000*350 + 30000*80 + 7000*500 = 26650000
        Assert.Equal("26650000", footerCells[1]);
    }

    [Fact]
    public void DeepSeekXml_Xslt2_EvaluatesAltovaExtensionFunction()
    {
        var doc = RunAltovaFoStylesheet();

        // The stylesheet calls altova:evaluate($expr) where $expr is the content of
        // <dynamic-expression> ("../title/text()"), evaluated relative to that node.
        // The managed Altova extension bridge must resolve it to the document title
        // instead of failing or returning an empty string.
        var block = doc.Descendants(FoNs + "block")
                       .SingleOrDefault(b => b.Value.Contains("Результат altova:evaluate()"));

        Assert.NotNull(block);
        Assert.Contains("Результат altova:evaluate(): Отчёт по продажам за 2026 год", block!.Value);
    }

    [Fact]
    public void DeepSeekXml_Xslt2_FoDocument_CarriesTitleSubtitleDateAndComments()
    {
        var text = RunAltovaFoStylesheet().Root!.Value;

        Assert.Contains("Отчёт по продажам за 2026 год", text);
        Assert.Contains("Квартальный срез: Q1 2026", text);
        Assert.Contains("2026-10-03", text);
        Assert.Contains("Документ сформирован автоматически", text);

        Assert.Contains("Комментарии по позициям:", text);
        foreach (var item in new[] { "Ноутбук Pro 15", "Смартфон X200", "Планшет Tab Mini", "Наушники AirSound" })
        {
            Assert.Contains(item, text);
        }
    }

    [Fact]
    public void DeepSeekXml_Xslt2_Execute_WritesFoFileToDisk()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"altovagen-fo2-{Guid.NewGuid():N}.fo");

        try
        {
            var xslt = new XSLT2(new EngineRouter())
            {
                InputXMLFileName = FixturePath("deepseek_xml_20261003_b25c49.xml"),
                XSLFileName = FixturePath("deepseek_xml_20261003_4d888a.xslt")
            };

            xslt.Execute(outputPath);

            Assert.Equal(string.Empty, xslt.LastErrorMessage);
            Assert.True(File.Exists(outputPath), $"FO document was not written to {outputPath}");

            var doc = XDocument.Parse(File.ReadAllText(outputPath));
            Assert.Equal(FoNs + "root", doc.Root!.Name);
            Assert.Equal(4, doc.Descendants(FoNs + "table-body").Elements(FoNs + "table-row").Count());
            Assert.Contains("26650000", doc.Root.Value);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void DeepSeekXml_Xslt2_IsStillRejectedByTheXslt10Engine()
    {
        // Regression guard: the BCL engine must not silently "succeed" on the XSLT
        // 2.0 fixture. Only the Saxon route may process it.
        var bcl = new AltovaGen.Engine.Bcl.BclXsltEngine();

        var result = bcl.Transform(new TransformRequest
        {
            InputXmlText = File.ReadAllText(FixturePath("deepseek_xml_20261003_b25c49.xml")),
            XslText = File.ReadAllText(FixturePath("deepseek_xml_20261003_4d888a.xslt"))
        });

        Assert.False(result.IsSuccess);
        Assert.NotEqual(string.Empty, result.LastErrorMessage);
    }

    [Fact]
    public void DeepSeekXml_Xslt1_GeneratesXslFo_Document()
    {
        var doc = RunFoStylesheet();

        // Root must be fo:root in the XSL-FO namespace.
        Assert.Equal(FoNs + "root", doc.Root!.Name);

        // Page geometry: A4 master, referenced by the page sequence.
        var master = doc.Root
            .Element(FoNs + "layout-master-set")?
            .Element(FoNs + "simple-page-master");

        Assert.NotNull(master);
        Assert.Equal("A4", master!.Attribute("master-name")?.Value);
        Assert.Equal("29.7cm", master.Attribute("page-height")?.Value);
        Assert.Equal("21cm", master.Attribute("page-width")?.Value);

        var pageSequence = doc.Root.Element(FoNs + "page-sequence");
        Assert.NotNull(pageSequence);
        Assert.Equal("A4", pageSequence!.Attribute("master-reference")?.Value);

        // The footer must carry a live page number - that is what makes FO paginate.
        var staticContent = pageSequence.Element(FoNs + "static-content");
        Assert.NotNull(staticContent);
        Assert.Equal("xsl-region-after", staticContent!.Attribute("flow-name")?.Value);
        Assert.Single(staticContent.Descendants(FoNs + "page-number"));

        // Body flow with the data table.
        var flow = pageSequence.Element(FoNs + "flow");
        Assert.NotNull(flow);
        Assert.Equal("xsl-region-body", flow!.Attribute("flow-name")?.Value);
        Assert.NotNull(flow.Element(FoNs + "table"));
    }

    [Fact]
    public void DeepSeekXml_Xslt1_FoTable_HasOneRowPerItemWithCorrectLineTotals()
    {
        var doc = RunFoStylesheet();

        var rows = doc.Descendants(FoNs + "table-body")
                      .Elements(FoNs + "table-row")
                      .ToList();

        Assert.Equal(4, rows.Count);

        var expected = new[]
        {
            (Id: "1", Name: "Ноутбук Pro 15",    Qty: "120", Price: "100000", Line: "12000000"),
            (Id: "2", Name: "Смартфон X200",     Qty: "350", Price: "25000",  Line: "8750000"),
            (Id: "3", Name: "Планшет Tab Mini",  Qty: "80",  Price: "30000",  Line: "2400000"),
            (Id: "4", Name: "Наушники AirSound", Qty: "500", Price: "7000",   Line: "3500000")
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var cells = rows[i].Elements(FoNs + "table-cell")
                               .Select(cell => cell.Value.Trim())
                               .ToList();

            Assert.Equal(5, cells.Count);
            Assert.Equal(expected[i].Id, cells[0]);
            Assert.Equal(expected[i].Name, cells[1]);
            Assert.Equal(expected[i].Qty, cells[2]);
            Assert.Equal(expected[i].Price, cells[3]);
            Assert.Equal(expected[i].Line, cells[4]);
        }
    }

    [Fact]
    public void DeepSeekXml_Xslt1_FoTableFooter_ContainsGrandTotal()
    {
        var doc = RunFoStylesheet();

        var footerCells = doc.Descendants(FoNs + "table-footer")
                             .Elements(FoNs + "table-row")
                             .Elements(FoNs + "table-cell")
                             .Select(cell => cell.Value.Trim())
                             .ToList();

        Assert.Equal(2, footerCells.Count);
        Assert.Equal("Итого:", footerCells[0]);
        // 100000*120 + 25000*350 + 30000*80 + 7000*500 = 26650000
        Assert.Equal("26650000", footerCells[1]);
    }

    [Fact]
    public void DeepSeekXml_Xslt1_FoDocument_CarriesTitleSubtitleAndGeneratedDate()
    {
        var text = RunFoStylesheet().Root!.Value;

        Assert.Contains("Отчёт по продажам за 2026 год", text);
        Assert.Contains("Квартальный срез: Q1 2026", text);
        Assert.Contains("2026-10-03", text);
        Assert.Contains("Документ сформирован автоматически", text);
    }

    [Fact]
    public void DeepSeekXml_Xslt1_FoDocument_ContainsPerItemComments()
    {
        var text = RunFoStylesheet().Root!.Value;

        Assert.Contains("Комментарии по позициям:", text);

        foreach (var item in new[] { "Ноутбук Pro 15", "Смартфон X200", "Планшет Tab Mini", "Наушники AirSound" })
        {
            Assert.Contains(item, text);
        }
    }

    [Fact]
    public void DeepSeekXml_Xslt1_ReplacesAltovaEvaluate_WithStandardXPath()
    {
        var doc = RunFoStylesheet();

        // The Altova original did: altova:evaluate('string(//dynamic-expression)'),
        // i.e. evaluate '../title/text()' relative to the dynamic-expression node.
        // The port must reproduce that result with standard XPath 1.0.
        var block = doc.Descendants(FoNs + "block")
                       .SingleOrDefault(b => b.Value.Contains("Результат altova:evaluate()"));

        Assert.NotNull(block);
        Assert.Contains("Отчёт по продажам за 2026 год", block!.Value);
    }

    [Fact]
    public void DeepSeekXml_Xslt1_Execute_WritesFoFileToDisk()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"altovagen-fo-{Guid.NewGuid():N}.fo");

        try
        {
            var xslt = new XSLT1(new EngineRouter())
            {
                InputXMLFileName = FixturePath("deepseek_xml_20261003_b25c49.xml"),
                XSLFileName = FixturePath("sales-report-fo.xslt")
            };

            xslt.Execute(outputPath);

            Assert.Equal(string.Empty, xslt.LastErrorMessage);
            Assert.True(File.Exists(outputPath), $"FO document was not written to {outputPath}");

            var doc = XDocument.Parse(File.ReadAllText(outputPath));
            Assert.Equal(FoNs + "root", doc.Root!.Name);
            Assert.Equal(4, doc.Descendants(FoNs + "table-body").Elements(FoNs + "table-row").Count());
            Assert.Contains("26650000", doc.Root.Value);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void DeepSeekXml_XSLT1_AlsoHandlesTheAltovaExtensionStylesheet()
    {
        // The XSLT 1.0 facade has no version marker of its own, but a stylesheet
        // that declares xmlns:altova="http://www.altova.com/xslt-extensions" is
        // routed to the Saxon engine as well, because the BCL processor cannot
        // resolve Altova extension functions. Legacy callers therefore keep working
        // when they point XSLT1 at an Altova-generated stylesheet.
        var xslt = new XSLT1(new EngineRouter());
        xslt.InputXMLFromText = File.ReadAllText(FixturePath("deepseek_xml_20261003_b25c49.xml"));
        xslt.XSLFromText = File.ReadAllText(FixturePath("deepseek_xml_20261003_4d888a.xslt"));

        var result = xslt.ExecuteAndGetResultAsString();

        Assert.Equal(string.Empty, xslt.LastErrorMessage);
        Assert.Contains("26650000", result);

        var doc = XDocument.Parse(result);
        Assert.Equal(FoNs + "root", doc.Root!.Name);
        Assert.Equal(4, doc.Descendants(FoNs + "table-body").Elements(FoNs + "table-row").Count());
    }

    [Fact]
    public void DeepSeekXml_Xslt1_TotalAmountCalculation()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "deepseek_xml_20261003_b25c49.xml");
        var xsl = new XSLT1(new EngineRouter());

        xsl.InputXMLFromText = File.ReadAllText(xmlPath);
        // XSLT 1.0 has no sequence arithmetic in sum(), so the price*quantity total
        // is accumulated with a recursive named template (valid XPath 1.0 / XSLT 1.0).
        xsl.XSLFromText = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text"/>
              <xsl:template match="/">
                <xsl:call-template name="sumItems">
                  <xsl:with-param name="nodes" select="//items/item"/>
                </xsl:call-template>
              </xsl:template>
              <xsl:template name="sumItems">
                <xsl:param name="nodes"/>
                <xsl:param name="acc" select="0"/>
                <xsl:choose>
                  <xsl:when test="count($nodes) = 0">
                    <xsl:value-of select="$acc"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:call-template name="sumItems">
                      <xsl:with-param name="nodes" select="$nodes[position() &gt; 1]"/>
                      <xsl:with-param name="acc" select="$acc + $nodes[1]/@price * $nodes[1]/quantity"/>
                    </xsl:call-template>
                  </xsl:otherwise>
                </xsl:choose>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = xsl.ExecuteAndGetResultAsString();

        Assert.Equal(string.Empty, xsl.LastErrorMessage);
        // Total: 100000*120 + 25000*350 + 30000*80 + 7000*500 = 12000000 + 8750000 + 2400000 + 3500000 = 26650000
        Assert.Equal("26650000", result.Trim());
    }

    [Fact]
    public void DeepSeekXml_Xslt1_ItemListing()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "deepseek_xml_20261003_b25c49.xml");
        var xsl = new XSLT1(new EngineRouter());

        xsl.InputXMLFromText = File.ReadAllText(xmlPath);
        xsl.XSLFromText = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text"/>
              <xsl:template match="/">
                <xsl:for-each select="//items/item">
                  <xsl:value-of select="@id"/>: <xsl:value-of select="name"/> (qty=<xsl:value-of select="quantity"/>)
                  <xsl:text>&#10;</xsl:text>
                </xsl:for-each>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = xsl.ExecuteAndGetResultAsString();

        Assert.Equal(string.Empty, xsl.LastErrorMessage);
        Assert.Contains("1: Ноутбук Pro 15", result);
        Assert.Contains("2: Смартфон X200", result);
    }
}

public class InvariantTests
{
    [Fact]
    public void NoComInteropAttributes_InCompatAssembly()
    {
        var assembly = typeof(Application).Assembly;

        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetCustomAttributes(), a =>
                a.GetType().Name is "ComImportAttribute" or "ComVisibleAttribute" or "InterfaceTypeAttribute");
            Assert.False(type.IsCOMObject);
        }
    }

    [Fact]
    public void NoComTypeAttributes_InAllProjectAssemblies()
    {
        var assemblies = new[]
        {
            typeof(Application).Assembly,
            typeof(Diagnostic).Assembly,
            typeof(AltovaGen.Engine.Bcl.BclValidatorEngine).Assembly
        };

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                Assert.DoesNotContain(type.GetCustomAttributes(), a =>
                    a.GetType().Name is "ComImportAttribute" or "ComVisibleAttribute" or "InterfaceTypeAttribute");
                Assert.False(type.IsCOMObject);
            }
        }
    }

    [Fact]
    public void Assemblies_AreAnyCpuIlOnly()
    {
        var assemblies = new[]
        {
            typeof(Application).Assembly,
            typeof(Diagnostic).Assembly,
            typeof(AltovaGen.Engine.Bcl.BclValidatorEngine).Assembly
        };

        foreach (var assembly in assemblies)
        {
            // The definitive x86-lock indicator is the COR header flags: a platform-specific
            // (x86/amd64) build sets Requires32Bit / Requires64Bit, whereas a default
            // AnyCPU build is ILOnly. PE COFF Machine = I386 is a historical convention for
            // IL-only managed assemblies and does NOT by itself mean "x86 only".
            using var stream = File.OpenRead(assembly.Location);
            using var pe = new PEReader(stream);
            var cor = pe.PEHeaders.CorHeader
                ?? throw new InvalidOperationException($"No COR header in {assembly.Location}");

            Assert.True(
                cor.Flags.HasFlag(CorFlags.ILOnly),
                $"Assembly {assembly.GetName().Name} is not IL-only (Flags={cor.Flags})");

            // Belt-and-braces: reject the explicit 32-bit locks. The CLR header has no
            // separate "Requires64Bit" bit; a 64-bit-only image is expressed by PE32+
            // plus a platform target, while AnyCPU/IL-only sets neither of these.
            Assert.False(cor.Flags.HasFlag(CorFlags.Requires32Bit), "Assembly requires 32-bit");
            Assert.False(cor.Flags.HasFlag(CorFlags.Prefers32Bit), "Assembly prefers 32-bit");
        }
    }

    [Fact]
    public void CompatSurface_XsltMembers_ArePresent()
    {
        var ixslt1 = typeof(IXSLT1);
        var ixslt2 = typeof(IXSLT2);

        Assert.NotNull(ixslt1.GetProperty("InputXMLFileName"));
        Assert.NotNull(ixslt1.GetProperty("XSLFileName"));
        Assert.NotNull(ixslt1.GetProperty("InputXMLFromText"));
        Assert.NotNull(ixslt1.GetProperty("XSLFromText"));
        Assert.NotNull(ixslt1.GetProperty("XSLStackSize"));
        Assert.NotNull(ixslt1.GetProperty("LastErrorMessage"));
        Assert.NotNull(ixslt1.GetProperty("DotNetExtensionsEnabled"));
        Assert.NotNull(ixslt1.GetProperty("JavaExtensionsEnabled"));
        Assert.NotNull(ixslt1.GetMethod("Execute"));
        Assert.NotNull(ixslt1.GetMethod("ExecuteAndGetResultAsString"));
        Assert.NotNull(ixslt1.GetMethod("AddExternalParameter"));
        Assert.NotNull(ixslt1.GetMethod("ClearExternalParameterList"));
        Assert.NotNull(ixslt2.GetProperty("InitialTemplateName"));
        Assert.NotNull(ixslt2.GetProperty("InitialTemplateMode"));
    }

    [Fact]
    public void CompatSurface_XQueryMembers_ArePresent()
    {
        var ixquery = typeof(IXQuery);

        Assert.NotNull(ixquery.GetProperty("XQueryFileName"));
        Assert.NotNull(ixquery.GetProperty("InputXMLFileName"));
        Assert.NotNull(ixquery.GetProperty("XQueryFromText"));
        Assert.NotNull(ixquery.GetProperty("InputXMLFromText"));
        Assert.NotNull(ixquery.GetProperty("OutputEncoding"));
        Assert.NotNull(ixquery.GetProperty("OutputIndent"));
        Assert.NotNull(ixquery.GetProperty("OutputMethod"));
        Assert.NotNull(ixquery.GetProperty("OutputOmitXMLDeclaration"));
        Assert.NotNull(ixquery.GetProperty("LastErrorMessage"));
        Assert.NotNull(ixquery.GetProperty("DotNetExtensionsEnabled"));
        Assert.NotNull(ixquery.GetProperty("JavaExtensionsEnabled"));
        Assert.NotNull(ixquery.GetMethod("Execute"));
        Assert.NotNull(ixquery.GetMethod("ExecuteAndGetResultAsString"));
        Assert.NotNull(ixquery.GetMethod("AddExternalVariable"));
        Assert.NotNull(ixquery.GetMethod("AddExternalVariableAsXPath"));
        Assert.NotNull(ixquery.GetMethod("ClearExternalVariableList"));
    }

    [Fact]
    public void CompatSurface_ValidatorMembers_ArePresent()
    {
        var ixmlValidator = typeof(IXMLValidator);

        Assert.NotNull(ixmlValidator.GetProperty("InputXMLFileName"));
        Assert.NotNull(ixmlValidator.GetProperty("SchemaFileName"));
        Assert.NotNull(ixmlValidator.GetProperty("DTDFileName"));
        Assert.NotNull(ixmlValidator.GetProperty("InputXMLFromText"));
        Assert.NotNull(ixmlValidator.GetProperty("SchemaFromText"));
        Assert.NotNull(ixmlValidator.GetProperty("DTDFromText"));
        Assert.NotNull(ixmlValidator.GetProperty("LastErrorMessage"));
        Assert.NotNull(ixmlValidator.GetProperty("TreatXBRLInconsistenciesAsErrors"));
        Assert.NotNull(ixmlValidator.GetMethod("IsValid"));
        Assert.NotNull(ixmlValidator.GetMethod("IsWellFormed"));
        Assert.NotNull(ixmlValidator.GetMethod("IsValidWithExternalSchemaOrDTD"));
    }
}
