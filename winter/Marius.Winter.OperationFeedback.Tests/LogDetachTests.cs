using System;
using System.Reflection;
using ThorVG;
using Xunit;
using static Marius.Winter.OperationFeedback.Tests.FeedbackTests;

namespace Marius.Winter.OperationFeedback.Tests;

public class LogDetachTests
{
    [Theory]
    [InlineData("remove")]
    [InlineData("clear-parent")]
    [InlineData("close")]
    public void RenderedNonemptyLogAppendCanDetachBeforeNextFrame(string transition)
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 320, "C08 rendered append detach", Theme.Light, RenderBackend.SW);
            using var parent = new Panel();
            var buffer = new LogBuffer();
            buffer.AppendLine("Existing output");
            using var log = new LogView { Buffer = buffer, FollowTail = false };
            parent.AddChild(log);
            window.AddChild(parent);
            log.Arrange(new RectF(0, 0, 480, 280));
            log.OnKeyDown(Glfw.GLFW.GLFW_KEY_A, Glfw.GLFW.GLFW_MOD_CONTROL, false);
            string selected = log.SelectedText;
            Assert.NotEmpty(selected);
            var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Render(canvas);
            buffer.AppendLine("Preparing preview pack");
            // No canvas update between the append and the ordinary removal.
            var removal = Record.Exception(() =>
            {
                if (transition == "remove") parent.RemoveChild(log);
                else if (transition == "clear-parent") parent.ClearChildren();
                else window.Dispose();
            });
            Console.WriteLine($"append -> {transition}: {removal?.ToString() ?? "ok"}");
            Assert.Null(removal);
            Assert.Equal("Existing output\nPreparing preview pack\n", buffer.Snapshot.Text);
            Assert.Equal(selected, log.SelectedText);
            Assert.False(log.FollowTail);
            if (transition != "close")
            {
                buffer.AppendLine("Detached producer continues");
                Assert.DoesNotContain("Detached producer", log.Text);
                parent.AddChild(log);
                log.Arrange(new RectF(0, 0, 480, 280));
                Assert.Equal(buffer.Snapshot.Text, log.Text);
                Assert.Equal(selected, log.SelectedText);
                Render(canvas);
                // Repeated attachment must rebuild retained glyphs as well as data.
                buffer.AppendLine("Another ordinary append");
                parent.RemoveChild(log);
                parent.AddChild(log);
                Render(canvas);
            }
        });
    }

    private static void Render(Canvas canvas)
    {
        Assert.Equal(Result.Success, canvas.Update());
        Assert.Equal(Result.Success, canvas.Draw(true));
        Assert.Equal(Result.Success, canvas.Sync());
    }
}
