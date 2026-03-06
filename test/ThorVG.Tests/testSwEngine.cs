using System;
using Xunit;

namespace ThorVG.Tests
{
    public class testSwEngine
    {
        private static readonly string TEST_DIR = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ThorVG", "test", "resources"));

        private static Shape CreateMask()
        {
            var mask = Shape.Gen();
            mask.AppendRect(0, 10, 20, 30, 5, 5);
            mask.Opacity(127);
            mask.SetFill(255, 255, 255);
            return mask;
        }

        private static readonly BlendMethod[] Methods = new[]
        {
            BlendMethod.Normal, BlendMethod.Multiply, BlendMethod.Screen,
            BlendMethod.Overlay, BlendMethod.Darken, BlendMethod.Lighten,
            BlendMethod.ColorDodge, BlendMethod.ColorBurn, BlendMethod.HardLight,
            BlendMethod.SoftLight, BlendMethod.Difference, BlendMethod.Hue,
            BlendMethod.Saturation, BlendMethod.Color, BlendMethod.Luminosity,
            BlendMethod.Add, BlendMethod.Composition
        };

        private static readonly MaskMethod[] Masks = new[]
        {
            MaskMethod.None, MaskMethod.Alpha, MaskMethod.InvAlpha,
            MaskMethod.Luma, MaskMethod.InvLuma, MaskMethod.Add,
            MaskMethod.Subtract, MaskMethod.Intersect, MaskMethod.Difference,
            MaskMethod.Lighten, MaskMethod.Darken
        };

        [Fact]
        public void BasicDraw()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888S));

            foreach (var method in Methods)
            {
                foreach (var maskOp in Masks)
                {
                    // Arc Line
                    var shape1 = Shape.Gen();
                    Assert.Equal(Result.Success, shape1.StrokeFill(255, 255, 255, 255));
                    Assert.Equal(Result.Success, shape1.StrokeWidth(2));
                    Assert.Equal(Result.Success, shape1.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape1.SetMask(CreateMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape1));

                    // Cubic
                    var shape2 = Shape.Gen();
                    Assert.Equal(Result.Success, shape2.MoveTo(50, 25));
                    Assert.Equal(Result.Success, shape2.CubicTo(62, 25, 75, 38, 75, 50));
                    Assert.Equal(Result.Success, shape2.Close());
                    Assert.Equal(Result.Success, shape2.StrokeFill(255, 0, 0, 125));
                    Assert.Equal(Result.Success, shape2.StrokeWidth(1));
                    Assert.Equal(Result.Success, shape2.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape2.SetMask(CreateMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape2));

                    // Fill
                    var shape3 = Shape.Gen();
                    Assert.Equal(Result.Success, shape3.MoveTo(0, 0));
                    Assert.Equal(Result.Success, shape3.LineTo(20, 0));
                    Assert.Equal(Result.Success, shape3.LineTo(20, 20));
                    Assert.Equal(Result.Success, shape3.LineTo(0, 20));
                    Assert.Equal(Result.Success, shape3.Close());
                    Assert.Equal(Result.Success, shape3.SetFill(255, 255, 255));
                    Assert.Equal(Result.Success, shape3.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape3.SetMask(CreateMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape3));

                    // Dashed Line shape
                    var shape4 = Shape.Gen();
                    var dashPattern = new float[] { 2.5f, 5.0f };
                    Assert.Equal(Result.Success, shape4.MoveTo(0, 0));
                    Assert.Equal(Result.Success, shape4.LineTo(25, 25));
                    Assert.Equal(Result.Success, shape4.CubicTo(50, 50, 75, -75, 50, 100));
                    Assert.Equal(Result.Success, shape4.Close());
                    Assert.Equal(Result.Success, shape4.SetFill(255, 255, 255));
                    Assert.Equal(Result.Success, shape4.StrokeFill(255, 0, 0, 255));
                    Assert.Equal(Result.Success, shape4.StrokeWidth(2));
                    Assert.Equal(Result.Success, shape4.StrokeDash(dashPattern, 2));
                    Assert.Equal(Result.Success, shape4.StrokeCap(StrokeCap.Round));
                    Assert.Equal(Result.Success, shape4.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape4.SetMask(CreateMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape4));
                }
            }

            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void ImageDraw()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            // Raw image
            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            Shape CreateImageMask()
            {
                var m = Shape.Gen();
                m.AppendRect(0, 10, 20, 30, 5, 5);
                m.SetFill(255, 255, 255);
                return m;
            }

            foreach (var method in Methods)
            {
                foreach (var maskOp in Masks)
                {
                    // Non-transformed images
                    var picture = Picture.Gen();
                    Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));
                    Assert.Equal(Result.Success, picture.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, picture.SetMask(CreateImageMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(picture));

                    // Clipped images
                    var picture2 = picture.Duplicate();
                    Assert.Equal(Result.Success, picture2.Clip(CreateImageMask()));
                    Assert.Equal(Result.Success, canvas.Add(picture2));

                    // Transformed images
                    var picture3 = picture.Duplicate();
                    Assert.Equal(Result.Success, picture3.Rotate(45));
                    Assert.Equal(Result.Success, canvas.Add(picture3));

                    // Up-scaled Image
                    var picture4 = picture.Duplicate();
                    Assert.Equal(Result.Success, picture4.Scale(2.0f));
                    Assert.Equal(Result.Success, canvas.Add(picture4));

                    // Down-scaled Image
                    var picture5 = picture.Duplicate();
                    Assert.Equal(Result.Success, picture5.Scale(0.25f));
                    Assert.Equal(Result.Success, canvas.Add(picture5));

                    // Direct Clipped image
                    var picture6 = Picture.Gen();
                    Assert.Equal(Result.Success, picture6.Load(data, 200, 300, ColorSpace.ARGB8888, false));
                    Assert.Equal(Result.Success, picture6.Clip(CreateImageMask()));
                    Assert.Equal(Result.Success, picture6.SetBlend(method));
                    Assert.Equal(Result.Success, canvas.Add(picture6));

                    // Scaled Clipped image
                    var picture7 = picture6.Duplicate();
                    Assert.Equal(Result.Success, picture7.Scale(2.0f));
                    Assert.Equal(Result.Success, canvas.Add(picture7));
                }
            }

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void FillingDraw()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            Shape CreateFillingMask()
            {
                var m = Shape.Gen();
                m.AppendRect(10, 10, 20, 30, 5, 5);
                m.Opacity(127);
                m.SetFill(255, 255, 255);
                return m;
            }

            var cs = new Fill.ColorStop[]
            {
                new Fill.ColorStop(0.1f, 0, 0, 0, 0),
                new Fill.ColorStop(0.2f, 50, 25, 50, 25),
                new Fill.ColorStop(0.5f, 100, 100, 100, 125),
                new Fill.ColorStop(0.9f, 255, 255, 255, 255)
            };

            foreach (var method in Methods)
            {
                foreach (var maskOp in Masks)
                {
                    // Linear Gradient
                    var linear = LinearGradient.Gen();
                    Assert.Equal(Result.Success, linear.SetColorStops(cs, 4));
                    Assert.Equal(Result.Success, linear.SetSpread(FillSpread.Repeat));
                    Assert.Equal(Result.Success, linear.Linear(0.0f, 0.0f, 100.0f, 120.0f));

                    var shape = Shape.Gen();
                    Assert.Equal(Result.Success, shape.AppendRect(0, 0, 50, 50, 5, 5));
                    Assert.Equal(Result.Success, shape.SetFill(linear));
                    Assert.Equal(Result.Success, shape.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape.SetMask(CreateFillingMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape));

                    // Radial Gradient
                    var radial = RadialGradient.Gen();
                    Assert.Equal(Result.Success, radial.SetColorStops(cs, 4));
                    Assert.Equal(Result.Success, radial.SetSpread(FillSpread.Pad));
                    Assert.Equal(Result.Success, radial.Radial(50.0f, 50.0f, 50.0f, 50.0f, 50.0f, 0.0f));

                    var shape2 = Shape.Gen();
                    Assert.Equal(Result.Success, shape2.AppendRect(50, 0, 50, 50));
                    Assert.Equal(Result.Success, shape2.SetFill(radial));
                    Assert.Equal(Result.Success, shape2.SetBlend(method));
                    if (maskOp != MaskMethod.None) Assert.Equal(Result.Success, shape2.SetMask(CreateFillingMask(), maskOp));
                    Assert.Equal(Result.Success, canvas.Add(shape2));
                }
            }

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void ImageRotation()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            const uint cw = 960;
            const uint ch = 960;
            var buffer = new uint[cw * ch];
            Assert.Equal(Result.Success, canvas.Target(buffer, cw, ch, cw, ColorSpace.ARGB8888));

            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_250x375.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var rawData = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, rawData, 0, bytes.Length);

            Assert.Equal(Result.Success, picture.Load(rawData, 250, 375, ColorSpace.ARGB8888, false));

            Assert.Equal(Result.Success, picture.SetSize(240, 240));
            Assert.Equal(Result.Success, picture.Transform(new Matrix(0.572866f, -4.431353f, 336.605835f, 5.198910f, -0.386219f, 30.710693f, 0.0f, 0.0f, 1.0f)));
            Assert.Equal(Result.Success, canvas.Add(picture));

            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());

            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
