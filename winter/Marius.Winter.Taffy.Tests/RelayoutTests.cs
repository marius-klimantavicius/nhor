using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class RelayoutTests
{
    [Fact]
    public void Relayout()
    {
        var taffy = NewTestTree();
        var node1 = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(8f), Dimension.FromLength(80f)),
        });
        var node0 = taffy.NewWithChildren(new Style
        {
            AlignSelfValue = AlignItems.Center,
            SizeValue = new Size<Dimension>(Dimension.Auto(), Dimension.Auto()),
        }, new[] { node1 });
        var node = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.FromPercent(1f)),
        }, new[] { node0 });

        var availSpace = new Size<AvailableSpace>(AvailableSpace.Definite(100f), AvailableSpace.Definite(100f));
        taffy.ComputeLayout(node, availSpace);
        var initial = taffy.GetLayout(node).Location;
        var initial0 = taffy.GetLayout(node0).Location;
        var initial1 = taffy.GetLayout(node1).Location;

        for (int i = 1; i < 10; i++)
        {
            taffy.ComputeLayout(node, availSpace);
            Assert.Equal(initial.X, taffy.GetLayout(node).Location.X);
            Assert.Equal(initial.Y, taffy.GetLayout(node).Location.Y);
            Assert.Equal(initial0.X, taffy.GetLayout(node0).Location.X);
            Assert.Equal(initial0.Y, taffy.GetLayout(node0).Location.Y);
            Assert.Equal(initial1.X, taffy.GetLayout(node1).Location.X);
            Assert.Equal(initial1.Y, taffy.GetLayout(node1).Location.Y);
        }
    }

    [Fact]
    public void ToggleRootDisplayNone()
    {
        var hidden = new Style
        {
            Display = Display.None,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };
        var flex = new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };

        var taffy = NewTestTree();
        var node = taffy.NewLeaf(hidden);

        taffy.ComputeLayout(node, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, flex);
        taffy.ComputeLayout(node, MaxContentSize);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, hidden);
        taffy.ComputeLayout(node, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void ToggleRootDisplayNoneWithChildren()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(800f), Dimension.FromLength(100f)),
        });
        var parent = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(800f), Dimension.FromLength(100f)),
        }, new[] { child });
        var root = taffy.NewWithChildren(new Style(), new[] { parent });

        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(800f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);

        taffy.SetStyle(root, new Style { Display = Display.None });
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(child).Size.Height);

        taffy.SetStyle(root, new Style());
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(800f, taffy.GetLayout(parent).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(parent).Size.Height);
        Assert.Equal(800f, taffy.GetLayout(child).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(child).Size.Height);
    }

    [Fact]
    public void ToggleFlexChildDisplayNone()
    {
        var hidden = new Style
        {
            Display = Display.None,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };
        var flex = new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };

        var taffy = NewTestTree();
        var node = taffy.NewLeaf(hidden);
        var root = taffy.NewWithChildren(flex, new[] { node });

        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, flex);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, hidden);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void ToggleFlexContainerDisplayNone()
    {
        var hidden = new Style
        {
            Display = Display.None,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };
        var flex = new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };

        var taffy = NewTestTree();
        var node = taffy.NewLeaf(hidden);
        var root = taffy.NewWithChildren(hidden, new[] { node });

        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Height);

        taffy.SetStyle(root, flex);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(100f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(root).Size.Height);

        taffy.SetStyle(root, hidden);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Height);
    }

    [Fact]
    public void ToggleGridChildDisplayNone()
    {
        var hidden = new Style
        {
            Display = Display.None,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };
        var grid = new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };

        var taffy = NewTestTree();
        var node = taffy.NewLeaf(hidden);
        var root = taffy.NewWithChildren(grid, new[] { node });

        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, grid);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(node).Size.Height);

        taffy.SetStyle(node, hidden);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(node).Size.Height);
    }

    [Fact]
    public void ToggleGridContainerDisplayNone()
    {
        var hidden = new Style
        {
            Display = Display.None,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };
        var grid = new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        };

        var taffy = NewTestTree();
        var node = taffy.NewLeaf(hidden);
        var root = taffy.NewWithChildren(hidden, new[] { node });

        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Height);

        taffy.SetStyle(root, grid);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(100f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(100f, taffy.GetLayout(root).Size.Height);

        taffy.SetStyle(root, hidden);
        taffy.ComputeLayout(root, MaxContentSize);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Width);
        Assert.Equal(0f, taffy.GetLayout(root).Size.Height);
    }

    [Fact]
    public void RelayoutIsStableWithRounding()
    {
        var taffy = NewTestTree();
        taffy.EnableRounding();

        var inner = taffy.NewLeaf(new Style
        {
            MinSizeValue = new Size<Dimension>(Dimension.FromLength(300f), Dimension.Auto()),
        });
        var wrapper = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.Auto()),
            JustifyContentValue = AlignContent.End,
        }, new[] { inner });
        var outer = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromPercent(1f), Dimension.Auto()),
            Inset = new Rect<LengthPercentageAuto>(
                LengthPercentageAuto.Length(1.5f), LengthPercentageAuto.AUTO,
                LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO),
        }, new[] { wrapper });
        var root = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(1920f), Dimension.FromLength(1080f)),
        }, new[] { outer });

        for (int i = 0; i < 5; i++)
        {
            taffy.MarkDirty(root);
            taffy.ComputeLayout(root, MaxContentSize);

            Assert.Equal(0f, taffy.GetLayout(root).Location.X);
            Assert.Equal(0f, taffy.GetLayout(root).Location.Y);
            Assert.Equal(1920f, taffy.GetLayout(root).Size.Width);
            Assert.Equal(1080f, taffy.GetLayout(root).Size.Height);

            Assert.Equal(2f, taffy.GetLayout(outer).Location.X);
            Assert.Equal(0f, taffy.GetLayout(outer).Location.Y);
            Assert.Equal(1920f, taffy.GetLayout(outer).Size.Width);
            Assert.Equal(1080f, taffy.GetLayout(outer).Size.Height);

            Assert.Equal(0f, taffy.GetLayout(wrapper).Location.X);
            Assert.Equal(0f, taffy.GetLayout(wrapper).Location.Y);
            Assert.Equal(150f, taffy.GetLayout(wrapper).Size.Width);
            Assert.Equal(1080f, taffy.GetLayout(wrapper).Size.Height);

            Assert.Equal(-150f, taffy.GetLayout(inner).Location.X);
            Assert.Equal(0f, taffy.GetLayout(inner).Location.Y);
            Assert.Equal(301f, taffy.GetLayout(inner).Size.Width);
            Assert.Equal(1080f, taffy.GetLayout(inner).Size.Height);
        }
    }
}
