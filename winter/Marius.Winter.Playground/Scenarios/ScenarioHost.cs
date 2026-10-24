using System;
using System.Collections.Generic;
using Marius.Winter;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Compact scenario registration; modules retain ownership of their content and work.</summary>
public sealed class ScenarioHost : IDisposable
{
    private readonly Window _window;
    private readonly ComboBox _navigation = new();
    private readonly Panel _pages = new() { Layout = new GridLayout { Columns = [TrackSize.Fr()], Rows = [TrackSize.Fr()] } };
    private readonly List<string> _names = [];
    private bool _large;
    public Element Content { get; }

    public ScenarioHost(Window window)
    {
        _window = window;
        var root = new Panel { Padding = new Thickness(12), Layout = new GridLayout { Columns = [TrackSize.Fr()], Rows = [TrackSize.Auto(), TrackSize.Fr()], RowGap = 12 } };
        var toolbar = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 10 } };
        _navigation.MinWidth = 240;
        _navigation.UserSelectionChanged += Select;
        toolbar.AddChild(_navigation);
        toolbar.AddChild(new Button("Toggle theme") { Clicked = () => ApplyAppearance(toggleTheme: true) });
        toolbar.AddChild(new Button("Larger text") { Clicked = () => { _large = !_large; ApplyAppearance(); } });
        root.AddChild(toolbar); root.AddChild(_pages); Content = root;
    }

    public void AddScenario(string name, Element content)
    {
        _names.Add(name); content.LayoutData = new GridItem();
        content.Visible = _pages.Children.Count == 0; _pages.AddChild(content);
        _navigation.SetItemsAndIndex(_names.ToArray(), 0);
    }

    private void Select(int index)
    {
        for (int i = 0; i < _pages.Children.Count; i++) _pages.Children[i].Visible = i == index;
    }

    private void ApplyAppearance(bool toggleTheme = false)
    {
        bool dark = _window.Theme.WindowBackground.R < .5f;
        if (toggleTheme) dark = !dark;
        var theme = dark ? Theme.Dark : Theme.Light;
        foreach (var style in new[] { theme.Button, theme.TextBox, theme.Checkbox, theme.Slider, theme.Label, theme.ProgressBar, theme.Tooltip, theme.MenuBar })
            style.FontSize *= _large ? 1.3f : 1;
        _window.Theme = theme;
    }

    public void Dispose()
    {
        _navigation.UserSelectionChanged -= Select;
        foreach (var child in new List<Element>(_pages.Children)) _pages.RemoveChild(child);
        Content.Dispose();
    }
}
