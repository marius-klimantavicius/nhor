using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class RoundingTests
{
    [Fact]
    public void RoundingDoesntLeaveGaps()
    {
        var taffy = NewTestTree();
        var wSquare = new Size<Dimension>(Dimension.FromLength(100.3f), Dimension.FromLength(100.3f));
        var childA = taffy.NewLeaf(new Style { SizeValue = wSquare });
        var childB = taffy.NewLeaf(new Style { SizeValue = wSquare });

        var rootNode = taffy.NewWithChildren(new Style
        {
            SizeValue = new Size<Dimension>(Dimension.FromLength(963.3333f), Dimension.FromLength(1000f)),
            JustifyContentValue = AlignContent.Center,
        }, new[] { childA, childB });

        taffy.ComputeLayout(rootNode, MaxContentSize);

        var layoutA = taffy.GetLayout(childA);
        var layoutB = taffy.GetLayout(childB);
        Assert.Equal(layoutA.Location.X + layoutA.Size.Width, layoutB.Location.X);
    }
}
