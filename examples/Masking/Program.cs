using System.IO;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Image
        var bytes = File.ReadAllBytes(ExamplePaths.ExampleDir + "/image/rawimage_200x300.raw");
        var data = new uint[200 * 300];
        System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

        //Masking
        {
            //Solid Rectangle
            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 300.0f, 300.0f);
            shape.SetFill(0, 0, 255);

            //Mask
            var mask = Shape.Gen();
            mask.AppendCircle(150.0f, 150.0f, 93.75f, 93.75f);
            mask.SetFill(255, 255, 255);

            //Nested Mask
            var nMask = Shape.Gen();
            nMask.AppendCircle(165.0f, 165.0f, 93.75f, 93.75f);
            nMask.SetFill(255, 255, 255);

            mask.SetMask(nMask, MaskMethod.Alpha);
            shape.SetMask(mask, MaskMethod.Alpha);
            canvas.Add(shape);

            //SVG
            var svg = Picture.Gen();
            if (!Verify(svg.Load(ExamplePaths.ExampleDir + "/svg/cartman.svg"))) return false;
            svg.Opacity(100);
            svg.Scale(2.25f);
            svg.Translate(37.5f, 300.0f);

            //Mask2
            var mask2 = Shape.Gen();
            mask2.AppendCircle(112.5f, 375.0f, 56.25f, 56.25f);
            mask2.AppendRect(112.5f, 375.0f, 150.0f, 150.0f, 22.5f, 22.5f);
            mask2.SetFill(255, 255, 255);
            svg.SetMask(mask2, MaskMethod.Alpha);
            canvas.Add(svg);

            //Star
            var star = Shape.Gen();
            star.SetFill(80, 80, 80);
            star.MoveTo(449.25f, 25.5f);
            star.LineTo(489.75f, 107.25f);
            star.LineTo(580.5f, 120.0f);
            star.LineTo(515.25f, 183.0f);
            star.LineTo(530.25f, 273.75f);
            star.LineTo(449.25f, 231.75f);
            star.LineTo(372.75f, 273.75f);
            star.LineTo(384.0f, 183.75f);
            star.LineTo(319.5f, 120.75f);
            star.LineTo(409.5f, 107.25f);
            star.Close();
            star.StrokeWidth(22.5f);
            star.StrokeJoin(StrokeJoin.Miter);
            star.StrokeFill(255, 255, 255);

            //Mask3
            var mask3 = Shape.Gen();
            mask3.AppendCircle(450.0f, 150.0f, 93.75f, 93.75f);
            mask3.SetFill(255, 255, 255);
            mask3.Opacity(200);
            star.SetMask(mask3, MaskMethod.Alpha);
            canvas.Add(star);

            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image.Translate(375.0f, 300.0f);
            image.Scale(0.75f);

            //Mask4
            var mask4 = Shape.Gen();
            mask4.MoveTo(449.25f, 288.0f);
            mask4.LineTo(489.75f, 369.75f);
            mask4.LineTo(580.5f, 382.5f);
            mask4.LineTo(515.25f, 445.5f);
            mask4.LineTo(530.25f, 536.25f);
            mask4.LineTo(449.25f, 494.25f);
            mask4.LineTo(372.75f, 536.25f);
            mask4.LineTo(384.0f, 446.25f);
            mask4.LineTo(319.5f, 383.25f);
            mask4.LineTo(409.5f, 369.75f);
            mask4.Close();
            mask4.SetFill(255, 255, 255);
            mask4.Opacity(70);
            image.SetMask(mask4, MaskMethod.Alpha);
            canvas.Add(image);
        }

        //Inverse Masking
        {
            //Solid Rectangle
            var shape = Shape.Gen();
            shape.AppendRect(600.0f, 0.0f, 300.0f, 300.0f);
            shape.SetFill(0, 0, 255);

            //Mask
            var mask = Shape.Gen();
            mask.AppendCircle(750.0f, 150.0f, 93.75f, 93.75f);
            mask.SetFill(255, 255, 255);

            //Nested Mask
            var nMask = Shape.Gen();
            nMask.AppendCircle(765.0f, 165.0f, 93.75f, 93.75f);
            nMask.SetFill(255, 255, 255);

            mask.SetMask(nMask, MaskMethod.InvAlpha);
            shape.SetMask(mask, MaskMethod.InvAlpha);
            canvas.Add(shape);

            //SVG
            var svg = Picture.Gen();
            if (!Verify(svg.Load(ExamplePaths.ExampleDir + "/svg/cartman.svg"))) return false;
            svg.Opacity(100);
            svg.Scale(2.25f);
            svg.Translate(637.5f, 300.0f);

            //Mask2
            var mask2 = Shape.Gen();
            mask2.AppendCircle(712.5f, 375.0f, 56.25f, 56.25f);
            mask2.AppendRect(712.5f, 375.0f, 150.0f, 150.0f, 22.5f, 22.5f);
            mask2.SetFill(255, 255, 255);
            svg.SetMask(mask2, MaskMethod.InvAlpha);
            canvas.Add(svg);

            //Star
            var star = Shape.Gen();
            star.SetFill(80, 80, 80);
            star.MoveTo(1049.25f, 25.5f);
            star.LineTo(1089.75f, 107.25f);
            star.LineTo(1180.5f, 120.0f);
            star.LineTo(1115.25f, 183.0f);
            star.LineTo(1130.25f, 273.75f);
            star.LineTo(1049.25f, 231.75f);
            star.LineTo(972.75f, 273.75f);
            star.LineTo(984.0f, 183.75f);
            star.LineTo(919.5f, 120.75f);
            star.LineTo(1009.5f, 107.25f);
            star.Close();
            star.StrokeWidth(7.5f);
            star.StrokeFill(255, 255, 255);

            //Mask3
            var mask3 = Shape.Gen();
            mask3.AppendCircle(1050.0f, 150.0f, 93.75f, 93.75f);
            mask3.SetFill(255, 255, 255);
            star.SetMask(mask3, MaskMethod.InvAlpha);
            canvas.Add(star);

            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ABGR8888, true))) return false;
            image.Scale(0.75f);
            image.Translate(975.0f, 300.0f);

            //Mask4
            var mask4 = Shape.Gen();
            mask4.MoveTo(1049.25f, 288.0f);
            mask4.LineTo(1089.75f, 369.75f);
            mask4.LineTo(1180.5f, 382.5f);
            mask4.LineTo(1115.25f, 445.5f);
            mask4.LineTo(1130.25f, 536.25f);
            mask4.LineTo(1049.25f, 494.25f);
            mask4.LineTo(972.75f, 536.25f);
            mask4.LineTo(984.0f, 446.25f);
            mask4.LineTo(919.5f, 383.25f);
            mask4.LineTo(1009.5f, 369.75f);
            mask4.Close();
            mask4.SetFill(255, 255, 255);
            mask4.Opacity(70);
            image.SetMask(mask4, MaskMethod.InvAlpha);
            canvas.Add(image);
        }

        //Luma Masking
        {
            var shape = Shape.Gen();
            shape.AppendRect(0.0f, 525.0f, 300.0f, 300.0f);
            shape.SetFill(255, 0, 0);

            var mask = Shape.Gen();
            mask.AppendCircle(150.0f, 675.0f, 93.75f, 93.75f);
            mask.SetFill(255, 100, 255);

            var nMask = Shape.Gen();
            nMask.AppendCircle(165.0f, 690.0f, 93.75f, 93.75f);
            nMask.SetFill(255, 200, 255);

            mask.SetMask(nMask, MaskMethod.Luma);
            shape.SetMask(mask, MaskMethod.Luma);
            canvas.Add(shape);

            var svg = Picture.Gen();
            if (!Verify(svg.Load(ExamplePaths.ExampleDir + "/svg/cartman.svg"))) return false;
            svg.Opacity(100);
            svg.Scale(2.25f);
            svg.Translate(37.5f, 825.0f);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(112.5f, 900.0f, 56.25f, 56.25f);
            mask2.AppendRect(112.5f, 900.0f, 150.0f, 150.0f, 22.5f, 22.5f);
            mask2.SetFill(255, 255, 255);
            svg.SetMask(mask2, MaskMethod.Luma);
            canvas.Add(svg);

            var star = Shape.Gen();
            star.SetFill(80, 80, 80);
            star.MoveTo(449.25f, 540.0f);
            star.LineTo(489.75f, 632.25f);
            star.LineTo(580.5f, 645.0f);
            star.LineTo(515.25f, 708.0f);
            star.LineTo(530.25f, 798.75f);
            star.LineTo(449.25f, 756.75f);
            star.LineTo(372.75f, 798.75f);
            star.LineTo(384.0f, 708.75f);
            star.LineTo(319.5f, 645.75f);
            star.LineTo(409.5f, 632.25f);
            star.Close();
            star.StrokeWidth(7.5f);
            star.StrokeFill(255, 255, 255);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(450.0f, 675.0f, 93.75f, 93.75f);
            mask3.SetFill(0, 255, 255);
            star.SetMask(mask3, MaskMethod.Luma);
            canvas.Add(star);

            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image.Translate(375.0f, 825.0f);
            image.Scale(0.75f);

            var mask4 = Scene.Gen();
            var mask4_rect = Shape.Gen();
            mask4_rect.AppendRect(375.0f, 825.0f, 150.0f, 225.0f);
            mask4_rect.SetFill(255, 255, 255);
            var mask4_circle = Shape.Gen();
            mask4_circle.AppendCircle(450.0f, 937.5f, 93.75f, 93.75f);
            mask4_circle.SetFill(128, 0, 128);
            mask4.Add(mask4_rect);
            mask4.Add(mask4_circle);
            image.SetMask(mask4, MaskMethod.Luma);
            canvas.Add(image);
        }

        //Inverse Luma Masking
        {
            var shape = Shape.Gen();
            shape.AppendRect(600.0f, 525.0f, 300.0f, 300.0f);
            shape.SetFill(255, 0, 0);

            var mask = Shape.Gen();
            mask.AppendCircle(750.0f, 675.0f, 93.75f, 93.75f);
            mask.SetFill(255, 100, 255);

            var nMask = Shape.Gen();
            nMask.AppendCircle(765.0f, 690.0f, 93.75f, 93.75f);
            nMask.SetFill(255, 200, 255);

            mask.SetMask(nMask, MaskMethod.InvLuma);
            shape.SetMask(mask, MaskMethod.InvLuma);
            canvas.Add(shape);

            var svg = Picture.Gen();
            if (!Verify(svg.Load(ExamplePaths.ExampleDir + "/svg/cartman.svg"))) return false;
            svg.Opacity(100);
            svg.Scale(2.25f);
            svg.Translate(637.5f, 825.0f);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(712.5f, 900.0f, 56.25f, 56.25f);
            mask2.AppendRect(712.5f, 900.0f, 150.0f, 150.0f, 22.5f, 22.5f);
            mask2.SetFill(255, 255, 255);
            svg.SetMask(mask2, MaskMethod.InvLuma);
            canvas.Add(svg);

            var star = Shape.Gen();
            star.SetFill(80, 80, 80);
            star.MoveTo(1049.25f, 540.0f);
            star.LineTo(1089.75f, 632.25f);
            star.LineTo(1180.5f, 645.0f);
            star.LineTo(1115.25f, 708.0f);
            star.LineTo(1130.25f, 798.75f);
            star.LineTo(1049.25f, 756.75f);
            star.LineTo(972.75f, 798.75f);
            star.LineTo(984.0f, 708.75f);
            star.LineTo(919.5f, 645.75f);
            star.LineTo(1009.5f, 632.25f);
            star.Close();
            star.StrokeWidth(7.5f);
            star.StrokeFill(255, 255, 255);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(1050.0f, 675.0f, 93.75f, 93.75f);
            mask3.SetFill(0, 255, 255);
            star.SetMask(mask3, MaskMethod.InvLuma);
            canvas.Add(star);

            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image.Translate(975.0f, 825.0f);
            image.Scale(0.75f);

            var mask4 = Scene.Gen();
            var mask4_rect = Shape.Gen();
            mask4_rect.AppendRect(975.0f, 825.0f, 150.0f, 225.0f);
            mask4_rect.SetFill(255, 255, 255);
            var mask4_circle = Shape.Gen();
            mask4_circle.AppendCircle(1050.0f, 937.5f, 93.75f, 93.75f);
            mask4_circle.SetFill(128, 0, 128);
            mask4.Add(mask4_rect);
            mask4.Add(mask4_circle);
            image.SetMask(mask4, MaskMethod.InvLuma);
            canvas.Add(image);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: false, w: 1200, h: 1100);
}
