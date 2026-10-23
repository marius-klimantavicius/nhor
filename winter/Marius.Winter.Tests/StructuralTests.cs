using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Marius.Winter.Taffy;
using ThorVG;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Marius.Winter.Tests;

internal static class Contracts
{
    internal static TaffyTree<Element> Tree(TaffyLayoutEngine engine) =>
        (TaffyTree<Element>)typeof(TaffyLayoutEngine).GetField("_tree", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;

    internal static void Synchronize(TaffyLayoutEngine engine, Element root) =>
        typeof(TaffyLayoutEngine).GetMethod("SyncChildren", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, new object[] { root });

    internal static void Dispose(Element element) => Assert.IsAssignableFrom<IDisposable>(element).Dispose();

    internal static void Draw(Canvas canvas, bool clear = false)
    {
        Assert.Equal(Result.Success, canvas.Update());
        Assert.Equal(Result.Success, canvas.Draw(clear));
        Assert.Equal(Result.Success, canvas.Sync());
    }
}

internal class Box : Element
{
    internal int Attachments, Detachments, Measurements;
    internal Window? DetachingWindow;
    internal readonly Shape Paint;

    internal Box(byte red = 255, byte blue = 0)
    {
        Paint = Shape.Gen();
        Paint.AppendRect(0, 0, 24, 24);
        Paint.SetFill(red, 0, blue, 128);
        AddPaint(Paint);
    }

    protected override void OnAttached() => Attachments++;
    protected override void OnDetaching() { Detachments++; DetachingWindow = OwnerWindow; }
    protected override Vector2 MeasureCore(float w, float h) { Measurements++; return new Vector2(20, 10); }
}

public class SceneOwnershipTests
{
    [Fact]
    public void RetainedSubtreeSurvivesRemovalAndReattachmentWithPixels()
    {
        Initializer.Init();
        using var canvas = SwCanvas.Gen(EngineOption.SmartRender)!;
        try
        {
            var pixels = new uint[32 * 32];
            Assert.Equal(Result.Success, canvas.Target(pixels, 32, 32, 32, ColorSpace.ABGR8888));
            var root = new Panel(); var left = new Panel(); var right = new Panel();
            var subtree = new Panel(); var shape = new Box();
            subtree.AddChild(shape); left.AddChild(subtree); root.AddChild(left); root.AddChild(right);
            Assert.Equal(Result.Success, canvas.Add(root.Scene));
            Contracts.Draw(canvas, true);
            uint expected = pixels[12 * 32 + 12]; Assert.NotEqual(0u, expected);
            left.RemoveChild(subtree);
            Contracts.Draw(canvas, true); Assert.Equal(0u, pixels[12 * 32 + 12]);
            right.AddChild(subtree);
            Contracts.Draw(canvas); Assert.Equal(expected, pixels[12 * 32 + 12]);
            Assert.Single(subtree.Children); Assert.Equal(2, subtree.Scene.Paints().Count);
            canvas.Remove(root.Scene);
        }
        finally { canvas.Dispose(); Initializer.Term(); }
    }

    [Fact]
    public void OverlappingUnchangedShapeReorderDamagesPartialRendering()
    {
        Initializer.Init();
        using var canvas = SwCanvas.Gen(EngineOption.SmartRender)!;
        try
        {
            var pixels = new uint[32 * 32]; canvas.Target(pixels, 32, 32, 32, ColorSpace.ABGR8888);
            var parent = new Panel(); var red = new Box(); var blue = new Box(0, 255);
            parent.AddChild(red); parent.AddChild(blue); canvas.Add(parent.Scene);
            Contracts.Draw(canvas, true); uint original = pixels[12 * 32 + 12];
            parent.ReorderChildren(new[] { blue, red }); Contracts.Draw(canvas);
            Assert.NotEqual(original, pixels[12 * 32 + 12]);
            uint reordered = pixels[12 * 32 + 12];
            Contracts.Draw(canvas, true); Assert.Equal(reordered, pixels[12 * 32 + 12]);
            parent.ReorderChildren(new[] { red, blue }); Contracts.Draw(canvas);
            Assert.Equal(original, pixels[12 * 32 + 12]);
            Assert.Same(parent.Scene, red.Scene.Parent()); Assert.Equal(0, red.Detachments);
            canvas.Remove(parent.Scene);
        }
        finally { canvas.Dispose(); Initializer.Term(); }
    }

    [Theory]
    [InlineData("subset")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("null")]
    public void ReorderRejectsAnythingExceptACompletePermutationAtomically(string kind)
    {
        var parent = new Panel(); var a = new Box(); var b = new Box(); var foreign = new Box();
        parent.AddChild(a); parent.AddChild(b);
        var scenes = parent.Scene.Paints().ToArray();
        Element[] order = kind switch { "subset" => new Element[] { a }, "duplicate" => new Element[] { a, a },
            "foreign" => new Element[] { a, foreign }, _ => new Element[] { a, null! } };
        Assert.Throws<ArgumentException>(() => parent.ReorderChildren(order));
        Assert.Equal(new Element[] { a, b }, parent.Children); Assert.Equal(scenes, parent.Scene.Paints());
    }

    [Fact]
    public void DisposalReleasesDetachedSubtreePaintsAndRejectsReuse()
    {
        var parent = new Panel(); var child = new Panel(); var leaf = new Box();
        parent.AddChild(child); child.AddChild(leaf);
        Contracts.Dispose(child); Contracts.Dispose(child);
        Assert.Empty(parent.Children); Assert.Empty(child.Children); Assert.Empty(child.Scene.Paints());
        Assert.Empty(leaf.Scene.Paints()); Assert.Equal((ushort)0, child.Scene.RefCnt());
        Assert.Throws<ObjectDisposedException>(() => parent.AddChild(child));
    }
}

public class NodeOwnershipTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SameCountReplacementInitializesEveryChildBeforePublishing(int index)
    {
        var engine = new TaffyLayoutEngine(); var parent = new Panel();
        var old = new[] { new Box(), new Box(), new Box() };
        foreach (var box in old) parent.AddChild(box);
        engine.ComputeLayout(parent, 100, 50);
        for (int i = index; i < old.Length; i++) { parent.RemoveChild(old[i]); parent.AddChild(new Box()); }
        // Inspect synchronization before computing a layout with invalid node IDs.
        Assert.Null(Record.Exception(() => Contracts.Synchronize(engine, parent)));
        Assert.All(parent.Children, c => Assert.True(c._taffyNodeValid, "Every replacement must be initialized"));
        Assert.Equal(parent.Children.Select(c => c._taffyNode), Contracts.Tree(engine).Children(parent._taffyNode));
        Assert.All(parent.Children, c => Assert.Same(c, Contracts.Tree(engine).GetNodeContext(c._taffyNode)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingSubtreeReleasesAllNodesIncludingManualDescendantRoots(bool clear)
    {
        var engine = new TaffyLayoutEngine(); var root = new Panel(); var page = new Panel();
        var manual = new ScrollPanel(); var nested = new Panel(); var leaf = new Box();
        nested.AddChild(leaf); manual.AddChild(nested); page.AddChild(manual); root.AddChild(page);
        engine.ComputeLayout(root, 100, 50); engine.ComputeLayout(nested, 100, 50);
        Assert.Equal(5, Contracts.Tree(engine).TotalNodeCount());
        if (clear) root.ClearChildren(); else root.RemoveChild(page);
        Assert.Equal(1, Contracts.Tree(engine).TotalNodeCount());
        Assert.False(leaf._taffyNodeValid); Assert.False(nested._taffyNodeValid);
    }

    [Fact]
    public void EngineTransferCannotReuseAnUnrelatedNodeId()
    {
        var first = new TaffyLayoutEngine(); var second = new TaffyLayoutEngine();
        var root = new Panel(); root.AddChild(new Box()); first.ComputeLayout(root, 100, 50);
        var unrelated = new Box(); second.ComputeLayout(unrelated, 12, 12);
        Assert.Null(Record.Exception(() => second.ComputeLayout(root, 100, 50)));
        Assert.Same(unrelated, Contracts.Tree(second).GetNodeContext(unrelated._taffyNode));
        Assert.Same(root, Contracts.Tree(second).GetNodeContext(root._taffyNode));
        Assert.Equal(0, Contracts.Tree(first).TotalNodeCount());
        Assert.Equal(3, Contracts.Tree(second).TotalNodeCount());
    }

    [Fact]
    public void ClearInvalidatesElementIdsBeforeTheEngineIsReused()
    {
        var engine = new TaffyLayoutEngine(); var root = new Panel(); var child = new Box(); root.AddChild(child);
        engine.ComputeLayout(root, 100, 50); engine.Clear();
        Assert.False(root._taffyNodeValid); Assert.False(child._taffyNodeValid);
        engine.ComputeLayout(new Box(), 10, 10); engine.ComputeLayout(root, 100, 50);
        Assert.Same(child, Contracts.Tree(engine).GetNodeContext(child._taffyNode));
    }

    [Fact]
    public void RemovedPagesAreCollectableWhileTheirEngineLives()
    {
        var engine = new TaffyLayoutEngine(); var root = new Panel();
        var weak = AddAndRemove(engine, root);
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Assert.All(weak, w => Assert.False(w.IsAlive, "A removed Element must not be rooted by its layout context"));
        Assert.Equal(1, Contracts.Tree(engine).TotalNodeCount()); GC.KeepAlive(engine); GC.KeepAlive(root);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] AddAndRemove(TaffyLayoutEngine engine, Panel root)
    {
        var page = new Panel(); var manual = new ScrollPanel(); var nested = new Panel(); var leaf = new Box();
        nested.AddChild(leaf); manual.AddChild(nested); page.AddChild(manual); root.AddChild(page);
        engine.ComputeLayout(root, 100, 50); engine.ComputeLayout(nested, 100, 50);
        root.RemoveChild(page);
        return new[] { new WeakReference(page), new WeakReference(manual), new WeakReference(nested), new WeakReference(leaf) };
    }
}
