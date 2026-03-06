using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Prepare Round Rectangle
        var shape1 = Shape.Gen();
        shape1.AppendRect(0, 0, 400, 400);    //x, y, w, h

        //LinearGradient
        var fill = LinearGradient.Gen();
        fill.Linear(0, 0, 400, 400);

        //Gradient Color Stops
        var colorStops = new Fill.ColorStop[2];
        colorStops[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
        colorStops[1] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill.SetColorStops(colorStops, 2);

        shape1.SetFill(fill);
        canvas.Add(shape1);

        //Prepare Circle
        var shape2 = Shape.Gen();
        shape2.AppendCircle(400, 400, 200, 200);    //cx, cy, radiusW, radiusH

        //LinearGradient
        var fill2 = LinearGradient.Gen();
        fill2.Linear(400, 200, 400, 600);

        //Gradient Color Stops
        var colorStops2 = new Fill.ColorStop[3];
        colorStops2[0] = new Fill.ColorStop(0, 255, 0, 0, 255);
        colorStops2[1] = new Fill.ColorStop(0.5f, 255, 255, 0, 255);
        colorStops2[2] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill2.SetColorStops(colorStops2, 3);

        shape2.SetFill(fill2);
        canvas.Add(shape2);

        //Prepare Ellipse
        var shape3 = Shape.Gen();
        shape3.AppendCircle(600, 600, 150, 100);    //cx, cy, radiusW, radiusH

        //LinearGradient
        var fill3 = LinearGradient.Gen();
        fill3.Linear(450, 600, 750, 600);

        //Gradient Color Stops
        var colorStops3 = new Fill.ColorStop[4];
        colorStops3[0] = new Fill.ColorStop(0, 0, 127, 0, 127);
        colorStops3[1] = new Fill.ColorStop(0.25f, 0, 170, 170, 170);
        colorStops3[2] = new Fill.ColorStop(0.5f, 200, 0, 200, 200);
        colorStops3[3] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill3.SetColorStops(colorStops3, 4);

        shape3.SetFill(fill3);
        canvas.Add(shape3);

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
