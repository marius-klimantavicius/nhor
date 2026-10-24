using System;
using System.Reflection;
using ThorVG;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class MultilineMeasurementOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RichLocalMeasurementsBalanceNormalAndBoldFonts(bool bounds, bool bold)
    {
        using var pump = new NativePump();
        await pump.Call(_ =>
        {
            var font = Assert.IsAssignableFrom<FontLoader>(LoaderMgr.Font(bold ? "default-bold" : "default"));
            try
            {
                int before = font.sharing;
                using var editor = new RichTextBox("Winter");
                // Exercise local helpers before attachment, when the reusable
                // measurement paints do not exist. Both return paths own Text.
                string name = bounds ? "MeasureLineBounds" : "MeasureRunWidth";
                var measure = typeof(RichTextBox).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (var text in new[] { "", "e\u0301", "Winter" })
                {
                    object?[] args = bounds ? [text, bold, 0f, 0f, 0f, 0f] : [text, bold];
                    measure.Invoke(editor, args);
                    Assert.Equal(before, font.sharing);
                }
            }
            finally { LoaderMgr.Retrieve(font); }
        });
    }
}
