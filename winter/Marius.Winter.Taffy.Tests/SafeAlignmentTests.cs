using System.Collections.Immutable;
using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class SafeAlignmentTests
{
    [Fact]
    public void GridSafeAlignSelfFallsBackToStartOnOverflow()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(150f)),
            AlignSelfValue = AlignItems.SafeEnd,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(child).Location.Y);
    }

    [Fact]
    public void GridSafeAlignSelfBehavesAsEndWhenNoOverflow()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(40f)),
            AlignSelfValue = AlignItems.SafeEnd,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(60f, taffy.GetLayout(child).Location.Y);
    }

    [Fact]
    public void GridUnsafeAlignSelfKeepsOverflowingPosition()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(150f)),
            AlignSelfValue = AlignItems.End,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(-50f, taffy.GetLayout(child).Location.Y);
    }

    [Fact]
    public void GridSafeJustifySelfFallsBackToStartOnOverflow()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
            JustifySelfValue = AlignItems.SafeEnd,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(child).Location.X);
    }

    [Fact]
    public void GridSafeJustifySelfRtlFallsBackToRtlStartEdge()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(150f), Dimension.FromLength(50f)),
            JustifySelfValue = AlignItems.SafeEnd,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            DirectionValue = Direction.Rtl,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(-50f, taffy.GetLayout(child).Location.X);
    }

    [Fact]
    public void GridSafeAlignContentOverflowFallsBackToStart()
    {
        var taffy = NewTestTree();
        var childA = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(80f)) });
        var childB = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(40f), Dimension.FromLength(80f)) });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Grid,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            GridTemplateRows = ImmutableList.Create(GridTemplateComponent.FromLength(80f), GridTemplateComponent.FromLength(80f)),
            GridTemplateColumns = ImmutableList.Create(GridTemplateComponent.FromLength(40f)),
            AlignContentValue = AlignContent.SafeEnd,
        }, new NodeId[] { childA, childB });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(childA).Location.Y);
    }

    [Fact]
    public void FlexSafeAlignSelfFallsBackToStartOnOverflow()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(150f)),
            AlignSelfValue = AlignItems.SafeEnd,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(child).Location.Y);
    }

    [Fact]
    public void FlexUnsafeAlignSelfKeepsOverflowingPosition()
    {
        var taffy = NewTestTree();
        var child = taffy.NewLeaf(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(50f), Dimension.FromLength(150f)),
            AlignSelfValue = AlignItems.End,
        });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
        }, new NodeId[] { child });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(-50f, taffy.GetLayout(child).Location.Y);
    }

    [Fact]
    public void FlexSafeJustifyContentFallsBackToStartOnOverflow()
    {
        var taffy = NewTestTree();
        var childA = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.FromLength(50f)) });
        var childB = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(80f), Dimension.FromLength(50f)) });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            JustifyContentValue = AlignContent.SafeEnd,
        }, new NodeId[] { childA, childB });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(childA).Location.X);
    }

    [Fact]
    public void FlexSafeAlignContentFallsBackToStartOnMultilineOverflow()
    {
        var taffy = NewTestTree();
        var childA = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)) });
        var childB = taffy.NewLeaf(new Style { SizeValue = new Size<Dimension>(Dimension.FromLength(60f), Dimension.FromLength(80f)) });
        var root = taffy.NewWithChildren(new Style
        {
            Display = Display.Flex,
            FlexWrapValue = FlexWrap.Wrap,
            SizeValue = new Size<Dimension>(Dimension.FromLength(100f), Dimension.FromLength(100f)),
            AlignContentValue = AlignContent.SafeEnd,
        }, new NodeId[] { childA, childB });

        taffy.ComputeLayout(root, MaxContentSize);

        Assert.Equal(0f, taffy.GetLayout(childA).Location.Y);
    }
}
