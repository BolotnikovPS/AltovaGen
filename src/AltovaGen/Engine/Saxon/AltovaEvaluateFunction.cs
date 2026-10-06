using Saxon.Api;

namespace AltovaGen.Engine.Saxon;

/// <summary>
/// Managed, COM-free emulation of the Altova extension function
/// <c>altova:evaluate()</c> from <c>xmlns:altova="http://www.altova.com/xslt-extensions"</c>.
///
/// Altova-generated stylesheets (XMLSpy, StyleVision, MapForce, RaptorXML) call this
/// function to evaluate an XPath expression that is computed at run time, typically
/// read from the source document:
/// <code>
///   &lt;xsl:variable name="expr" select="string(//dynamic-expression)"/&gt;
///   &lt;xsl:value-of select="altova:evaluate($expr)"/&gt;
/// </code>
///
/// Semantics implemented here:
/// <list type="bullet">
///   <item><c>altova:evaluate($expr)</c> — evaluates <c>$expr</c> as an XPath expression
///     with the focus (context item) that is in effect at the call site. This matches
///     Altova's documented behaviour of evaluating relative to the context node, so
///     <c>../title/text()</c> called from a <c>&lt;dynamic-expression&gt;</c> node returns
///     the text of the sibling <c>&lt;title&gt;</c>.</item>
///   <item><c>altova:evaluate($expr, $focus)</c> — evaluates <c>$expr</c> relative to an
///     explicitly supplied node instead of the context item.</item>
/// </list>
///
/// The expression is compiled by the Saxon XPath 3.1 compiler, therefore it may use
/// XPath 2.0/3.0 syntax. The common <c>xs</c>, <c>fn</c>, <c>math</c>, <c>map</c> and
/// <c>array</c> prefixes are pre-declared, and the base URI of the context node is used
/// for relative URI resolution.
///
/// There is no COM, no registry lookup and no Altova runtime: the function is registered
/// on the Saxon processor by <see cref="SaxonXsltEngine"/>.
/// </summary>
internal sealed class AltovaEvaluateFunction : ExtensionFunctionDefinition
{
    /// <summary>The Altova XSLT extension namespace.</summary>
    public const string NamespaceUri = "http://www.altova.com/xslt-extensions";

    /// <summary>The function name inside <see cref="NamespaceUri"/>.</summary>
    public const string LocalName = "evaluate";

    public override QName FunctionName => new(NamespaceUri, LocalName);

    public override int MinimumNumberOfArguments => 1;

    public override int MaximumNumberOfArguments => 2;

    public override XdmSequenceType[] ArgumentTypes =>
    [
        XdmSequenceType.AnySequenceType,
        XdmSequenceType.AnySequenceType
    ];

    /// <summary>The function depends on the focus: the call-site context item is the default context node.</summary>
    public override bool DependsOnFocus => true;

    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes) => XdmSequenceType.AnySequenceType;

    public override ExtensionFunctionCall MakeFunctionCall() => new EvaluateCall();

    private sealed class EvaluateCall : ExtensionFunctionCall
    {
        public override XdmValue Call(XdmValue[] arguments, DynamicContext context)
        {
            if (arguments.Length == 0 || arguments[0].Count == 0)
            {
                return Empty;
            }

            // XdmNode.ToString() returns XML markup, not the node's string value, so a
            // stylesheet passing a node here would evaluate "<b>…</b>" instead of "…".
            // Use StringValue for nodes; fall back to ToString() for atomic values.
            var first = arguments[0].ItemAt(0);
            var expression = first is XdmNode xmlNode ? xmlNode.StringValue : first.ToString();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return Empty;
            }

            // Second argument (optional) overrides the context node explicitly.
            var focus = arguments.Length > 1 && arguments[1].Count > 0
                ? arguments[1].ItemAt(0)
                : context.ContextItem;

            var compiler = context.Processor.NewXPathCompiler();
            DeclareCommonNamespaces(compiler);

            if (focus is XdmNode node && node.BaseUri is not null)
            {
                compiler.BaseUri = node.BaseUri;
            }

            // focus may be null: the expression then runs with no context item, which is
            // correct for context-free expressions such as "1 + 1".
            return compiler.Evaluate(expression, focus!);
        }

        private static void DeclareCommonNamespaces(XPathCompiler compiler)
        {
            compiler.DeclareNamespace("xs", "http://www.w3.org/2001/XMLSchema");
            compiler.DeclareNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            compiler.DeclareNamespace("fn", "http://www.w3.org/2005/xpath-functions");
            compiler.DeclareNamespace("math", "http://www.w3.org/2005/xpath-functions/math");
            compiler.DeclareNamespace("map", "http://www.w3.org/2005/xpath-functions/map");
            compiler.DeclareNamespace("array", "http://www.w3.org/2005/xpath-functions/array");
        }
    }

    private static XdmValue Empty => new(Array.Empty<XdmItem>());
}
