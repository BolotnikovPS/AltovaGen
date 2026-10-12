using System.Xml;

namespace AltovaGen.Helpers;

/// <summary>
/// Reader settings for XML input (matches Altova's Unicode string handling).
/// </summary>
public static class XmlReaderSettingsHelper
{
    /// <summary>
    /// Creates XmlReaderSettings that treat text as Unicode (matches Altova behavior).
    /// Altova interprets text strings as Unicode, ignoring xml declaration encoding.
    /// </summary>
    public static XmlReaderSettings CreateForStringText()
    {
        return new XmlReaderSettings
        {
            // Ignore encoding declaration in xml declaration when from string
            // (Altova treats strings as Unicode regardless of encoding attribute)
            CloseInput = false
        };
    }
}
