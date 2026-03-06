using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class CachingTests
{
    [Fact]
    public void MeasureCountFlexbox()
    {
        var taffy = NewTestTree();
        var leaf = taffy.NewLeafWithContext(new Style(), TestNodeContext.Fixed(50, 50));

        var node = taffy.NewWithChildren(new Style(), new[] { leaf });
        for (int i = 0; i < 100; i++)
            node = taffy.NewWithChildren(new Style(), new[] { node });

        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(4, taffy.GetNodeContext(leaf)!.Count);
    }

    [Fact]
    public void MeasureCountGrid()
    {
        var taffy = NewTestTree();
        var leaf = taffy.NewLeafWithContext(new Style { Display = Display.Grid }, TestNodeContext.Fixed(50, 50));

        var node = taffy.NewWithChildren(new Style(), new[] { leaf });
        for (int i = 0; i < 100; i++)
            node = taffy.NewWithChildren(new Style(), new[] { node });

        taffy.ComputeLayoutWithMeasure(node, MaxContentSize, MeasureFunction);

        Assert.Equal(4, taffy.GetNodeContext(leaf)!.Count);
    }
}
