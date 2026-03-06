using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class RootConstraintTests
{
    [Fact]
    public void RootWithPercentageSize()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        });
        taffy.ComputeLayout(node, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(200f)));

        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(200f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void RootWithNoSize()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style());
        taffy.ComputeLayout(node, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(100f)));

        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void RootWithLargerSize()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(200f), Dimension.FromLength(200f)),
        });
        taffy.ComputeLayout(node, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(100f)));

        Assert.Equal(200f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(200f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void RootPaddingAndBorderLargerThanDefiniteSize()
    {
        var tree = new TaffyTree<TestNodeContext>();
        var child = tree.NewLeaf(new Style());
        var root = tree.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
            PaddingValue = new Rect<LengthPercentage>(
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f),
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f)),
            BorderValue = new Rect<LengthPercentage>(
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f),
                LengthPercentage.FromLength(10f), LengthPercentage.FromLength(10f)),
        }, new[] { child });

        tree.ComputeLayout(root, MaxContentSize);
        Assert.Equal(40f, tree.GetLayout(root).Size.Width);
        Assert.Equal(40f, tree.GetLayout(root).Size.Height);
    }
}
