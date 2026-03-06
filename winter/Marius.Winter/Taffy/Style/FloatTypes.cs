// Port of taffy/src/style/float.rs

namespace Marius.Winter.Taffy;

/// <summary>
/// Floats a box to the left or right.
/// This property only applies to children of a block layout.
/// </summary>
public enum Float
{
    /// <summary>The box is floated to the left</summary>
    Left,

    /// <summary>The box is floated to the right</summary>
    Right,

    /// <summary>The box is not floated</summary>
    None,
}

/// <summary>
/// Extension methods for <see cref="Float"/>.
/// </summary>
public static class FloatExtensions
{
    /// <summary>Whether the box is floated.</summary>
    public static bool IsFloated(this Float self)
    {
        return self == Float.Left || self == Float.Right;
    }

    /// <summary>Converts Float into a nullable FloatDirection.</summary>
    public static FloatDirection? GetFloatDirection(this Float self)
    {
        return self switch
        {
            Float.Left => FloatDirection.Left,
            Float.Right => FloatDirection.Right,
            Float.None => null,
            _ => null,
        };
    }
}

/// <summary>
/// Whether a box that is definitely floated is floated to the left or to the right.
/// This type is only used in the low-level parts of the layout algorithm.
/// </summary>
public enum FloatDirection : byte
{
    /// <summary>The box is floated to the left</summary>
    Left = 0,

    /// <summary>The box is floated to the right</summary>
    Right = 1,
}

/// <summary>
/// Gives a box "clearance", which moves it below floated boxes which precede
/// it in the tree.
/// </summary>
public enum Clear
{
    /// <summary>The box clears left-floated boxes</summary>
    Left,

    /// <summary>The box clears right-floated boxes</summary>
    Right,

    /// <summary>The box clears boxes floated in either direction</summary>
    Both,

    /// <summary>The box does not clear floated boxes</summary>
    None,
}
