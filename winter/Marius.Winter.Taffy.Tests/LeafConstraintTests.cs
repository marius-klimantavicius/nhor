using Marius.Winter.Taffy;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class LeafConstraintTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KnownBorderBoxDimensionsStillSubtractPaddingAndBordersForMeasurement(bool horizontal)
    {
        var style = new Style
        {
            PaddingValue = new Rect<LengthPercentage>(LengthPercentage.Length(5), LengthPercentage.Length(7), LengthPercentage.Length(2), LengthPercentage.Length(4)),
            BorderValue = new Rect<LengthPercentage>(LengthPercentage.Length(3), LengthPercentage.Length(5), LengthPercentage.Length(1), LengthPercentage.Length(3)),
        };
        var input = new LayoutInput
        {
            RunMode = RunMode.PerformLayout,
            SizingMode = SizingMode.InherentSize,
            Axis = RequestedAxis.Both,
            KnownDimensions = horizontal ? new Size<float?>(80, null) : new Size<float?>(null, 80),
            AvailableSpace = new Size<AvailableSpace>(AvailableSpace.Definite(100), AvailableSpace.Definite(100)),
        };
        Size<AvailableSpace> measuredAvailable = default;
        LeafCompute.ComputeLeafLayout(input, style, (_, _) => 0, (known, available) =>
        {
            Assert.Equal(SizeExtensions.NoneF32, known);
            measuredAvailable = available;
            return new Size<float>(10, 10);
        });
        Assert.Equal(horizontal ? 60 : 80, measuredAvailable.Width.Unwrap());
        Assert.Equal(horizontal ? 90 : 70, measuredAvailable.Height.Unwrap());
    }

    [Fact]
    public void AvailableBorderBoxSpaceAlreadyExcludesMargins()
    {
        var style = new Style
        {
            MarginValue = new Rect<LengthPercentageAuto>(LengthPercentageAuto.Length(10), LengthPercentageAuto.Length(20),
                LengthPercentageAuto.Length(5), LengthPercentageAuto.Length(15)),
        };
        var input = new LayoutInput
        {
            RunMode = RunMode.PerformLayout,
            SizingMode = SizingMode.InherentSize,
            Axis = RequestedAxis.Both,
            AvailableSpace = new Size<AvailableSpace>(AvailableSpace.Definite(75), AvailableSpace.Definite(60)),
        };
        var output = LeafCompute.ComputeLeafLayout(input, style, (_, _) => 0,
            (_, available) => new Size<float>(available.Width.Unwrap(), available.Height.Unwrap()));
        Assert.Equal(new Size<float>(75, 60), output.Size);
    }
}
