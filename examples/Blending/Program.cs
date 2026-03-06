using System.IO;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    void Blender(Canvas canvas, string name, BlendMethod method, float x, float y, uint[] data)
    {
        var text = Text.Gen();
        text.SetFont("PublicSans-Regular");
        text.SetFontSize(15);
        text.SetText(name);
        text.SetFill(255, 255, 255);
        text.Translate(x + 20, y);
        canvas.Add(text);

        //solid
        {
            var bottom = Shape.Gen();
            bottom.AppendRect(20.0f + x, 25.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            bottom.SetFill(255, 255, 0);
            canvas.Add(bottom);

            var top = Shape.Gen();
            top.AppendRect(45.0f + x, 50.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            top.SetFill(0, 255, 255);
            top.SetBlend(method);
            canvas.Add(top);
        }

        //solid (half transparent)
        {
            var bottom = Shape.Gen();
            bottom.AppendRect(170.0f + x, 25.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            bottom.SetFill(255, 255, 0, 127);
            canvas.Add(bottom);

            var top = Shape.Gen();
            top.AppendRect(195.0f + x, 50.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            top.SetFill(0, 255, 255, 127);
            top.SetBlend(method);
            canvas.Add(top);
        }

        //gradient blending
        {
            Fill.ColorStop[] colorStops = new Fill.ColorStop[2];
            colorStops[0] = new Fill.ColorStop(0, 255, 0, 255, 255);
            colorStops[1] = new Fill.ColorStop(1, 0, 255, 0, 127);

            var fill = LinearGradient.Gen();
            fill.Linear(325.0f + x, 25.0f + y, 425.0f + x, 125.0f + y);
            fill.SetColorStops(colorStops, 2);

            var bottom = Shape.Gen();
            bottom.AppendRect(325.0f + x, 25.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            bottom.SetFill(fill);
            canvas.Add(bottom);

            var fill2 = LinearGradient.Gen();
            fill2.Linear(350.0f + x, 50.0f + y, 450.0f + x, 150.0f + y);
            fill2.SetColorStops(colorStops, 2);

            var top = Shape.Gen();
            top.AppendRect(350.0f + x, 50.0f + y, 100.0f, 100.0f, 10.0f, 10.0f);
            top.SetFill(fill2);
            top.SetBlend(method);
            canvas.Add(top);
        }

        //image
        {
            var bottom = Picture.Gen();
            bottom.Load(data, 200, 300, ColorSpace.ARGB8888, true);
            bottom.Translate(475 + x, 25.0f + y);
            bottom.Scale(0.35f);
            canvas.Add(bottom);

            var top = bottom.Duplicate();
            top.Translate(500.0f + x, 50.0f + y);
            top.Rotate(-10.0f);
            top.SetBlend(method);
            canvas.Add(top);
        }

        //scene
        {
            var bottom = Picture.Gen();
            bottom.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            bottom.Translate(600.0f + x, 25.0f + y);
            bottom.Scale(0.11f);
            canvas.Add(bottom);

            var top = bottom.Duplicate();
            top.Translate(625.0f + x, 50.0f + y);
            top.SetBlend(method);
            canvas.Add(top);
        }

        //scene (half transparent)
        {
            var bottom = Picture.Gen();
            bottom.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            bottom.Translate(750.0f + x, 25.0f + y);
            bottom.Scale(0.11f);
            bottom.Opacity(127);
            canvas.Add(bottom);

            var top = bottom.Duplicate();
            top.Translate(775.0f + x, 50.0f + y);
            top.SetBlend(method);
            canvas.Add(top);
        }
    }


    public override bool Content(Canvas canvas, uint w, uint h)
    {
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"))) return false;

        //Prepare Image
        var path = ExamplePaths.ExampleDir + "/image/rawimage_200x300.raw";
        var bytes = File.ReadAllBytes(path);
        var data = new uint[200 * 300];
        System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

        Blender(canvas, "Normal", BlendMethod.Normal, 0.0f, 0.0f, data);
        Blender(canvas, "Multiply", BlendMethod.Multiply, 0.0f, 150.0f, data);
        Blender(canvas, "Screen", BlendMethod.Screen, 0.0f, 300.0f, data);
        Blender(canvas, "Overlay", BlendMethod.Overlay, 0.0f, 450.0f, data);
        Blender(canvas, "Darken", BlendMethod.Darken, 0.0f, 600.0f, data);
        Blender(canvas, "Lighten", BlendMethod.Lighten, 0.0f, 750.0f, data);
        Blender(canvas, "ColorDodge", BlendMethod.ColorDodge, 0.0f, 900.0f, data);
        Blender(canvas, "ColorBurn", BlendMethod.ColorBurn, 0.0f, 1050.0f, data);
        Blender(canvas, "HardLight", BlendMethod.HardLight, 0.0f, 1200.0f, data);

        Blender(canvas, "SoftLight", BlendMethod.SoftLight, 900.0f, 0.0f, data);
        Blender(canvas, "Difference", BlendMethod.Difference, 900.0f, 150.0f, data);
        Blender(canvas, "Exclusion", BlendMethod.Exclusion, 900.0f, 300.0f, data);
        Blender(canvas, "Hue", BlendMethod.Hue, 900.0f, 450.0f, data);
        Blender(canvas, "Saturation", BlendMethod.Saturation, 900.0f, 600.0f, data);
        Blender(canvas, "Color", BlendMethod.Color, 900.0f, 750.0f, data);
        Blender(canvas, "Luminosity", BlendMethod.Luminosity, 900.0f, 900.0f, data);
        Blender(canvas, "Add", BlendMethod.Add, 900.0f, 1050.0f, data);

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: true, w: 1800, h: 1380);
}
