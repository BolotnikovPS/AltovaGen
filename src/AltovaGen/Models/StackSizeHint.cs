namespace AltovaGen.Models;

/// <summary>
/// Stack size hint for transformer execution.
/// </summary>
public enum StackSizeHint
{
    Default = 1024 * 1024,      // 1 MB
    Large = 4 * 1024 * 1024,    // 4 MB
    ExtraLarge = 16 * 1024 * 1024 // 16 MB
}
