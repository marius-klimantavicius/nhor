using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //StrokeJoin & StrokeCap
        var shape1 = Shape.Gen();
        shape1.MoveTo(20, 50);
        shape1.LineTo(250, 50);
        shape1.LineTo(220, 200);
        shape1.LineTo(70, 170);
        shape1.LineTo(70, 30);
        shape1.StrokeFill(255, 0, 0);
        shape1.StrokeWidth(10);
        shape1.StrokeJoin(StrokeJoin.Round);
        shape1.StrokeCap(StrokeCap.Round);
        canvas.Add(shape1);

        var shape2 = Shape.Gen();
        shape2.MoveTo(270, 50);
        shape2.LineTo(500, 50);
        shape2.LineTo(470, 200);
        shape2.LineTo(320, 170);
        shape2.LineTo(320, 30);
        shape2.StrokeFill(255, 255, 0);
        shape2.StrokeWidth(10);
        shape2.StrokeJoin(StrokeJoin.Bevel);
        shape2.StrokeCap(StrokeCap.Square);
        canvas.Add(shape2);

        var shape3 = Shape.Gen();
        shape3.MoveTo(520, 50);
        shape3.LineTo(750, 50);
        shape3.LineTo(720, 200);
        shape3.LineTo(570, 170);
        shape3.LineTo(570, 30);
        shape3.StrokeFill(0, 255, 0);
        shape3.StrokeWidth(10);
        shape3.StrokeJoin(StrokeJoin.Miter);
        shape3.StrokeCap(StrokeCap.Butt);
        canvas.Add(shape3);

        //Stroke Dash
        var shape4 = Shape.Gen();
        shape4.MoveTo(20, 230);
        shape4.LineTo(250, 230);
        shape4.LineTo(220, 380);
        shape4.LineTo(70, 330);
        shape4.LineTo(70, 210);
        shape4.StrokeFill(255, 0, 0);
        shape4.StrokeWidth(5);
        shape4.StrokeJoin(StrokeJoin.Round);
        shape4.StrokeCap(StrokeCap.Round);

        float[] dashPattern1 = { 20, 10 };
        shape4.StrokeDash(dashPattern1, 2);
        canvas.Add(shape4);

        var shape5 = Shape.Gen();
        shape5.MoveTo(270, 230);
        shape5.LineTo(500, 230);
        shape5.LineTo(470, 380);
        shape5.LineTo(320, 330);
        shape5.LineTo(320, 210);
        shape5.StrokeFill(255, 255, 0);
        shape5.StrokeWidth(5);
        shape5.StrokeJoin(StrokeJoin.Bevel);
        shape5.StrokeCap(StrokeCap.Square);

        float[] dashPattern2 = { 10, 10 };
        shape5.StrokeDash(dashPattern2, 2);
        canvas.Add(shape5);

        var shape6 = Shape.Gen();
        shape6.MoveTo(520, 230);
        shape6.LineTo(750, 230);
        shape6.LineTo(720, 380);
        shape6.LineTo(570, 330);
        shape6.LineTo(570, 210);
        shape6.StrokeFill(0, 255, 0);
        shape6.StrokeWidth(5);
        shape6.StrokeJoin(StrokeJoin.Miter);
        shape6.StrokeCap(StrokeCap.Butt);

        float[] dashPattern3 = { 10, 10, 1, 8, 1, 10 };
        shape6.StrokeDash(dashPattern3, 6);
        canvas.Add(shape6);

        //Closed Shape Stroke
        var shape7 = Shape.Gen();
        shape7.MoveTo(70, 440);
        shape7.LineTo(230, 440);
        shape7.CubicTo(230, 535, 170, 590, 70, 590);
        shape7.Close();
        shape7.StrokeFill(255, 0, 0);
        shape7.StrokeWidth(15);
        shape7.StrokeJoin(StrokeJoin.Round);
        shape7.StrokeCap(StrokeCap.Round);
        canvas.Add(shape7);

        var shape8 = Shape.Gen();
        shape8.MoveTo(320, 440);
        shape8.LineTo(480, 440);
        shape8.CubicTo(480, 535, 420, 590, 320, 590);
        shape8.Close();
        shape8.StrokeFill(255, 255, 0);
        shape8.StrokeWidth(15);
        shape8.StrokeJoin(StrokeJoin.Bevel);
        shape8.StrokeCap(StrokeCap.Square);
        canvas.Add(shape8);

        var shape9 = Shape.Gen();
        shape9.MoveTo(570, 440);
        shape9.LineTo(730, 440);
        shape9.CubicTo(730, 535, 670, 590, 570, 590);
        shape9.Close();
        shape9.StrokeFill(0, 255, 0);
        shape9.StrokeWidth(15);
        shape9.StrokeJoin(StrokeJoin.Miter);
        shape9.StrokeCap(StrokeCap.Butt);
        canvas.Add(shape9);

        //Stroke Dash for Circle and Rect
        var shape10 = Shape.Gen();
        shape10.AppendCircle(70, 700, 20, 60);
        shape10.AppendRect(130, 650, 100, 80);
        shape10.StrokeFill(255, 0, 0);
        shape10.StrokeWidth(5);
        shape10.StrokeJoin(StrokeJoin.Round);
        shape10.StrokeCap(StrokeCap.Round);
        shape10.StrokeDash(dashPattern1, 2);
        canvas.Add(shape10);

        var shape11 = Shape.Gen();
        shape11.AppendCircle(320, 700, 20, 60);
        shape11.AppendRect(380, 650, 100, 80);
        shape11.StrokeFill(255, 255, 0);
        shape11.StrokeWidth(5);
        shape11.StrokeJoin(StrokeJoin.Bevel);
        shape11.StrokeCap(StrokeCap.Square);
        shape11.StrokeDash(dashPattern2, 2);
        canvas.Add(shape11);

        var shape12 = Shape.Gen();
        shape12.AppendCircle(570, 700, 20, 60);
        shape12.AppendRect(630, 650, 100, 80);
        shape12.StrokeFill(0, 255, 0);
        shape12.StrokeWidth(5);
        shape12.StrokeJoin(StrokeJoin.Miter);
        shape12.StrokeCap(StrokeCap.Butt);
        shape12.StrokeDash(dashPattern3, 6);
        canvas.Add(shape12);

        //Zero length Dashes
        float[] dashPattern = { 0, 20 };

        var shape13 = Shape.Gen();
        shape13.AppendCircle(70, 850, 20, 60);
        shape13.AppendRect(130, 800, 100, 80);
        shape13.StrokeFill(255, 0, 0);
        shape13.StrokeWidth(5);
        shape13.StrokeDash(dashPattern, 2);
        shape13.StrokeJoin(StrokeJoin.Round);
        shape13.StrokeCap(StrokeCap.Round);
        canvas.Add(shape13);

        var shape14 = Shape.Gen();
        shape14.AppendCircle(320, 850, 20, 60);
        shape14.AppendRect(380, 800, 100, 80);
        shape14.StrokeFill(255, 255, 0);
        shape14.StrokeWidth(5);
        shape14.StrokeDash(dashPattern, 2);
        shape14.StrokeJoin(StrokeJoin.Bevel);
        shape14.StrokeCap(StrokeCap.Square);
        canvas.Add(shape14);

        var shape15 = Shape.Gen();
        shape15.AppendCircle(570, 850, 20, 60);
        shape15.AppendRect(630, 800, 100, 80);
        shape15.StrokeFill(0, 255, 0);
        shape15.StrokeWidth(5);
        shape15.StrokeDash(dashPattern, 2);
        shape15.StrokeJoin(StrokeJoin.Miter);
        shape15.StrokeCap(StrokeCap.Butt);  //butt has no cap expansions, so no visible
        canvas.Add(shape15);

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, w: 800, h: 960);
}
