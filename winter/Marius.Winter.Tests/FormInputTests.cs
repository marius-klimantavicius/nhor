using System;
using System.Linq;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class FormInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadOnlyPreservesUnicodeSelectionCopyAndProgrammaticReset(bool multiline) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(360, 160, "Read-only input");
        const string original = "Žąsis e\u0301 👩🏽‍💻";
        Element input = multiline ? new TextArea(original) { ReadOnly = true } : new TextBox(original) { ReadOnly = true };
        window.AddChild(input); input.Arrange(new RectF(0, 0, 340, 90));
        var changes = 0;
        if (input is TextBox box) box.UserTextChanged += _ => changes++;
        else ((TextArea)input).UserTextChanged += _ => changes++;
        input.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        input.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal(original, Glfw.Glfw.glfwGetClipboardString(null));
        input.OnTextInput("replacement");
        Assert.Equal(original, Text(input));
        foreach (var key in new[] { GLFW_KEY_BACKSPACE, GLFW_KEY_DELETE, GLFW_KEY_X, GLFW_KEY_V })
        {
            input.OnKeyDown(key, GLFW_MOD_CONTROL, false);
            Assert.Equal(original, Text(input));
        }
        input.OnKeyDown(GLFW_KEY_LEFT, 0, false);
        input.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_CONTROL, false);
        input.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_SHIFT, false);
        input.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal("Ž", Glfw.Glfw.glfwGetClipboardString(null));
        Assert.Equal(0, changes);
        var menu = new Menu(); input.OnBuildContextMenu(menu);
        Assert.DoesNotContain(menu.Entries, entry => entry.Label is "Cut" or "Paste");
        Assert.Contains(menu.Entries, entry => entry.Label == "Copy");
        if (input is TextBox editableBox) editableBox.ReadOnly = false; else ((TextArea)input).ReadOnly = false;
        var staleMenu = new Menu(); input.OnBuildContextMenu(staleMenu);
        if (input is TextBox readOnlyBox) readOnlyBox.ReadOnly = true; else ((TextArea)input).ReadOnly = true;
        foreach (var action in staleMenu.Entries.Where(entry => entry.Label is "Cut" or "Paste")) action.Action!();
        Assert.Equal(original, Text(input)); Assert.Equal(0, changes);
        if (input is TextBox text) text.ResetText("new value"); else ((TextArea)input).ResetText("new value");
        Assert.Equal("new value", Text(input)); Assert.Equal(0, changes);
    });

    private static string Text(Element input) => input is TextBox text ? text.Text : ((TextArea)input).Text;
}
