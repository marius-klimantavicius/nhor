using System;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Noninteractive help and readable rich display, hosted in the existing Window.</summary>
public sealed class HelpRichDisplayScenario : IDisposable
{
    private const string Star = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><path fill='#f5b400' d='M12 1l3 7 8 1-6 5 2 9-7-4-7 4 2-9-6-5 8-1z'/></svg>";
    private readonly Window _window;
    private readonly Panel _helpRow;
    private readonly Button _favorite;
    private readonly RichLabel _name;
    private readonly SvgImage _icon;
    private readonly Label _result;
    private bool _disposed;

    public HelpRichDisplayScenario(Window window)
    {
        _window = window;
        var content = new ScrollPanel
        {
            Padding = new Thickness(12),
            Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start }
        };
        Content = content;
        content.AddChild(new Label("Help and rich display") { FontSize = 22 });
        content.AddChild(new Label("Hover a help button. Change help or resize the preview."));
        _helpRow = Row();
        _helpRow.AddChild(new Button("About favorites") { Tooltip = "Keep useful items in Favorites." });
        _helpRow.AddChild(new Button("Preview star") { Tooltip = new TooltipContent(Svg: Star) });
        _favorite = new Button("Favorite") { Tooltip = FavoriteHelp() };
        _helpRow.AddChild(_favorite);
        content.AddChild(_helpRow);
        var helpActions = Row();
        AddAction(helpActions, "Replace help", () =>
        {
            _favorite.Tooltip = new TooltipContent("Saved · Žvaigždė", Star) { TrailingSvg = Star };
            ShowResult("Favorite help: Saved · Žvaigždė, with two stars.");
        });
        AddAction(helpActions, "Plain fallback", () =>
        {
            _favorite.Tooltip = "Favorite help (plain)";
            ShowResult("Favorite help: plain text fallback.");
        });
        AddAction(helpActions, "Clear help", () =>
        {
            _favorite.Tooltip = null;
            ShowResult("Favorite help cleared, including any help already visible.");
        });
        content.AddChild(helpActions);
        var ownerActions = Row();
        AddAction(ownerActions, "Remove favorite", () =>
        {
            _helpRow.RemoveChild(_favorite);
            ShowResult("Favorite removed. Its help is gone too.");
        });
        AddAction(ownerActions, "Reset", Reset);
        content.AddChild(ownerActions);
        content.AddChild(new Separator());
        _name = new RichLabel();
        _icon = new SvgImage();
        content.AddChild(_name);
        content.AddChild(_icon);
        var displayActions = Row();
        AddAction(displayActions, "Larger preview", () =>
        {
            SetDisplay(28, new Color4(.65f, .24f, .08f, 1), 64);
            ShowResult("Preview: 28-unit text and 64-unit icon.");
        });
        AddAction(displayActions, "Theme sizes", () =>
        {
            SetDisplay(_window.Theme.Label.FontSize, _window.Theme.TextColor, 0);
            _name.ResetFontSize();
            _name.ResetColor();
            ShowResult("Preview: theme text/color and intrinsic 24-unit icon.");
        });
        AddAction(displayActions, "Clear display", () =>
        {
            _name.Text = "";
            _icon.Svg = null;
            ShowResult("Preview cleared. Reset restores text and icon.");
        });
        content.AddChild(displayActions);
        var iconActions = Row();
        AddAction(iconActions, "Larger icon", () =>
        {
            _icon.RequestedWidth = _icon.RequestedHeight = 96;
            ShowResult("Icon: 96 by 96 units. Text size stays the same.");
        });
        AddAction(iconActions, "Clear icon width", () =>
        {
            _icon.RequestedWidth = 0;
            ShowResult("Icon width: intrinsic 24 units. Height stays the same.");
        });
        AddAction(iconActions, "Clear icon height", () =>
        {
            _icon.RequestedHeight = 0;
            ShowResult("Icon height: intrinsic 24 units. Width stays the same.");
        });
        AddAction(iconActions, "Reset icon", () =>
        {
            _icon.Svg = Star;
            _icon.RequestedWidth = _icon.RequestedHeight = 32;
            ShowResult("Icon reset to 32 units. Text stays the same.");
        });
        content.AddChild(iconActions);
        var opacityActions = Row();
        AddAction(opacityActions, "Opaque text", () => SetTextAlpha(1, "Text: fully opaque."));
        AddAction(opacityActions, "Half text", () => SetTextAlpha(.5f, "Text: half opacity."));
        AddAction(opacityActions, "Hide text", () => SetTextAlpha(0, "Text: transparent; its space and icon stay visible."));
        AddAction(opacityActions, "Theme color", () =>
        {
            _name.ResetColor();
            ShowResult("Text color and opacity follow the theme.");
        });
        content.AddChild(opacityActions);
        _result = new Label();
        content.AddChild(_result);
        Reset();
        _window.Closing += Closing;
    }

    public Element Content { get; }

    private static TooltipContent FavoriteHelp() => new("Star icon", Star) { TrailingSvg = Star };
    private static Panel Row() => new() { Layout = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, CrossAlignment = Alignment.Center } };
    private static void AddAction(Panel row, string text, Action action) => row.AddChild(new Button(text) { Clicked = action });
    private void ShowResult(string text) => _result.Text = text;
    private void SetTextAlpha(float alpha, string result)
    {
        _name.Color = new Color4(.1f, .4f, .65f, alpha);
        ShowResult(result);
    }
    private void SetDisplay(float fontSize, Color4 color, float size)
    {
        _name.Text = "Favorite · Žvaigždė · Café";
        _name.FontSize = fontSize;
        _name.Color = color;
        _icon.Svg = Star;
        _icon.RequestedWidth = _icon.RequestedHeight = size;
    }
    private void Reset()
    {
        if (_favorite.Parent == null) _helpRow.AddChild(_favorite);
        _favorite.Tooltip = FavoriteHelp();
        SetDisplay(18, new Color4(.1f, .4f, .65f, 1), 32);
        ShowResult("Ready: Star icon help, 18-unit text and 32-unit preview.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Closing -= Closing;
        if (_favorite.Parent == null) _favorite.Dispose();
        Content.Dispose();
    }

    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
}
