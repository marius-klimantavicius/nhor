using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        var colorStops1 = new Fill.ColorStop[3];
        colorStops1[0] = new Fill.ColorStop(0, 255, 0, 0, 150);
        colorStops1[1] = new Fill.ColorStop(0.5f, 0, 0, 255, 150);
        colorStops1[2] = new Fill.ColorStop(1, 127, 0, 127, 150);

        var colorStops2 = new Fill.ColorStop[2];
        colorStops2[0] = new Fill.ColorStop(0.3f, 255, 0, 0, 255);
        colorStops2[1] = new Fill.ColorStop(1, 50, 0, 255, 155);

        var colorStops3 = new Fill.ColorStop[2];
        colorStops3[0] = new Fill.ColorStop(0, 0, 0, 255, 155);
        colorStops3[1] = new Fill.ColorStop(1, 0, 255, 0, 155);

        float[] dashPattern1 = { 15, 15 };

        // linear gradient stroke + linear gradient fill
        var shape1 = Shape.Gen();
        shape1.MoveTo(150, 100);
        shape1.LineTo(200, 100);
        shape1.LineTo(200, 150);
        shape1.LineTo(300, 150);
        shape1.LineTo(250, 200);
        shape1.LineTo(200, 200);
        shape1.LineTo(200, 250);
        shape1.LineTo(150, 300);
        shape1.LineTo(150, 200);
        shape1.LineTo(100, 200);
        shape1.LineTo(100, 150);
        shape1.Close();

        shape1.StrokeFill(0, 255, 0);
        shape1.StrokeWidth(20);
        shape1.StrokeJoin(StrokeJoin.Miter);
        shape1.StrokeCap(StrokeCap.Butt);

        var fillStroke1 = LinearGradient.Gen();
        fillStroke1.Linear(100, 100, 250, 250);
        fillStroke1.SetColorStops(colorStops1, 3);
        shape1.StrokeFill(fillStroke1);

        var fill1 = LinearGradient.Gen();
        fill1.Linear(100, 100, 250, 250);
        fill1.SetColorStops(colorStops1, 3);
        shape1.SetFill(fill1);

        canvas.Add(shape1);

        // radial gradient stroke + duplicate
        var shape2 = Shape.Gen();
        shape2.AppendCircle(600, 175, 100, 60);
        shape2.StrokeWidth(80);

        var fillStroke2 = RadialGradient.Gen();
        fillStroke2.Radial(600, 175, 100, 600, 175, 0);
        fillStroke2.SetColorStops(colorStops2, 2);
        shape2.StrokeFill(fillStroke2);

        var shape3 = (Shape)shape2.Duplicate();
        shape3.Translate(0, 200);

        var fillStroke3 = LinearGradient.Gen();
        fillStroke3.Linear(500, 115, 700, 235);
        fillStroke3.SetColorStops(colorStops3, 2);
        shape3.StrokeFill(fillStroke3);

        var shape4 = (Shape)shape2.Duplicate();
        shape4.Translate(0, 400);

        canvas.Add(shape2);
        canvas.Add(shape3);
        canvas.Add(shape4);

        // dashed gradient stroke
        var shape5 = Shape.Gen();
        shape5.AppendRect(100, 500, 300, 300, 50, 80);

        shape5.StrokeWidth(20);
        shape5.StrokeDash(dashPattern1, 2);
        shape5.StrokeCap(StrokeCap.Butt);
        var fillStroke5 = LinearGradient.Gen();
        fillStroke5.Linear(150, 450, 450, 750);
        fillStroke5.SetColorStops(colorStops3, 2);
        shape5.StrokeFill(fillStroke5);

        var fill5 = LinearGradient.Gen();
        fill5.Linear(150, 450, 450, 750);
        fill5.SetColorStops(colorStops3, 2);
        shape5.SetFill(fill5);
        shape5.Scale(0.8f);

        canvas.Add(shape5);

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
