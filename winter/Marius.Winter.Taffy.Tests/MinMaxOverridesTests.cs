using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class MinMaxOverridesTests
{
    [Fact]
    public void MinOverridesMax()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        taffy.ComputeLayout(child, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(100f)));

        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MaxOverridesSize()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MaxSizeValue = new Size<Dimension>(Dimension.FromLength(10f), Dimension.FromLength(10f)),
        });
        taffy.ComputeLayout(child, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(100f)));

        Assert.Equal(10f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(10f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void MinOverridesSize()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(50f)),
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        });
        taffy.ComputeLayout(child, new Size<AvailableSpace>(
            AvailableSpace.Definite(100f), AvailableSpace.Definite(100f)));

        Assert.Equal(100f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }
}
