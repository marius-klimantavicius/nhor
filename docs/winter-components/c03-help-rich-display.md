# Help and rich display

All Winter Blazor elements accept `string? Tooltip` and
`Marius.Winter.TooltipContent? TooltipContent`. Non-null `TooltipContent` wins,
including an empty content record. Set it to null to use `Tooltip`; set both
to null to clear help. Rich content uses the existing object transport.

```razor
<Button Text="Favorite" Tooltip="Favorite help (plain)" TooltipContent="@help" />
<RichLabel Text="Favorite" FontSize="@textSize" Color="@textColor" />
<SvgImage Svg="@star" RequestedWidth="@iconSize" RequestedHeight="@iconSize" />

@code {
    private const string star = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><path fill='#f5b400' d='M12 1l3 7 8 1-6 5 2 9-7-4-7 4 2-9-6-5 8-1z'/></svg>";
    private TooltipContent? help = new("Star icon", star) { TrailingSvg = star };
    private float? textSize = 24;
    private Color4? textColor = new Color4(.1f, .4f, .65f, 1);
    private float? iconSize = 48;
}
```

`TooltipContent` has optional `Text`, leading `Svg`, and `TrailingSvg`. Native
callers continue to assign a string or `TooltipContent` to `Element.Tooltip`.
Help is noninteractive and appears on pointer hover. Replacing rich content or
switching to the string fallback refreshes that owner's visible help immediately,
even while the pointer stays still. Clearing dismisses visible and pending help;
restoring help under the same pointer starts the usual hover delay. Removing
the hovered owner also cancels its help. Other overlays are unaffected.
Focus-triggered help and interactive popovers are outside this component.

`RichLabel.FontSize` and `Color` are nullable wrapper overrides. Clearing them
restores theme sizing/color; explicit overrides survive attachment and theme
changes. Text color alpha is rendered, including explicit zero (transparent),
and composes with element `Opacity`. It does not change RichLabel's SVG mode.
Native `RichLabel.ResetFontSize()` and `ResetColor()` resume theme
defaults. RichLabel displays text or SVG; it is not a rich-text editor.

`SvgImage.RequestedWidth` and `RequestedHeight` are independent nullable logical
dimensions. Null or zero restores that intrinsic SVG dimension. The native
image scales to fit its arranged bounds while preserving aspect ratio. Dimension
updates repaint in place without requiring a text, position or SVG change. Clearing
`Svg` removes the image; clearing dimensions alone retains it.

Both playgrounds provide **Help and rich display** scenario modules with the
same buttons, Unicode text, help variants, replacement/fallback/clear, owner
removal, independent icon sizing/width and height clears/reset, text opacity
(opaque, half, transparent, theme), visible results, and Reset. The native module
owns only its Content subtree and its temporarily detached Favorite button. The
host owns the Window and registers the module in its navigation.
The module releases its detached Favorite during `Window.Closing`, before the
graphics runtime closes; explicit disposal is also supported and idempotent.
Both actual hosts register this page beside Profile and volume, Resizable
workspace, Operation feedback and Existing demos in their compact selector.
