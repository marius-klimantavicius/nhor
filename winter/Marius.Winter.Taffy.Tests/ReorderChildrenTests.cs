using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class ReorderChildrenTests
{
    /// <summary>
    /// Basic shuffle: three children are reordered. Verifies Children list
    /// and Scene.Paints() order both update correctly.
    /// </summary>
    [Fact]
    public void ReorderChildren_BasicShuffle_UpdatesChildrenAndPaints()
    {
        var panel = new Panel();
        var a = new Panel(); // use Panel as lightweight child
        var b = new Panel();
        var c = new Panel();

        panel.AddChild(a);
        panel.AddChild(b);
        panel.AddChild(c);

        // Verify initial order
        Assert.Equal(new Element[] { a, b, c }, panel.Children);

        // Reorder to [C, A, B]
        panel.ReorderChildren(new List<Element> { c, a, b });

        Assert.Equal(3, panel.Children.Count);
        Assert.Same(c, panel.Children[0]);
        Assert.Same(a, panel.Children[1]);
        Assert.Same(b, panel.Children[2]);

        // Verify scene paint order: _paintScene is first, then child scenes
        var paints = panel.Scene.Paints();
        int aIdx = -1, bIdx = -1, cIdx = -1;
        for (int i = 0; i < paints.Count; i++)
        {
            if (paints[i] == a.Scene) aIdx = i;
            if (paints[i] == b.Scene) bIdx = i;
            if (paints[i] == c.Scene) cIdx = i;
        }
        Assert.True(cIdx < aIdx, "c.Scene should come before a.Scene in paints");
        Assert.True(aIdx < bIdx, "a.Scene should come before b.Scene in paints");
    }

    /// <summary>
    /// Multiple consecutive shuffles should not corrupt state.
    /// Reproduces the Blazor @key shuffle scenario.
    /// </summary>
    [Fact]
    public void ReorderChildren_MultipleShuffles_RemainsConsistent()
    {
        var panel = new Panel();
        var children = Enumerable.Range(0, 8).Select(i => new Panel()).ToArray();
        foreach (var child in children)
            panel.AddChild(child);

        // Shuffle 1: reverse
        var reversed = children.Reverse().ToList();
        panel.ReorderChildren(reversed);
        Assert.Equal(reversed, panel.Children);

        // Shuffle 2: rotate left by 1
        var rotated = reversed.Skip(1).Concat(reversed.Take(1)).ToList();
        panel.ReorderChildren(rotated);
        Assert.Equal(rotated, panel.Children);

        // Shuffle 3: restore original
        var original = children.ToList();
        panel.ReorderChildren(original);
        Assert.Equal(original, panel.Children);

        // Verify all scenes still present in paints
        var paints = panel.Scene.Paints();
        foreach (var child in children)
            Assert.Contains(child.Scene, paints);
    }

    /// <summary>
    /// Identity reorder (same order) should be a no-op.
    /// </summary>
    [Fact]
    public void ReorderChildren_SameOrder_IsNoOp()
    {
        var panel = new Panel();
        var a = new Panel();
        var b = new Panel();
        panel.AddChild(a);
        panel.AddChild(b);

        panel.ReorderChildren(new List<Element> { a, b });

        Assert.Same(a, panel.Children[0]);
        Assert.Same(b, panel.Children[1]);
    }

    /// <summary>
    /// Reorder when the parent scene contains extra non-child paints
    /// (e.g. scrollbar shapes added directly to Scene).
    /// The extra paints should remain untouched.
    /// </summary>
    [Fact]
    public void ReorderChildren_WithExtraPaintsInScene_PreservesExtraPaints()
    {
        var panel = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();

        panel.AddChild(a);
        panel.AddChild(b);
        panel.AddChild(c);

        // Add an extra shape directly to the parent scene (simulates scrollbar shapes)
        var extraShape = ThorVG.Shape.Gen()!;
        panel.Scene.Add(extraShape);

        panel.ReorderChildren(new List<Element> { c, b, a });

        Assert.Same(c, panel.Children[0]);
        Assert.Same(b, panel.Children[1]);
        Assert.Same(a, panel.Children[2]);

        // Extra shape should still be in paints
        Assert.Contains(extraShape, panel.Scene.Paints());
    }

    /// <summary>
    /// BUG REPRO: When a child's scene is missing from _scene.paints
    /// (e.g. due to a failed Scene.Add), the old algorithm crashes with
    /// IndexOutOfRangeException because the reordered array is sized by
    /// paintPositions.Count but indexed by newOrder indices.
    /// </summary>
    [Fact]
    public void ReorderChildren_MissingChildScene_DoesNotThrow()
    {
        var panel = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();

        panel.AddChild(a);
        panel.AddChild(b);
        panel.AddChild(c);

        // Simulate a desync: remove b's scene from the parent scene.
        // This can happen if Scene.Add failed silently during child insertion,
        // or during a complex render batch with concurrent add/remove/reorder.
        panel.Scene.Remove(b.Scene);

        // The old code threw IndexOutOfRangeException here because:
        //   sceneToNewIndex has 3 entries (indices 0,1,2)
        //   paintPositions has only 2 entries (a and c found, b missing)
        //   reordered = new Paint[2]
        //   reordered[2] = ... → IndexOutOfRangeException
        var ex = Record.Exception(() =>
            panel.ReorderChildren(new List<Element> { c, a, b }));

        Assert.Null(ex);
    }

    /// <summary>
    /// Fisher-Yates shuffle of 8 items, executed 10 times.
    /// Mimics the exact Blazor @key demo scenario.
    /// </summary>
    [Fact]
    public void ReorderChildren_FisherYatesShuffle_MultipleRounds()
    {
        var panel = new Panel();
        var children = Enumerable.Range(0, 8).Select(i => new Panel()).ToArray();
        foreach (var child in children)
            panel.AddChild(child);

        var rng = new System.Random(42);
        for (int round = 0; round < 10; round++)
        {
            var list = panel.Children.ToList();
            // Fisher-Yates shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            panel.ReorderChildren(list);

            Assert.Equal(list.Count, panel.Children.Count);
            for (int i = 0; i < list.Count; i++)
                Assert.Same(list[i], panel.Children[i]);

            // Verify all child scenes are still in paints and in the correct relative order
            var paints = panel.Scene.Paints();
            int prevIdx = -1;
            foreach (var child in panel.Children)
            {
                int idx = -1;
                for (int k = 0; k < paints.Count; k++)
                    if (paints[k] == child.Scene) { idx = k; break; }
                Assert.True(idx > prevIdx, $"Round {round}: child scene not in ascending paint order");
                prevIdx = idx;
            }
        }
    }

    /// <summary>
    /// ScrollPanel-specific test: the ScrollPanel adds scrollbar shapes
    /// directly to Scene (not via AddPaint). After reorder, scrollbar shapes
    /// must not be corrupted and children must be in correct order.
    /// </summary>
    [Fact]
    public void ReorderChildren_ScrollPanel_ShuffleDoesNotCrash()
    {
        var scrollPanel = new ScrollPanel();
        var children = Enumerable.Range(0, 8).Select(i => new Panel()).ToArray();
        foreach (var child in children)
            scrollPanel.AddChild(child);

        // Shuffle children
        var shuffled = children.Reverse().ToList();
        scrollPanel.ReorderChildren(shuffled);

        Assert.Equal(shuffled, scrollPanel.Children);

        // All child scenes still present
        var paints = scrollPanel.Scene.Paints();
        foreach (var child in children)
            Assert.Contains(child.Scene, paints);
    }
}
