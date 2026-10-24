using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marius.Winter.Blazor;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalFirstFrameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProfileModulesPaintTitleCurrentTextAndActionsInFirstUnscrolledFrame(bool razor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 620, "C06 first modal frame", Theme.Light, RenderBackend.SW);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.WindowsAndDialogs>().IsCompletedSuccessfully);
        else
        {
            var scenario = new Playground.Scenarios.WindowsAndDialogsScenario(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
        }
        WindowRegressionOwner.Flush(window);
        ModalScenarioInputTests.Walk(window).OfType<Button>().Single(b => b.Text == "Edit profile").OnClick();
        WindowRegressionOwner.Flush(window);
        var modal = ModalScenarioInputTests.Walk(window).OfType<ModalDialog>().Single();
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
        var pixels = WindowRuntimeTests.Buffer(window);
        int Ink(RectF rect)
        {
            int count = 0;
            for (int y = (int)rect.Y + 4; y < Math.Min(620, rect.Bottom - 4); y++)
                for (int x = (int)rect.X + 5; x < Math.Min(800, rect.Right - 5); x++)
                { var c = pixels[y * 800 + x]; if ((c & 255) < 110 && ((c >> 8) & 255) < 110 && ((c >> 16) & 255) < 110) count++; }
            return count;
        }
        var editor = ModalScenarioInputTests.Walk(modal.Body!).OfType<TextBox>().Single();
        var save = ModalScenarioInputTests.Walk(modal.Footer!).OfType<Button>().Single(b => b.Text == "Save");
        Assert.Equal("Marius", editor.Text);
        Assert.True(Ink(ModalScenarioInputTests.Absolute(editor)) > 60, "Current text and caret must paint in the initial editor frame.");
        Assert.True(Ink(ModalScenarioInputTests.Absolute(save)) > 25, "Save action text must paint in the initial footer frame.");
        Assert.True(Ink(new RectF(modal.DialogBounds.X + 12, modal.DialogBounds.Y + 12, 180, 36)) > 60, "The title must paint in the first modal frame.");
        var viewport = editor.Parent!; while (viewport is not ScrollPanel) viewport = viewport.Parent!;
        Assert.Equal(0, ((ScrollPanel)viewport).Scroll);
    });
}
