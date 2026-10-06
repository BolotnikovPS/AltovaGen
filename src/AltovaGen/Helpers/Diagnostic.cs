namespace AltovaGen.Helpers;

/// <summary>
/// Represents an error or warning with position information.
/// Mirrors Altova's error reporting style for LastErrorMessage compatibility.
/// </summary>
public readonly record struct Diagnostic(
    string Message,
    int Line = -1,
    int Column = -1,
    string Engine = "BCL",
    string Code = "",
    bool IsWarning = false)
{
    public override string ToString()
    {
        var position = (Line >= 0 && Column >= 0) ? $" at line {Line}, column {Column}" : "";
        var engineInfo = string.IsNullOrEmpty(Engine) ? "" : $" ({Engine})";
        var codeInfo = string.IsNullOrEmpty(Code) ? "" : $" [{Code}]";

        return $"{(Message ?? string.Empty).TrimEnd('.')}{position}{engineInfo}{codeInfo}";
    }

    /// <summary>
    /// True when this diagnostic is an error (not a warning) and carries a message.
    /// </summary>
    public bool IsError => !IsWarning && !string.IsNullOrEmpty(Message);
}
