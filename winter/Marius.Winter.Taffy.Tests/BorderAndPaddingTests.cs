using Xunit;
using Marius.Winter.Taffy;
using static Marius.Winter.Taffy.Tests.TestHelpers;

namespace Marius.Winter.Taffy.Tests;

public class BorderAndPaddingTests
{
    [Fact(Skip = "Ignored in upstream")]
    public void BorderOnASingleAxisDoesntIncreaseSize()
    {
        var definiteSize = new Size<AvailableSpace>(AvailableSpace.Definite(100f), AvailableSpace.Definite(100f));
        for (int i = 0; i < 4; i++)
        {
            var taffy = NewTestTree();
            var border = new Rect<LengthPercentage>(
                LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);
            switch (i)
            {
                case 0: border.Left = LengthPercentage.FromLength(10f); break;
                case 1: border.Right = LengthPercentage.FromLength(10f); break;
                case 2: border.Top = LengthPercentage.FromLength(10f); break;
                case 3: border.Bottom = LengthPercentage.FromLength(10f); break;
            }

            var node = taffy.NewLeaf(new Style { BorderValue = border });
            taffy.ComputeLayout(node, definiteSize);
            var layout = taffy.GetLayout(node);
            Assert.Equal(0f, layout.Size.Width * layout.Size.Height);
        }
    }

    [Fact(Skip = "Ignored in upstream")]
    public void PaddingOnASingleAxisDoesntIncreaseSize()
    {
        var definiteSize = new Size<AvailableSpace>(AvailableSpace.Definite(100f), AvailableSpace.Definite(100f));
        for (int i = 0; i < 4; i++)
        {
            var taffy = NewTestTree();
            var padding = new Rect<LengthPercentage>(
                LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);
            switch (i)
            {
                case 0: padding.Left = LengthPercentage.FromLength(10f); break;
                case 1: padding.Right = LengthPercentage.FromLength(10f); break;
                case 2: padding.Top = LengthPercentage.FromLength(10f); break;
                case 3: padding.Bottom = LengthPercentage.FromLength(10f); break;
            }

            var node = taffy.NewLeaf(new Style { PaddingValue = padding });
            taffy.ComputeLayout(node, definiteSize);
            var layout = taffy.GetLayout(node);
            Assert.Equal(0f, layout.Size.Width * layout.Size.Height);
        }
    }

    [Fact(Skip = "Ignored in upstream")]
    public void BorderAndPaddingOnASingleAxisDoesntIncreaseSize()
    {
        var definiteSize = new Size<AvailableSpace>(AvailableSpace.Definite(100f), AvailableSpace.Definite(100f));
        for (int i = 0; i < 4; i++)
        {
            var taffy = NewTestTree();
            var rect = new Rect<LengthPercentage>(
                LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);
            switch (i)
            {
                case 0: rect.Left = LengthPercentage.FromLength(10f); break;
                case 1: rect.Right = LengthPercentage.FromLength(10f); break;
                case 2: rect.Top = LengthPercentage.FromLength(10f); break;
                case 3: rect.Bottom = LengthPercentage.FromLength(10f); break;
            }

            var node = taffy.NewLeaf(new Style { BorderValue = rect, PaddingValue = rect });
            taffy.ComputeLayout(node, definiteSize);
            var layout = taffy.GetLayout(node);
            Assert.Equal(0f, layout.Size.Width * layout.Size.Height);
        }
    }

    [Fact(Skip = "Ignored in upstream")]
    public void VerticalBorderAndPaddingPercentageValuesUseAvailableSpaceCorrectly()
    {
        var taffy = NewTestTree();
        var node = taffy.NewLeaf(new Style
        {
            PaddingValue = new Rect<LengthPercentage>(
                LengthPercentage.FromPercent(1f), LengthPercentage.ZERO,
                LengthPercentage.FromPercent(1f), LengthPercentage.ZERO),
        });
        taffy.ComputeLayout(node, new Size<AvailableSpace>(
            AvailableSpace.Definite(200f), AvailableSpace.Definite(100f)));

        Assert.Equal(200f, taffy.GetLayout(node).Size.Width);
        Assert.Equal(200f, taffy.GetLayout(node).Size.Height);
    }
}
