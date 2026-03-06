using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        return Update(canvas, 0);
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        Verify(canvas.Remove());

        var progress = Progress(elapsed, 2.0f, true);  //play time 2 sec.

        //Shape1
        var shape = Shape.Gen();
        shape.AppendRect(-285, -300, 280, 280);
        shape.AppendRect(-145, -160, 380, 380, 100, 100);
        shape.AppendCircle(195, 180, 140, 140);
        shape.AppendCircle(235, 320, 210, 140);

        //LinearGradient
        var fill = LinearGradient.Gen();
        fill.Linear(-285, -300, 285, 300);

        //Gradient Color Stops
        var colorStops = new Fill.ColorStop[3];
        colorStops[0] = new Fill.ColorStop(0, 255, 0, 0, 255);
        colorStops[1] = new Fill.ColorStop(0.5f, 255, 255, 0, 255);
        colorStops[2] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill.SetColorStops(colorStops, 3);
        shape.SetFill(fill);
        shape.Translate(385, 400);

        //Update Shape1
        shape.Scale(1.0f - 0.75f * progress);
        shape.Rotate(360.0f * progress);

        canvas.Add(shape);

        //Shape2
        var shape2 = Shape.Gen();
        shape2.AppendRect(-50, -50, 180, 180);
        shape2.Translate(480, 480);

        //LinearGradient
        var fill2 = LinearGradient.Gen();
        fill2.Linear(-50, -50, 130, 130);

        //Gradient Color Stops
        var colorStops2 = new Fill.ColorStop[2];
        colorStops2[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
        colorStops2[1] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill2.SetColorStops(colorStops2, 2);
        shape2.SetFill(fill2);

        shape2.Rotate(360 * progress);
        shape2.Translate(480 + progress * 300, 480);

        canvas.Add(shape2);

        //Shape3
        var shape3 = Shape.Gen();
        /* Look, how shape3's origin is different with shape2
        The center of the shape is the anchor point for transformation. */
        shape3.AppendRect(100, 100, 150, 100, 20, 20);

        //RadialGradient
        var fill3 = RadialGradient.Gen();
        fill3.Radial(175, 150, 75, 175, 150, 0);

        //Gradient Color Stops
        var colorStops3 = new Fill.ColorStop[4];
        colorStops3[0] = new Fill.ColorStop(0, 0, 127, 0, 127);
        colorStops3[1] = new Fill.ColorStop(0.25f, 0, 170, 170, 170);
        colorStops3[2] = new Fill.ColorStop(0.5f, 200, 0, 200, 200);
        colorStops3[3] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill3.SetColorStops(colorStops3, 4);

        shape3.SetFill(fill3);
        shape3.Translate(480, 480);

        //Update Shape3
        shape3.Rotate(-360.0f * progress);
        shape3.Scale(0.5f + progress);

        canvas.Add(shape3);

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: true, w: 960, h: 960);
}
