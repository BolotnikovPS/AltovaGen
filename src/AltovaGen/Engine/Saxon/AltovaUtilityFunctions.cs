using Saxon.Api;

namespace AltovaGen.Engine.Saxon;

/// <summary>
/// Managed, COM-free emulation of the Altova XSLT 2.0 extension functions
/// <c>distinct-nodes()</c>, <c>get-temp-folder()</c> and <c>encode-for-rft()</c>,
/// all registered under <c>http://www.altova.com/xslt-extensions</c>.
///
/// These are simple, self-contained helpers that do not need the Altova runtime,
/// an XBRL taxonomy or COM interop, so they can be emulated exactly.
/// </summary>

/// <summary>
/// <c>altova:distinct-nodes($nodes as node()*)</c> — returns the sequence of distinct
/// nodes from <c>$nodes</c>, removing duplicates by node identity while preserving
/// document order. Equivalent to Saxon's <c>distinct-values</c> but for nodes (which
/// compare by identity, not by string value) and returning nodes rather than values.
/// </summary>
internal sealed class AltovaDistinctNodesFunction : ExtensionFunctionDefinition
{
    public const string LocalName = "distinct-nodes";

    private readonly string _namespaceUri;

    public AltovaDistinctNodesFunction(string namespaceUri)
    {
        _namespaceUri = namespaceUri;
    }

    public override QName FunctionName => new(_namespaceUri, LocalName);

    public override int MinimumNumberOfArguments => 1;

    public override int MaximumNumberOfArguments => 1;

    public override XdmSequenceType[] ArgumentTypes => [XdmSequenceType.AnySequenceType];

    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes) => XdmSequenceType.AnySequenceType;

    public override ExtensionFunctionCall MakeFunctionCall() => new DistinctNodesCall();

    private sealed class DistinctNodesCall : ExtensionFunctionCall
    {
        public override XdmValue Call(XdmValue[] arguments, DynamicContext context)
        {
            if (arguments.Length == 0 || arguments[0].Count == 0)
            {
                return Empty;
            }

            // XdmValue.DocumentOrder returns the same nodes with duplicates eliminated
            // (by node identity) and sorted into document order — exactly the
            // distinct-nodes() semantics.
            return arguments[0].DocumentOrder();
        }

        private static XdmValue Empty => new(Array.Empty<XdmItem>());
    }
}

/// <summary>
/// <c>altova:get-temp-folder()</c> — returns the operating-system temporary folder path
/// as an <c>xs:string</c>. Mirrors <see cref="Path.GetTempPath()"/>.
/// </summary>
internal sealed class AltovaGetTempFolderFunction : ExtensionFunctionDefinition
{
    public const string LocalName = "get-temp-folder";

    private readonly string _namespaceUri;

    public AltovaGetTempFolderFunction(string namespaceUri)
    {
        _namespaceUri = namespaceUri;
    }

    public override QName FunctionName => new(_namespaceUri, LocalName);

    public override int MinimumNumberOfArguments => 0;

    public override int MaximumNumberOfArguments => 0;

    public override XdmSequenceType[] ArgumentTypes => [];

    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes) => XdmSequenceType.AnySequenceType;

    public override ExtensionFunctionCall MakeFunctionCall() => new GetTempFolderCall();

    private sealed class GetTempFolderCall : ExtensionFunctionCall
    {
        public override XdmValue Call(XdmValue[] arguments, DynamicContext context)
        {
            return new XdmAtomicValue(Path.GetTempPath());
        }
    }
}

/// <summary>
/// <c>altova:encode-for-rft($value as xs:string?)</c> — encodes a string for embedding in
/// RTF (Rich Text Format) text, matching Altova's RFT escaping used by XBRL rendered text.
///
/// RTF reserves the characters <c>\</c>, <c>{</c> and <c>}</c>, and represents characters
/// outside the ASCII range with the <c>\uN?</c> escape. This implementation applies:
/// <list type="bullet">
///   <item><c>\</c> → <c>\\</c></item>
///   <item><c>{</c> → <c>\{</c></item>
///   <item><c>}</c> → <c>\}</c></item>
///   <item>each code point ≥ 0x80 → <c>\uN?</c> (where N is the decimal code point and
///     <c>?</c> is the ANSI fallback character)</item>
/// </list>
/// </summary>
internal sealed class AltovaEncodeForRftFunction : ExtensionFunctionDefinition
{
    public const string LocalName = "encode-for-rft";

    private readonly string _namespaceUri;

    public AltovaEncodeForRftFunction(string namespaceUri)
    {
        _namespaceUri = namespaceUri;
    }

    public override QName FunctionName => new(_namespaceUri, LocalName);

    public override int MinimumNumberOfArguments => 1;

    public override int MaximumNumberOfArguments => 1;

    public override XdmSequenceType[] ArgumentTypes => [XdmSequenceType.AnySequenceType];

    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes) => XdmSequenceType.AnySequenceType;

    public override ExtensionFunctionCall MakeFunctionCall() => new EncodeForRftCall();

    private sealed class EncodeForRftCall : ExtensionFunctionCall
    {
        public override XdmValue Call(XdmValue[] arguments, DynamicContext context)
        {
            if (arguments.Length == 0 || arguments[0].Count == 0)
            {
                return new XdmAtomicValue(string.Empty);
            }

            var value = arguments[0].ItemAt(0).StringValue;
            var builder = new System.Text.StringBuilder(value.Length);

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    case '{':
                        builder.Append(@"\{");
                        break;
                    case '}':
                        builder.Append(@"\}");
                        break;
                    default:
                        if (ch > 0x7F)
                        {
                            builder.Append(@"\u").Append((int)ch).Append('?');
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }

            return new XdmAtomicValue(builder.ToString());
        }
    }
}
