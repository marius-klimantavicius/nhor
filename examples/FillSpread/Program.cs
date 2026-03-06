using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        const int colorCnt = 4;
        var colorStops = new Fill.ColorStop[colorCnt];
        colorStops[0] = new Fill.ColorStop(0.0f, 127, 39, 255, 255);
        colorStops[1] = new Fill.ColorStop(0.33f, 159, 112, 253, 255);
        colorStops[2] = new Fill.ColorStop(0.66f, 253, 191, 96, 255);
        colorStops[3] = new Fill.ColorStop(1.0f, 255, 137, 17, 255);

        //Radial grad
        {
            float x1, y1 = 80.0f, r = 120.0f;

            //Pad
            x1 = 20.0f;
            var shape1 = Shape.Gen();
            shape1.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill1 = RadialGradient.Gen();
            fill1.Radial(x1 + r, y1 + r, 40.0f, x1 + r, y1 + r, 0.0f);
            fill1.SetColorStops(colorStops, colorCnt);
            fill1.SetSpread(FillSpread.Pad);
            shape1.SetFill(fill1);

            canvas.Add(shape1);

            //Reflect
            x1 = 280.0f;
            var shape2 = Shape.Gen();
            shape2.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill2 = RadialGradient.Gen();
            fill2.Radial(x1 + r, y1 + r, 40.0f, x1 + r, y1 + r, 0.0f);
            fill2.SetColorStops(colorStops, colorCnt);
            fill2.SetSpread(FillSpread.Reflect);
            shape2.SetFill(fill2);

            canvas.Add(shape2);

            //Repeat
            x1 = 540.0f;
            var shape3 = Shape.Gen();
            shape3.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill3 = RadialGradient.Gen();
            fill3.Radial(x1 + r, y1 + r, 40.0f, x1 + r, y1 + r, 0.0f);
            fill3.SetColorStops(colorStops, colorCnt);
            fill3.SetSpread(FillSpread.Repeat);
            shape3.SetFill(fill3);

            canvas.Add(shape3);
        }

        //Linear grad
        {
            float x1, y1 = 480.0f, r = 120.0f;

            //Pad
            x1 = 20.0f;
            var shape1 = Shape.Gen();
            shape1.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill1 = LinearGradient.Gen();
            fill1.Linear(x1, y1, x1 + 50.0f, y1 + 50.0f);
            fill1.SetColorStops(colorStops, colorCnt);
            fill1.SetSpread(FillSpread.Pad);
            shape1.SetFill(fill1);

            canvas.Add(shape1);

            //Reflect
            x1 = 280.0f;
            var shape2 = Shape.Gen();
            shape2.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill2 = LinearGradient.Gen();
            fill2.Linear(x1, y1, x1 + 50.0f, y1 + 50.0f);
            fill2.SetColorStops(colorStops, colorCnt);
            fill2.SetSpread(FillSpread.Reflect);
            shape2.SetFill(fill2);

            canvas.Add(shape2);

            //Repeat
            x1 = 540.0f;
            var shape3 = Shape.Gen();
            shape3.AppendRect(x1, y1, 2.0f * r, 2.0f * r);

            var fill3 = LinearGradient.Gen();
            fill3.Linear(x1, y1, x1 + 50.0f, y1 + 50.0f);
            fill3.SetColorStops(colorStops, colorCnt);
            fill3.SetSpread(FillSpread.Repeat);
            shape3.SetFill(fill3);

            canvas.Add(shape3);

            return true;
        }
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args);
}
