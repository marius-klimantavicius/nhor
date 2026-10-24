# Button text and appearance

Native `Button.Text` uses the current effective `Style.FontName` and logical
`Style.FontSize` for both its label and shadow. Joining a Window after detached
construction, changing the theme, and mutating the observed style refresh the
retained glyph paths and measurement together. Repeated text-size changes use
the current font for placement as well as painting.

Buttons with children retain their existing composition: the child content is
laid out inside the padding and replaces the visible text label. Theme updates
preserve click handlers, focus and scene ownership. Both playground hosts expose
theme and text-size controls above their scenario selector.
