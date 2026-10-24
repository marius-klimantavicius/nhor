using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Marius.Winter.Tests;

public class SelectionControlsTests
{
    static Element Popup(ComboBox combo) => (Element)typeof(ComboBox)
        .GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;

    [Fact]
    public void ComboKeyboard_OpensNavigatesCancelsAndCommitsOnlyOnce() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 keyboard");
        var combo = new ComboBox(["One", "Two", "Three"]) { Bounds = new RectF(10, 10, 220, 32) };
        window.AddChild(combo); window.SetFocus(combo);
        var changes = new List<int>(); combo.UserSelectionChanged += changes.Add;
        combo.OnKeyDown(257, 0, false);
        var popup = Popup(combo);
        Assert.NotNull(popup);
        combo.OnKeyDown(264, 0, false); combo.OnKeyDown(269, 0, false);
        Assert.Equal(0, combo.SelectedIndex); Assert.Empty(changes);
        combo.OnKeyDown(256, 0, false);
        Assert.Null(Popup(combo)); Assert.Equal(0, combo.SelectedIndex);
        combo.OnKeyDown(32, 0, false); combo.OnKeyDown(269, 0, false); combo.OnKeyDown(257, 0, false);
        Assert.Equal(2, combo.SelectedIndex); Assert.Equal(new[] { 2 }, changes); Assert.Null(Popup(combo));
        combo.OnKeyDown(257, 0, true); Assert.Null(Popup(combo));
    });

    [Fact]
    public void Popup_FirstPointerPressUsesActualRowWithoutPriorMove() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 first pointer");
        var combo = new ComboBox(["One", "Two", "Three"]) { Bounds = new RectF(10, 10, 220, 32) };
        window.AddChild(combo); combo.OnClick();
        var popup = Popup(combo);
        var x = popup.Bounds.X + 12; var y = popup.Bounds.Y + 2 + (combo.Style.FontSize + 8) * 1.5f;
        popup.OnMouseDown(0, x, y); popup.OnMouseUp(0, x, y); popup.OnClick();
        Assert.Equal(1, combo.SelectedIndex);
    });

    [Fact]
    public void Popup_FlipsClampsAndKeyboardRevealsLastRow() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(240, 160, "C02 edge");
        var combo = new ComboBox(Enumerable.Range(1, 32).Select(i => "Environment " + i).ToArray())
        { Bounds = new RectF(140, 124, 180, 32) };
        window.AddChild(combo); combo.OnClick();
        var popup = Popup(combo);
        Assert.InRange(popup.Bounds.X, 0, window.Bounds.W - popup.Bounds.W);
        Assert.InRange(popup.Bounds.Y, 0, window.Bounds.H - popup.Bounds.H);
        Assert.True(popup.Bounds.Bottom <= combo.Bounds.Y, "Popup should flip above an owner near the lower edge.");
        combo.OnKeyDown(269, 0, false);
        // Press the last visible row: scroll-to-highlight must have brought item 32 into view.
        var x = popup.Bounds.X + 12; var y = popup.Bounds.Bottom - 2 - (combo.Style.FontSize + 8) / 2;
        popup.OnMouseDown(0, x, y); popup.OnMouseUp(0, x, y); popup.OnClick();
        Assert.Equal(31, combo.SelectedIndex);
    });

    [Fact]
    public void Tabs_EnterFromTabOrderAndNavigateWithIdentityPreserved() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 tabs");
        var tabs = new TabWidget(); var a = new Panel(); var b = new Panel(); var c = new Panel();
        tabs.AddTab("General", a); tabs.AddTab("Details", b); tabs.AddTab("Advanced", c);
        window.AddChild(tabs);
        Assert.True(tabs.Focusable);
        window.SetFocus(tabs); tabs.OnKeyDown(262, 0, false);
        Assert.Equal(1, tabs.ActiveTab); Assert.True(b.Visible); Assert.False(a.Visible);
        tabs.MoveTab(b, 0); Assert.Equal(0, tabs.ActiveTab); Assert.True(b.Visible);
        tabs.OnKeyDown(269, 0, false); Assert.Equal(2, tabs.ActiveTab);
        tabs.OnKeyDown(268, 0, false); Assert.Equal(0, tabs.ActiveTab);
        tabs.RemoveTab(0); Assert.True(a.Visible); Assert.Equal(0, tabs.ActiveTab);
        tabs.Enabled = false; Assert.Null(window.FocusedElement);
        foreach (var detached in new[] { b }) detached.Dispose();
    });

    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("detach")]
    public void InactiveOwner_CancelsTentativePopupAndReleasesFocusWithoutCommit(string transition) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 activity");
        var combo = new ComboBox(["One", "Two"]) { Bounds = new RectF(10, 10, 220, 32) };
        window.AddChild(combo); window.SetFocus(combo); var changes = 0; combo.UserSelectionChanged += _ => changes++;
        combo.OnKeyDown(257, 0, false); combo.OnKeyDown(269, 0, false);
        var popup = Popup(combo);
        if (transition == "hide") combo.Visible = false;
        else if (transition == "disable") combo.Enabled = false;
        else window.RemoveChild(combo);
        Assert.Null(window.FocusedElement); Assert.Null(Popup(combo)); Assert.True(popup.IsDisposed);
        Assert.Equal(0, combo.SelectedIndex); Assert.Equal(0, changes);
        combo.Dispose();
    });

    [Fact]
    public void Popup_ScrollbarChangesViewportWithoutCommittingAndWheelDoesNotSnapBack() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(320, 240, "C02 scrolling");
        var combo = new ComboBox(Enumerable.Range(0, 40).Select(i => $"Environment {i}").ToArray()) { Bounds = new RectF(10, 10, 260, 32) };
        window.AddChild(combo); combo.OnClick(); var popup = Popup(combo);
        var x = popup.Bounds.Right - 6; var y = popup.Bounds.Bottom - 12;
        popup.OnMouseDown(0, x, y); popup.OnMouseUp(0, x, y); popup.OnClick();
        Assert.True(combo.IsDropDownOpen); Assert.Equal(0, combo.SelectedIndex);
        popup.OnScroll(0, -2); WindowRegressionOwner.Flush(window);
        x = popup.Bounds.X + 12; y = popup.Bounds.Y + 14;
        popup.OnMouseDown(0, x, y); popup.OnMouseUp(0, x, y); popup.OnClick();
        Assert.True(combo.SelectedIndex > 6);
    });
}
