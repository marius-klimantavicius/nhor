using System;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class MutableContextTests
{
    private struct Context { public int Calls; }
    [Fact]
    public void ValueContextMutationAndAbsenceSurviveCallbacksCachingAndRemoval()
    {
        var tree = new TaffyTree<Context>(); var node = tree.NewLeafWithContext(new Style(), default);
        int totalCalls = 0;
        void Compute(bool expectedPresent) => tree.ComputeLayoutWithContextMeasure(node, new(AvailableSpace.MaxContent, AvailableSpace.MaxContent), (input, id, context, style) =>
        {
            totalCalls++; Assert.Equal(expectedPresent, context.HasValue);
            if (context.HasValue) context.Value.Calls++;
            else Assert.Throws<InvalidOperationException>(() => { var ignored = context.Value; });
            return LeafCompute.ComputeLeafLayout(input, style, (_, _) => 0, (_, _) => new(20, 10));
        });
        Compute(true); int coldCalls = totalCalls; Assert.True(coldCalls > 0); Assert.Equal(coldCalls, tree.GetNodeContextReference(node).Value.Calls);
        Compute(true); Assert.Equal(coldCalls, totalCalls);
        tree.MarkDirty(node); Compute(true); Assert.Equal(totalCalls, tree.GetNodeContextReference(node).Value.Calls);
        tree.ClearNodeContext(node); Assert.False(tree.TryGetNodeContext(node, out _)); Compute(false);
        var reference = tree.GetNodeContextReference(node); tree.Remove(node); tree.NewLeafWithContext(new Style(), default);
        Assert.False(reference.HasValue);
        Assert.Throws<InvalidOperationException>(() => { var ignored = reference.Value; });
    }
    [Fact]
    public void DisjointContextMutationRejectsMissingDuplicateAndStaleKeys()
    {
        var tree = new TaffyTree<Context>(); var a = tree.NewLeafWithContext(new Style(), default); var b = tree.NewLeafWithContext(new Style(), default); var absent = tree.NewLeaf(new Style());
        var references = tree.GetDisjointNodeContextMut([a, b]); Assert.NotNull(references);
        references[0].Value.Calls = 3; references[1].Value.Calls = 7;
        Assert.Equal(3, tree.GetNodeContextMut(a).Value.Calls); Assert.Equal(7, tree.GetNodeContextMut(b).Value.Calls);
        Assert.Null(tree.GetDisjointNodeContextMut([a, a])); Assert.Null(tree.GetDisjointNodeContextMut([a, absent]));
        Assert.Empty(tree.GetDisjointNodeContextMut([])!);
        tree.Remove(a); var reused = tree.NewLeafWithContext(new Style(), default);
        Assert.Null(tree.GetDisjointNodeContextMut([a, b])); Assert.False(references[0].HasValue);
        Assert.Equal(0, tree.GetNodeContextMut(reused).Value.Calls);
    }
}
