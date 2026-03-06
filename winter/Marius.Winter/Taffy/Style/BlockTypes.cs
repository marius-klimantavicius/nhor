// Port of taffy/src/style/block.rs

namespace Marius.Winter.Taffy;

/// <summary>
/// Used by block layout to implement the legacy behaviour of &lt;center&gt; and
/// &lt;div align="left | right | center"&gt;.
/// </summary>
public enum TextAlign
{
    /// <summary>No special legacy text align behaviour.</summary>
    Auto = 0,

    /// <summary>Corresponds to -webkit-left or -moz-left in browsers</summary>
    LegacyLeft,

    /// <summary>Corresponds to -webkit-right or -moz-right in browsers</summary>
    LegacyRight,

    /// <summary>Corresponds to -webkit-center or -moz-center in browsers</summary>
    LegacyCenter,
}
