using System;
using System.Linq;
using Marius.Winter;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Environment choice and settings navigation, shared in behavior with the Razor scenario.</summary>
public sealed class EnvironmentAndTabsScenario : IDisposable
{
    public record EnvironmentOption(string Id, string Name, string Description);
    private static EnvironmentOption[] Defaults() => new[]
    {
        new EnvironmentOption("local", "Local workspace", "Work offline with your local project files."),
        new("development", "Development", "Try configuration changes before sharing them."),
        new("preview-eu", "Preview · Europe", "Review the next release with the European team."),
        new("preview-us", "Preview · Americas", "Review the next release with the Americas team."),
        new("production-eu", "Production", "Serve customers in Europe. Identity: production-eu."),
        new("production-us", "Production", "Serve customers in the Americas. Identity: production-us.")
    }.Concat(Enumerable.Range(1, 18).Select(i => new EnvironmentOption($"review-{i:00}", $"Review workspace {i:00}", $"Isolated review workspace {i:00} for release approval."))).ToArray();

    private readonly Window _window;
    private readonly Select<EnvironmentOption, string> _environment = new();
    private readonly TabWidget _settings = new() { MinHeight = 140 };
    private readonly Label _description = Wrap("");
    private readonly Label _result = Wrap("");
    private EnvironmentOption[] _options = Defaults();
    private int _changes;
    private bool _disposed;
    public Element Content { get; }

    public EnvironmentAndTabsScenario(Window window)
    {
        _window = window;
        var root = new ScrollPanel { Layout = Column(), Padding = new Thickness(8), HorizontalScroll = ScrollMode.Never };
        Content = root;
        root.AddChild(new Label("Environment and tabs") { FontSize = 22 });
        root.AddChild(Wrap("Choose where to work, then review its settings. Tab reaches the chooser and tab headers; arrows explore, Enter applies, Escape cancels."));
        root.AddChild(new Label("Environment")); root.AddChild(_environment); root.AddChild(_description);
        var choices = Row(); root.AddChild(choices);
        Add(choices, "Rename environment", RenameEnvironment); Add(choices, "Reorder environments", ReorderEnvironments);
        Add(choices, "Remove environment", RemoveEnvironment); Add(choices, "Clear selection", Clear);
        root.AddChild(_settings);
        var actions = Row(); root.AddChild(actions);
        Add(actions, "Go to details", GoToDetails); Add(actions, "Rename tab", RenameTab); Add(actions, "Reorder tabs", ReorderTabs);
        Add(actions, "Remove tab", RemoveTab); Add(actions, "Disable tab", DisableTab); Add(actions, "Hide tab", HideTab);
        Add(actions, "Reset", Reset); root.AddChild(_result);
        _environment.UserValueChanged += Selected; _settings.UserActiveTabChanged += TabSelected;
        Reset();
    }

    private static FlexLayout Column() => new() { Direction = Orientation.Vertical, Gap = 8, AlignItems = Alignment.Stretch };
    private static Panel Row() => new() { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 8 } };
    private static Label Wrap(string text) => new(text) { TextWrapping = ThorVG.TextWrap.Word };
    private static void Add(Panel row, string label, Action action) => row.AddChild(new Button(label) { Clicked = action });
    private void Selected(string _) { _changes++; Refresh("Environment applied"); }
    private void TabSelected(int _) => Refresh("Settings page changed");

    private void Refresh(string message)
    {
        _description.Text = _environment.SelectedOption?.Description ?? "No matching environment selected. Choose an environment or Reset.";
        var page = _settings.ActiveContent?.Children.OfType<Label>().FirstOrDefault()?.Text ?? "none";
        _result.Text = $"{message}. Selected: {_environment.Value ?? "none"} · Settings: {page} · Applied choices: {_changes}";
    }

    private void ApplyOptions() => _environment.Configure(_options, o => o.Name, o => o.Id, _environment.Value);
    private void RenameEnvironment()
    {
        _options = _options.Select(o => o.Id == _environment.Value ? o with { Name = o.Name + " · Žąsis e\u0301" } : o).ToArray();
        ApplyOptions(); Refresh("Environment renamed");
    }
    private void ReorderEnvironments() { _options = _options.AsEnumerable().Reverse().ToArray(); ApplyOptions(); Refresh("Environment order reversed"); }
    private void RemoveEnvironment() { _options = _options.Where(o => o.Id != _environment.Value).ToArray(); ApplyOptions(); Refresh("Environment removed"); }
    private void Clear() { _environment.ClearSelection(); _settings.ActiveTab = -1; Refresh("Selection cleared"); }
    private void GoToDetails()
    {
        var details = _settings.Children.FirstOrDefault(c => c.Children.OfType<Label>().Any(l => l.Text == "Details"));
        if (details != null) _settings.ActiveTab = _settings.Children.ToList().IndexOf(details);
        _window.SetFocus(_settings); Refresh("Details requested");
    }
    private void RenameTab() { if (_settings.ActiveContent is { } active) _settings.UpdateTab(active, "Review settings"); Refresh("Tab renamed"); }
    private void ReorderTabs() { if (_settings.Children.Count > 1) _settings.MoveTab(_settings.Children[^1], 0); Refresh("Tab order rotated"); }
    private void RemoveTab()
    {
        if (_settings.ActiveContent is { } active) { _settings.RemoveTab(_settings.ActiveTab); active.Dispose(); }
        Refresh("Tab removed");
    }
    private void DisableTab() { if (_settings.ActiveContent is { } active) _settings.SetTabEnabled(active, false); Refresh("Tab disabled"); }
    private void HideTab() { if (_settings.ActiveContent is { } active) _settings.SetTabVisible(active, false); Refresh("Tab hidden"); }
    private void Reset()
    {
        _options = Defaults(); _environment.Configure(_options, o => o.Name, o => o.Id, "local"); _changes = 0;
        while (_settings.TabCount > 0) { var content = _settings.Children[0]; _settings.RemoveTab(0); content.Dispose(); }
        foreach (var page in new[] { "General", "Details", "Access" })
        {
            var panel = new Panel { Layout = Column(), Padding = new Thickness(8) };
            panel.AddChild(Wrap(page)); panel.AddChild(Wrap(page switch
            {
                "General" => "Use the selected environment for your next project session.",
                "Details" => "Review the environment description and release workspace before applying changes.",
                _ => "Access follows your workspace membership. No credentials are changed in this demo."
            }));
            _settings.AddTab(page, panel);
        }
        _settings.ActiveTab = 0; Refresh("Ready");
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _environment.UserValueChanged -= Selected; _settings.UserActiveTabChanged -= TabSelected;
        Content.Dispose();
    }
}
