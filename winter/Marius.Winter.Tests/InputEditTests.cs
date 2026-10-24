using System.Collections.Generic;
using Xunit;

namespace Marius.Winter.Tests;

public class InputEditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UserEditsAndCommits_AreSeparateFromLegacySynchronization(bool multiline)
    {
        Element input = multiline ? new TextArea("Milda") : new TextBox("Milda");
        using (input)
        {
            var legacy = new List<string>(); var edits = new List<string>(); var commits = new List<TextCommitEventArgs>();
            void Set(string text) { if (input is TextBox box) box.Text = text; else ((TextArea)input).Text = text; }
            if (input is TextBox box) { box.TextChanged = legacy.Add; box.UserTextChanged += edits.Add; box.TextCommitted += commits.Add; }
            else { var area = (TextArea)input; area.TextChanged = legacy.Add; area.UserTextChanged += edits.Add; area.TextCommitted += commits.Add; }
            Set("Programmatic"); input.OnFocus(); input.OnBlur();
            Assert.Equal(new[] { "Programmatic" }, legacy); Assert.Empty(edits); Assert.Empty(commits);
            input.OnKeyDown(269, 2, false); input.OnTextInput("😀e\u0301");
            input.OnKeyDown(257, multiline ? 2 : 0, false); input.OnKeyDown(257, multiline ? 2 : 0, true); input.OnBlur();
            Assert.Equal(new[] { "Programmatic😀e\u0301" }, edits);
            var commit = Assert.Single(commits); Assert.Equal(edits[0], commit.Text); Assert.Equal(TextCommitReason.Enter, commit.Reason);
            Set("Reset"); input.OnBlur(); Assert.Single(commits);
            input.OnKeyDown(259, 0, false); input.OnBlur();
            Assert.Equal(2, commits.Count); Assert.Equal(TextCommitReason.Blur, commits[1].Reason);
        }
    }

    [Fact]
    public void NativeScalarLegacyEventsRemainCompatible_AndUserEventsAreExplicit()
    {
        using var checkbox = new Checkbox(); using var slider = new Slider(); using var combo = new ComboBox(["Personal", "Studio"]);
        var legacy = 0; var edits = 0;
        checkbox.Changed = _ => legacy++; checkbox.UserChanged += _ => edits++;
        slider.ValueChanged = _ => legacy++; slider.UserValueChanged += _ => edits++;
        combo.SelectionChanged = _ => legacy++; combo.UserSelectionChanged += _ => edits++;
        checkbox.IsChecked = true; slider.Value = .5f; combo.SelectedIndex = 1;
        Assert.Equal(3, legacy); Assert.Equal(0, edits);
        checkbox.OnClick(); slider.SetValueFromUser(.75f); combo.SelectFromUser(0);
        Assert.Equal(6, legacy); Assert.Equal(3, edits);
        slider.SetValueFromUser(.75f); combo.SelectFromUser(0);
        Assert.Equal(6, legacy); Assert.Equal(3, edits);
        slider.Enabled = false; combo.Enabled = false; checkbox.Enabled = false;
        checkbox.OnClick(); slider.OnKeyDown(262, 0, false); combo.SelectFromUser(1);
        Assert.Equal(6, legacy); Assert.Equal(3, edits);
    }
}
