using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    Shape? solid = null;
    Shape? gradient = null;

    uint w, h;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Shape (for BG)
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, w, h);
        bg.SetFill(255, 255, 255);
        canvas.Add(bg);

        //Solid Shape
        {
            solid = Shape.Gen();
            solid.AppendRect(-100, -100, 200, 200);

            //fill property will be retained
            solid.SetFill(127, 255, 255);
            solid.StrokeFill(0, 0, 255);
            solid.StrokeWidth(1);

            canvas.Add(solid);
        }

        //Gradient Shape
        {
            gradient = Shape.Gen();
            gradient.AppendRect(w - 200, 0, 200, 200);

            //LinearGradient
            var fill = LinearGradient.Gen();
            fill.Linear(w - 200, 0, w - 200 + 285, 300);

            //Gradient Color Stops
            var colorStops = new Fill.ColorStop[3];
            colorStops[0] = new Fill.ColorStop(0, 255, 0, 0, 127);
            colorStops[1] = new Fill.ColorStop(0.5f, 255, 255, 0, 127);
            colorStops[2] = new Fill.ColorStop(1, 255, 255, 255, 127);

            fill.SetColorStops(colorStops, 3);
            gradient.SetFill(fill);

            canvas.Add(gradient);
        }

        this.w = w;
        this.h = h;

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, 2.0f, true);  //play time 2 sec.

        //Reset Shape
        if (Verify(solid!.ResetShape()))
        {
            //Solid Shape
            solid.AppendRect(-100 + (w * progress), -100 + (h * progress), 200, 200, (100 * progress), (100 * progress));
            solid.StrokeWidth(30 * progress);

            //Gradient Shape
            gradient!.Translate(-(w * progress), (h * progress));

            canvas.Update();

            return true;
        }

        return false;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 960, 960);
}
