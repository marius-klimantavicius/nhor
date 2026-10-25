using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ModelessAncestorOverflowTests
{
    [Theory]
    [InlineData(RenderBackend.SW, "header")]
    [InlineData(RenderBackend.SW, "body")]
    [InlineData(RenderBackend.SW, "grip")]
    [InlineData(RenderBackend.GL, "header")]
    [InlineData(RenderBackend.GL, "body")]
    [InlineData(RenderBackend.GL, "grip")]
    public void PaintedWindowReceivesInputThroughTwoNaturallySizedAncestors(RenderBackend backend, string target) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 400, "Nested modeless input", Theme.Light, backend);
        var outer = NaturalPanel();
        var inner = NaturalPanel();
        inner.AddChild(new Box());
        outer.AddChild(inner);
        window.AddChild(outer);
        var before = outer.Measure(420, 360);
        var dialog = Dialog();
        var body = new Checkbox("Body remains interactive");
        dialog.Layout = new StackLayout();
        dialog.AddChild(body);
        inner.AddChild(dialog);
        Assert.Equal(before, outer.Measure(420, 360));
        outer.Arrange(new RectF(20, 30, 420, before.Y));
        WindowRegressionOwner.Flush(window);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
        Assert.Equal(20, outer.Bounds.H);
        Assert.Equal(20, inner.Bounds.H);
        Assert.Same(inner, dialog.Parent);
        Assert.Same(window, body.OwnerWindow);
        Assert.Same(dialog.Scene, inner.Scene.Paints().Last());
        int top = Shade(window, backend, 94, 135), bottom = Shade(window, backend, 94, 155);
        Assert.True(top > bottom + 5, $"The overflowing header is painted: {top}, {bottom}");
        var (x, y, expected) = target switch
        {
            "body" => (80 + body.Bounds.X + 15, 130 + body.Bounds.Y + 10, (Element)body),
            "grip" => (358f, 298f, dialog),
            _ => (100f, 145f, dialog)
        };
        Assert.Same(expected, window.HitTest(x, y));
    });

    [Fact]
    public void OrdinaryOverflowStaysClippedEvenWhenItsBranchOwnsAModelessWindow()
    {
        using var root = Root();
        var outer = new Panel { Bounds = new RectF(20, 30, 420, 20) };
        var inner = new Panel { Bounds = new RectF(40, 100, 180, 60) };
        var ordinary = new Panel { Bounds = new RectF(0, 0, 180, 60) };
        inner.AddChild(ordinary);
        // Merely having a modeless descendant must not enable ordinary input.
        inner.AddChild(new DialogWindow { Bounds = new RectF(300, 200, 100, 100) });
        outer.AddChild(inner);
        root.AddChild(outer);
        Assert.Same(root, root.HitTest(80, 145));
        Assert.Same(ordinary, inner.HitTest(60, 115));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitViewportRejectsOutsideInputAndAllowsNestedInputInside(bool insideViewport)
    {
        using var root = Root();
        var viewport = new ScrollPanel
        {
            Bounds = new RectF(20, 30, 420, insideViewport ? 360 : 60),
            VerticalScroll = ScrollMode.Never,
            HorizontalScroll = ScrollMode.Never
        };
        var (outer, _, dialog) = Branch(0, 0);
        viewport.AddChild(outer);
        // Put another non-clipping ancestor above the explicit viewport too.
        var wrapper = new Panel { Bounds = new RectF(0, 0, 450, 20) };
        wrapper.AddChild(viewport);
        root.AddChild(wrapper);
        Assert.Same(insideViewport ? dialog : root, root.HitTest(100, 145));
    }

    [Fact]
    public void OverlappingDescendantsFollowTheirOwningScenesOrder()
    {
        using var root = Root();
        var (first, firstInner, firstDialog) = Branch();
        var (second, secondInner, secondDialog) = Branch();
        root.AddChild(first);
        root.AddChild(second);
        void Order(Element front, Element expected)
        {
            Assert.Same(front.Scene, root.Scene.Paints().Last());
            Assert.Same(expected, root.HitTest(100, 145));
        }
        Order(second, secondDialog);
        root.ReorderChildren([second, first]);
        Order(first, firstDialog);
        var third = Dialog();
        firstInner.InsertChild(third, 2);
        Assert.Same(third.Scene, firstInner.Scene.Paints().Last());
        Order(first, third);
        firstInner.ReorderChildren([third, firstInner.Children[0], firstDialog]);
        Assert.Same(firstDialog.Scene, firstInner.Scene.Paints().Last());
        Order(first, firstDialog);
        // A later ordinary root sibling still covers an earlier owned dialog.
        var cover = new Panel { Bounds = new RectF(80, 130, 280, 170) };
        root.AddChild(cover);
        Order(cover, cover);
        cover.Visible = false;
        Assert.Same(firstDialog, root.HitTest(100, 145));
        Assert.Same(firstInner, firstDialog.Parent);
        Assert.Same(secondInner, secondDialog.Parent);
    }

    [Theory]
    [InlineData("dialog")]
    [InlineData("inner")]
    [InlineData("outer")]
    public void HiddenOrDisabledAncestorsDoNotExposeModelessInput(string level)
    {
        using var root = Root();
        var (outer, inner, dialog) = Branch();
        root.AddChild(outer);
        var element = level == "dialog" ? (Element)dialog : level == "inner" ? inner : outer;
        element.Visible = false;
        Assert.Same(root, root.HitTest(100, 145));
        element.Visible = true;
        Assert.Same(dialog, root.HitTest(100, 145));
        element.Enabled = false;
        Assert.Same(root, root.HitTest(100, 145));
        element.Enabled = true;
        Assert.Same(dialog, root.HitTest(100, 145));
    }

    [Fact]
    public void DetachedReparentedClearedAndDisposedSubtreesUpdateOverflowReachability()
    {
        using var root = Root();
        using var other = Root();
        var (outer, inner, dialog) = Branch();
        root.InsertChild(outer, 0); // subtree already contains a dialog
        Assert.Same(dialog, root.HitTest(100, 145));
        root.RemoveChild(outer);
        Assert.Same(root, root.HitTest(100, 145));
        other.AddChild(outer);
        Assert.Same(dialog, other.HitTest(100, 145));
        inner.RemoveChild(dialog);
        Assert.Same(other, other.HitTest(100, 145));
        inner.AddChild(dialog);
        Assert.Same(dialog, other.HitTest(100, 145));
        var ordinary = inner.Children[0];
        inner.ClearChildren();
        ordinary.Dispose();
        Assert.Same(other, other.HitTest(100, 145));
        inner.AddChild(dialog);
        Assert.Same(dialog, other.HitTest(100, 145));
        dialog.Dispose();
        Assert.Null(dialog.Parent);
        Assert.Same(other, other.HitTest(100, 145));
        // ClearChildren detaches reusable ordinary children as well.
        inner.AddChild(new Box());
    }

    [Fact]
    public void OutsideInputDoesNotVisitUnrelatedSubtreesBeforeOrAfterDialogRemoval()
    {
        using var root = Root();
        var outer = new Panel { Bounds = new RectF(20, 30, 420, 20) };
        var branches = Enumerable.Range(0, 32).Select(_ => new HitCounter { Bounds = new RectF(0, 0, 80, 20) }).ToArray();
        foreach (var branch in branches)
        {
            for (int i = 0; i < 8; i++) branch.AddChild(new HitCounter());
            outer.AddChild(branch);
        }
        root.AddChild(outer);
        Assert.Same(root, root.HitTest(100, 145));
        Assert.All(branches, branch => Assert.Equal(0, branch.Calls));
        using var dialog = Dialog();
        branches[0].AddChild(dialog);
        Assert.Same(dialog, root.HitTest(100, 145));
        Assert.All(branches.Skip(1), branch => Assert.Equal(0, branch.Calls));
        branches[0].RemoveChild(dialog);
        foreach (var branch in branches) branch.Calls = 0;
        Assert.Same(root, root.HitTest(100, 145));
        Assert.All(branches, branch => Assert.Equal(0, branch.Calls));
    }

    private static Panel Root() => new() { Bounds = new RectF(0, 0, 500, 400) };
    private static Panel NaturalPanel() => new()
    {
        Padding = new Thickness(0),
        Layout = new StackLayout { Spacing = 0, CrossAlignment = Alignment.Start }
    };
    private static DialogWindow Dialog() => new("Nested modeless") { Bounds = new RectF(60, 100, 280, 170) };
    private static (Panel Outer, Panel Inner, DialogWindow Dialog) Branch(float x = 20, float y = 30)
    {
        var outer = NaturalPanel();
        var inner = NaturalPanel();
        inner.AddChild(new Box());
        var dialog = Dialog();
        inner.AddChild(dialog);
        outer.AddChild(inner);
        var size = outer.Measure(420, 360);
        Assert.Equal(20, size.Y);
        outer.Arrange(new RectF(x, y, 420, size.Y));
        return (outer, inner, dialog);
    }

    private sealed class Box : Element
    {
        protected override Vector2 MeasureCore(float width, float height) => new(80, 20);
    }
    private sealed class HitCounter : Panel
    {
        public int Calls;
        public override Element? HitTest(float x, float y) { Calls++; return base.HitTest(x, y); }
    }

    private static unsafe int Shade(Window window, RenderBackend backend, int x, int y)
    {
        if (backend == RenderBackend.SW)
            return (int)(WindowRuntimeTests.Buffer(window)[y * (int)window.FramebufferWidth + x] & 255);
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var readBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        finish(); bind(0x8CA8, 0); readBuffer(0x0405);
        uint rgba = 0;
        read(x, (int)window.FramebufferHeight - 1 - y, 1, 1, 0x1908, 0x1401, &rgba);
        return (int)(rgba & 255);
    }
}
