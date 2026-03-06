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
        //Duplicate Shapes
        {
            //Original Shape
            var shape1 = Shape.Gen();
            shape1.AppendRect(10, 10, 200, 200);
            shape1.AppendRect(220, 10, 100, 100);

            shape1.StrokeWidth(3);
            shape1.StrokeFill(0, 255, 0);

            float[] dashPattern = { 4, 4 };
            shape1.StrokeDash(dashPattern, 2);
            shape1.SetFill(255, 0, 0);

            //Duplicate Shape, Switch fill method
            var shape2 = (Shape)shape1.Duplicate();
            shape2.Translate(0, 220);

            var fill = LinearGradient.Gen();
            fill.Linear(10, 10, 440, 200);

            Fill.ColorStop[] colorStops = new Fill.ColorStop[2];
            colorStops[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops[1] = new Fill.ColorStop(1, 255, 255, 255, 255);
            fill.SetColorStops(colorStops, 2);

            shape2.SetFill(fill);

            //Duplicate Shape 2
            var shape3 = shape2.Duplicate();
            shape3.Translate(0, 440);

            canvas.Add(shape1);
            canvas.Add(shape2);
            canvas.Add(shape3);
        }

        //Duplicate Scene
        {
            //Create a Scene1
            var scene1 = Scene.Gen();

            var shape1 = Shape.Gen();
            shape1.AppendRect(0, 0, 400, 400, 50, 50);
            shape1.SetFill(0, 255, 0);
            scene1.Add(shape1);

            var shape2 = Shape.Gen();
            shape2.AppendCircle(400, 400, 200, 200);
            shape2.SetFill(255, 255, 0);
            scene1.Add(shape2);

            var shape3 = Shape.Gen();
            shape3.AppendCircle(600, 600, 150, 100);
            shape3.SetFill(0, 255, 255);
            scene1.Add(shape3);

            scene1.Scale(0.25f);
            scene1.Translate(400, 0);

            //Duplicate Scene1
            var scene2 = scene1.Duplicate();
            scene2.Translate(600, 0);

            canvas.Add(scene1);
            canvas.Add(scene2);
        }

        //Duplicate Picture - svg
        {
            var picture1 = Picture.Gen();
            if (!Verify(picture1.Load(ExamplePaths.ExampleDir + "/svg/2684.svg"))) return false;
            picture1.Translate(350, 200);
            picture1.Scale(4);

            var picture2 = picture1.Duplicate();
            picture2.Translate(550, 250);

            canvas.Add(picture1);
            canvas.Add(picture2);
        }

        //Duplicate Picture - raw
        {
            var path = ExamplePaths.ExampleDir + "/image/rawimage_200x300.raw";
            var bytes = File.ReadAllBytes(path);
            var data = new uint[200 * 300];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            var picture1 = Picture.Gen();
            if (!Verify(picture1.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
            picture1.Scale(0.8f);
            picture1.Translate(400, 450);

            var picture2 = picture1.Duplicate();
            picture2.Translate(600, 550);
            picture2.Scale(0.7f);
            picture2.Rotate(8);

            canvas.Add(picture1);
            canvas.Add(picture2);
        }

        //Duplicate Text
        {
            var text = Text.Gen();
            if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"))) return false;
            text.SetFont("PublicSans-Regular");
            text.SetFontSize(50);
            text.Translate(0, 650);
            text.SetText("ThorVG Text");
            text.SetFill(100, 100, 255);

            var text2 = text.Duplicate();
            text2.Translate(0, 700);

            canvas.Add(text);
            canvas.Add(text2);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args);
}
