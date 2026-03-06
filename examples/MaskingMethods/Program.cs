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
        //Image source
        var bytes = File.ReadAllBytes(ExamplePaths.ExampleDir + "/image/rawimage_200x300.raw");
        var data = new uint[200 * 300];
        System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

        //background
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, 625, h);
        bg.SetFill(50, 50, 50);
        canvas.Add(bg);

        {
            //Shape + Shape Mask Add
            var shape = Shape.Gen();
            shape.AppendCircle(125, 100, 150, 150);
            shape.SetFill(255, 255, 255);

            var mask = Shape.Gen();
            mask.AppendCircle(125, 100, 50, 50);
            mask.SetFill(255, 255, 255);

            var add = Shape.Gen();
            add.AppendCircle(175, 100, 50, 50);
            add.SetFill(255, 255, 255);
            mask.SetMask(add, MaskMethod.Add);
            shape.SetMask(mask, MaskMethod.Alpha);
            canvas.Add(shape);

            //Shape + Shape Mask Subtract
            var shape2 = Shape.Gen();
            shape2.AppendCircle(375, 100, 150, 150);
            shape2.SetFill(255, 255, 255, 255);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(375, 100, 50, 50);
            mask2.SetFill(255, 255, 255, 127);

            var sub = Shape.Gen();
            sub.AppendCircle(400, 100, 50, 50);
            sub.SetFill(255, 255, 255);
            mask2.SetMask(sub, MaskMethod.Subtract);
            shape2.SetMask(mask2, MaskMethod.Alpha);
            canvas.Add(shape2);

            //Shape + Shape Mask Intersect
            var shape3 = Shape.Gen();
            shape3.AppendCircle(625, 100, 50, 50);
            shape3.SetFill(255, 255, 255, 127);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(625, 100, 50, 50);
            mask3.SetFill(255, 255, 255, 127);

            var inter = Shape.Gen();
            inter.AppendCircle(650, 100, 50, 50);
            inter.SetFill(255, 255, 255);
            mask3.SetMask(inter, MaskMethod.Intersect);
            shape3.SetMask(mask3, MaskMethod.Alpha);
            canvas.Add(shape3);

            //Shape + Shape Mask Difference
            var shape4 = Shape.Gen();
            shape4.AppendCircle(875, 100, 150, 150);
            shape4.SetFill(255, 255, 255);

            var mask4 = Shape.Gen();
            mask4.AppendCircle(875, 100, 50, 50);
            mask4.SetFill(255, 255, 255);

            var diff = Shape.Gen();
            diff.AppendCircle(900, 100, 50, 50);
            diff.SetFill(255, 255, 255);
            mask4.SetMask(diff, MaskMethod.Difference);
            shape4.SetMask(mask4, MaskMethod.Alpha);
            canvas.Add(shape4);

            //Shape + Shape Mask Lighten
            var shape5 = Shape.Gen();
            shape5.AppendCircle(1125, 100, 150, 150);
            shape5.SetFill(255, 255, 255);

            var mask5 = Shape.Gen();
            mask5.AppendCircle(1125, 100, 50, 50);
            mask5.SetFill(255, 255, 255, 200);

            var light = Shape.Gen();
            light.AppendCircle(1150, 100, 50, 50);
            light.SetFill(255, 255, 255);
            mask5.SetMask(light, MaskMethod.Lighten);
            shape5.SetMask(mask5, MaskMethod.Alpha);
            canvas.Add(shape5);

            //Shape + Shape Mask Darken
            var shape6 = Shape.Gen();
            shape6.AppendCircle(1375, 100, 150, 150);
            shape6.SetFill(255, 255, 255);

            var mask6 = Shape.Gen();
            mask6.AppendCircle(1375, 100, 50, 50);
            mask6.SetFill(255, 255, 255, 200);

            var dark = Shape.Gen();
            dark.AppendCircle(1400, 100, 50, 50);
            dark.SetFill(255, 255, 255);
            mask6.SetMask(dark, MaskMethod.Darken);
            shape6.SetMask(mask6, MaskMethod.Alpha);
            canvas.Add(shape6);
        }
        {
            //Shape + Shape Mask Add
            var shape = Shape.Gen();
            shape.AppendCircle(125, 300, 100, 100);
            shape.SetFill(255, 255, 255);

            var mask = Shape.Gen();
            mask.AppendCircle(125, 300, 50, 50);
            mask.SetFill(255, 255, 255);

            var add = Shape.Gen();
            add.AppendCircle(175, 300, 50, 50);
            add.SetFill(255, 255, 255);
            mask.SetMask(add, MaskMethod.Add);
            shape.SetMask(mask, MaskMethod.InvAlpha);
            canvas.Add(shape);

            //Shape + Shape Mask Subtract
            var shape2 = Shape.Gen();
            shape2.AppendCircle(375, 300, 100, 100);
            shape2.SetFill(255, 255, 255, 255);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(375, 300, 50, 50);
            mask2.SetFill(255, 255, 255, 127);

            var sub = Shape.Gen();
            sub.AppendCircle(400, 300, 50, 50);
            sub.SetFill(255, 255, 255);
            mask2.SetMask(sub, MaskMethod.Subtract);
            shape2.SetMask(mask2, MaskMethod.InvAlpha);
            canvas.Add(shape2);

            //Shape + Shape Mask Intersect
            var shape3 = Shape.Gen();
            shape3.AppendCircle(625, 300, 100, 100);
            shape3.SetFill(255, 255, 255, 127);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(625, 300, 50, 50);
            mask3.SetFill(255, 255, 255, 127);

            var inter = Shape.Gen();
            inter.AppendCircle(650, 300, 50, 50);
            inter.SetFill(255, 255, 255);
            mask3.SetMask(inter, MaskMethod.Intersect);
            shape3.SetMask(mask3, MaskMethod.InvAlpha);
            canvas.Add(shape3);

            //Shape + Shape Mask Difference
            var shape4 = Shape.Gen();
            shape4.AppendCircle(875, 300, 100, 100);
            shape4.SetFill(255, 255, 255);

            var mask4 = Shape.Gen();
            mask4.AppendCircle(875, 300, 50, 50);
            mask4.SetFill(255, 255, 255);

            var diff = Shape.Gen();
            diff.AppendCircle(900, 300, 50, 50);
            diff.SetFill(255, 255, 255);
            mask4.SetMask(diff, MaskMethod.Difference);
            shape4.SetMask(mask4, MaskMethod.InvAlpha);
            canvas.Add(shape4);

            //Shape + Shape Mask Lighten
            var shape5 = Shape.Gen();
            shape5.AppendCircle(1125, 300, 100, 100);
            shape5.SetFill(255, 255, 255);

            var mask5 = Shape.Gen();
            mask5.AppendCircle(1125, 300, 50, 50);
            mask5.SetFill(255, 255, 255, 200);

            var light = Shape.Gen();
            light.AppendCircle(1150, 300, 50, 50);
            light.SetFill(255, 255, 255);
            mask5.SetMask(light, MaskMethod.Lighten);
            shape5.SetMask(mask5, MaskMethod.InvAlpha);
            canvas.Add(shape5);

            //Shape + Shape Mask Darken
            var shape6 = Shape.Gen();
            shape6.AppendCircle(1375, 300, 100, 100);
            shape6.SetFill(255, 255, 255);

            var mask6 = Shape.Gen();
            mask6.AppendCircle(1375, 300, 50, 50);
            mask6.SetFill(255, 255, 255, 200);

            var dark = Shape.Gen();
            dark.AppendCircle(1400, 300, 50, 50);
            dark.SetFill(255, 255, 255);
            mask6.SetMask(dark, MaskMethod.Darken);
            shape6.SetMask(mask6, MaskMethod.InvAlpha);
            canvas.Add(shape6);
        }
        {
            //Rect + Rect Mask Add
            var shape = Shape.Gen();
            shape.AppendRect(75, 450, 150, 150);
            shape.SetFill(255, 255, 255);

            var mask = Shape.Gen();
            mask.AppendRect(75, 500, 100, 100);
            mask.SetFill(255, 255, 255);

            var add = Shape.Gen();
            add.AppendRect(125, 450, 100, 100);
            add.SetFill(255, 255, 255);
            mask.SetMask(add, MaskMethod.Add);
            shape.SetMask(mask, MaskMethod.Alpha);
            canvas.Add(shape);

            //Rect + Rect Mask Subtract
            var shape2 = Shape.Gen();
            shape2.AppendRect(325, 450, 150, 150);
            shape2.SetFill(255, 255, 255);

            var mask2 = Shape.Gen();
            mask2.AppendRect(325, 500, 100, 100);
            mask2.SetFill(255, 255, 255, 127);

            var sub = Shape.Gen();
            sub.AppendRect(375, 450, 100, 100);
            sub.SetFill(255, 255, 255);
            mask2.SetMask(sub, MaskMethod.Subtract);
            shape2.SetMask(mask2, MaskMethod.Alpha);
            canvas.Add(shape2);

            //Rect + Rect Mask Intersect
            var shape3 = Shape.Gen();
            shape3.AppendRect(575, 450, 150, 150);
            shape3.SetFill(255, 255, 255);

            var mask3 = Shape.Gen();
            mask3.AppendRect(575, 500, 100, 100);
            mask3.SetFill(255, 255, 255, 127);

            var inter = Shape.Gen();
            inter.AppendRect(625, 450, 100, 100);
            inter.SetFill(255, 255, 255);
            mask3.SetMask(inter, MaskMethod.Intersect);
            shape3.SetMask(mask3, MaskMethod.Alpha);
            canvas.Add(shape3);

            //Rect + Rect Mask Difference
            var shape4 = Shape.Gen();
            shape4.AppendRect(825, 450, 150, 150);
            shape4.SetFill(255, 255, 255);

            var mask4 = Shape.Gen();
            mask4.AppendRect(825, 500, 100, 100);
            mask4.SetFill(255, 255, 255);

            var diff = Shape.Gen();
            diff.AppendRect(875, 450, 100, 100);
            diff.SetFill(255, 255, 255);
            mask4.SetMask(diff, MaskMethod.Difference);
            shape4.SetMask(mask4, MaskMethod.Alpha);
            canvas.Add(shape4);

            //Rect + Rect Mask Lighten
            var shape5 = Shape.Gen();
            shape5.AppendRect(1125, 450, 150, 150);
            shape5.SetFill(255, 255, 255);

            var mask5 = Shape.Gen();
            mask5.AppendRect(1125, 500, 100, 100);
            mask5.SetFill(255, 255, 255, 200);

            var light = Shape.Gen();
            light.AppendRect(1175, 450, 100, 100);
            light.SetFill(255, 255, 255);
            mask5.SetMask(light, MaskMethod.Lighten);
            shape5.SetMask(mask5, MaskMethod.Alpha);
            canvas.Add(shape5);

            //Rect + Rect Mask Darken
            var shape6 = Shape.Gen();
            shape6.AppendRect(1375, 450, 150, 150);
            shape6.SetFill(255, 255, 255);

            var mask6 = Shape.Gen();
            mask6.AppendRect(1375, 500, 100, 100);
            mask6.SetFill(255, 255, 255, 200);

            var dark = Shape.Gen();
            dark.AppendRect(1400, 450, 100, 100);
            dark.SetFill(255, 255, 255);
            mask6.SetMask(dark, MaskMethod.Darken);
            shape6.SetMask(mask6, MaskMethod.Alpha);
            canvas.Add(shape6);
        }
        {
            //Transformed Image + Shape Mask Add
            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image.Translate(150, 650);
            image.Scale(0.5f);
            image.Rotate(45);

            var mask = Shape.Gen();
            mask.AppendCircle(125, 700, 50, 50);
            mask.SetFill(255, 255, 255);

            var add = Shape.Gen();
            add.AppendCircle(150, 750, 50, 50);
            add.SetFill(255, 255, 255);
            mask.SetMask(add, MaskMethod.Add);
            image.SetMask(mask, MaskMethod.Alpha);
            canvas.Add(image);

            //Transformed Image + Shape Mask Subtract
            var image2 = Picture.Gen();
            if (!Verify(image2.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image2.Translate(400, 650);
            image2.Scale(0.5f);
            image2.Rotate(45);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(375, 700, 50, 50);
            mask2.SetFill(255, 255, 255, 127);

            var sub = Shape.Gen();
            sub.AppendCircle(400, 750, 50, 50);
            sub.SetFill(255, 255, 255);
            mask2.SetMask(sub, MaskMethod.Subtract);
            image2.SetMask(mask2, MaskMethod.Alpha);
            canvas.Add(image2);

            //Transformed Image + Shape Mask Intersect
            var image3 = Picture.Gen();
            if (!Verify(image3.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image3.Translate(650, 650);
            image3.Scale(0.5f);
            image3.Rotate(45);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(625, 700, 50, 50);
            mask3.SetFill(255, 255, 255, 127);

            var inter = Shape.Gen();
            inter.AppendCircle(650, 750, 50, 50);
            inter.SetFill(255, 255, 255, 127);
            mask3.SetMask(inter, MaskMethod.Intersect);
            image3.SetMask(mask3, MaskMethod.Alpha);
            canvas.Add(image3);

            //Transformed Image + Shape Mask Difference
            var image4 = Picture.Gen();
            if (!Verify(image4.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image4.Translate(900, 650);
            image4.Scale(0.5f);
            image4.Rotate(45);

            var mask4 = Shape.Gen();
            mask4.AppendCircle(875, 700, 50, 50);
            mask4.SetFill(255, 255, 255);

            var diff = Shape.Gen();
            diff.AppendCircle(900, 750, 50, 50);
            diff.SetFill(255, 255, 255);
            mask4.SetMask(diff, MaskMethod.Difference);
            image4.SetMask(mask4, MaskMethod.Alpha);
            canvas.Add(image4);

            //Transformed Image + Shape Mask Lighten
            var image5 = Picture.Gen();
            if (!Verify(image5.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image5.Translate(1150, 650);
            image5.Scale(0.5f);
            image5.Rotate(45);

            var mask5 = Shape.Gen();
            mask5.AppendCircle(1125, 700, 50, 50);
            mask5.SetFill(255, 255, 255, 200);

            var light = Shape.Gen();
            light.AppendCircle(1150, 750, 50, 50);
            light.SetFill(255, 255, 255);
            mask5.SetMask(light, MaskMethod.Lighten);
            image5.SetMask(mask5, MaskMethod.Alpha);
            canvas.Add(image5);

            //Transformed Image + Shape Mask Darken
            var image6 = Picture.Gen();
            if (!Verify(image6.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image6.Translate(1400, 650);
            image6.Scale(0.5f);
            image6.Rotate(45);

            var mask6 = Shape.Gen();
            mask6.AppendCircle(1375, 700, 50, 50);
            mask6.SetFill(255, 255, 255, 200);

            var dark = Shape.Gen();
            dark.AppendCircle(1400, 750, 50, 50);
            dark.SetFill(255, 255, 255);
            mask6.SetMask(dark, MaskMethod.Darken);
            image6.SetMask(mask6, MaskMethod.Alpha);
            canvas.Add(image6);
        }
        {
            //Transformed Image + Shape Mask Add
            var image = Picture.Gen();
            if (!Verify(image.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image.Translate(150, 850);
            image.Scale(0.5f);
            image.Rotate(45);

            var mask = Shape.Gen();
            mask.AppendCircle(125, 900, 50, 50);
            mask.SetFill(255, 255, 255);

            var add = Shape.Gen();
            add.AppendCircle(150, 950, 50, 50);
            add.SetFill(255, 255, 255);
            mask.SetMask(add, MaskMethod.Add);
            image.SetMask(mask, MaskMethod.InvAlpha);
            canvas.Add(image);

            //Transformed Image + Shape Mask Subtract
            var image2 = Picture.Gen();
            if (!Verify(image2.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image2.Translate(400, 850);
            image2.Scale(0.5f);
            image2.Rotate(45);

            var mask2 = Shape.Gen();
            mask2.AppendCircle(375, 900, 50, 50);
            mask2.SetFill(255, 255, 255, 127);

            var sub = Shape.Gen();
            sub.AppendCircle(400, 950, 50, 50);
            sub.SetFill(255, 255, 255);
            mask2.SetMask(sub, MaskMethod.Subtract);
            image2.SetMask(mask2, MaskMethod.InvAlpha);
            canvas.Add(image2);

            //Transformed Image + Shape Mask Intersect
            var image3 = Picture.Gen();
            if (!Verify(image3.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image3.Translate(650, 850);
            image3.Scale(0.5f);
            image3.Rotate(45);

            var mask3 = Shape.Gen();
            mask3.AppendCircle(625, 900, 50, 50);
            mask3.SetFill(255, 255, 255, 127);

            var inter = Shape.Gen();
            inter.AppendCircle(650, 950, 50, 50);
            inter.SetFill(255, 255, 255, 127);
            mask3.SetMask(inter, MaskMethod.Intersect);
            image3.SetMask(mask3, MaskMethod.InvAlpha);
            canvas.Add(image3);

            //Transformed Image + Shape Mask Difference
            var image4 = Picture.Gen();
            if (!Verify(image4.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image4.Translate(900, 850);
            image4.Scale(0.5f);
            image4.Rotate(45);

            var mask4 = Shape.Gen();
            mask4.AppendCircle(875, 900, 50, 50);
            mask4.SetFill(255, 255, 255);

            var diff = Shape.Gen();
            diff.AppendCircle(900, 950, 50, 50);
            diff.SetFill(255, 255, 255);
            mask4.SetMask(diff, MaskMethod.Difference);
            image4.SetMask(mask4, MaskMethod.InvAlpha);
            canvas.Add(image4);

            //Transformed Image + Shape Mask Lighten
            var image5 = Picture.Gen();
            if (!Verify(image5.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image5.Translate(1150, 850);
            image5.Scale(0.5f);
            image5.Rotate(45);

            var mask5 = Shape.Gen();
            mask5.AppendCircle(1125, 900, 50, 50);
            mask5.SetFill(255, 255, 255, 200);

            var light = Shape.Gen();
            light.AppendCircle(1150, 950, 50, 50);
            light.SetFill(255, 255, 255);
            mask5.SetMask(light, MaskMethod.Lighten);
            image5.SetMask(mask5, MaskMethod.InvAlpha);
            canvas.Add(image5);

            //Transformed Image + Shape Mask Darken
            var image6 = Picture.Gen();
            if (!Verify(image6.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            image6.Translate(1400, 850);
            image6.Scale(0.5f);
            image6.Rotate(45);

            var mask6 = Shape.Gen();
            mask6.AppendCircle(1375, 900, 50, 50);
            mask6.SetFill(255, 255, 255, 200);

            var dark = Shape.Gen();
            dark.AppendCircle(1400, 950, 50, 50);
            dark.SetFill(255, 255, 255);
            mask6.SetMask(dark, MaskMethod.Darken);
            image6.SetMask(mask6, MaskMethod.InvAlpha);
            canvas.Add(image6);
        }
        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: false, w: 1500, h: 1024);
}
