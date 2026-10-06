// AltovaGen Compat — XSLT2
using AltovaGen.Abstractions;
using AltovaGen.Models;

namespace AltovaGen.Compat;

/// <summary>
/// Managed, COM-free replacement for XSLT2.
/// Adds initial template name/mode. Requires XSLT 2.0, which is processed by the
/// bundled SaxonCS-HE engine (XSLT 2.0/3.0, including XSLT 2.0-only constructs like
/// xsl:for-each-group and Altova extension functions). On the legacy BCL route only
/// XSLT 1.0 semantics are available.
/// </summary>
internal class XSLT2 : XSLT1, IXSLT2
{
    private string? _initialTemplateName;
    private string? _initialTemplateMode;

    public XSLT2(IEngineRouter router) : base(router) { }

    /// <summary>
    /// Altova XSLT2 targets XSLT 2.0. The router routes version &gt; 1.0
    /// requests to the Saxon engine (XSLT 2.0/3.0), which also provides the
    /// Altova extension-function bridge.
    /// </summary>
    protected override double RequiredXsltVersion => 2.0;

    public string InitialTemplateName { set => _initialTemplateName = value; }
    public string InitialTemplateMode { set => _initialTemplateMode = value; }

    protected override TransformRequest BuildRequest(string? outputPath)
    {
        var request = base.BuildRequest(outputPath);
        return new TransformRequest
        {
            InputXmlPath = request.InputXmlPath,
            XslPath = request.XslPath,
            InputXmlText = request.InputXmlText,
            XslText = request.XslText,
            OutputPath = request.OutputPath,
            ExternalParameters = request.ExternalParameters,
            StackSizeHint = request.StackSizeHint,
            InitialTemplateName = _initialTemplateName,
            InitialTemplateMode = _initialTemplateMode
        };
    }
}