using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Shape 1
        var shape1 = Shape.Gen();
        shape1.AppendRect(50, 50, 200, 200);
        shape1.SetFill(50, 50, 50);
        shape1.StrokeFill(255, 255, 255);            //color: r, g, b
        shape1.StrokeJoin(StrokeJoin.Bevel);          //default is Bevel
        shape1.StrokeWidth(10);                        //width: 10px

        canvas.Add(shape1);

        //Shape 2
        var shape2 = Shape.Gen();
        shape2.AppendRect(300, 50, 200, 200);
        shape2.SetFill(50, 50, 50);
        shape2.StrokeFill(255, 255, 255);
        shape2.StrokeJoin(StrokeJoin.Round);
        shape2.StrokeWidth(10);

        canvas.Add(shape2);

        //Shape 3
        var shape3 = Shape.Gen();
        shape3.AppendRect(550, 50, 200, 200);
        shape3.SetFill(50, 50, 50);
        shape3.StrokeFill(255, 255, 255);
        shape3.StrokeJoin(StrokeJoin.Miter);
        shape3.StrokeWidth(10);

        canvas.Add(shape3);

        //Shape 4
        var shape4 = Shape.Gen();
        shape4.AppendCircle(150, 400, 100, 100);
        shape4.SetFill(50, 50, 50);
        shape4.StrokeFill(255, 255, 255);
        shape4.StrokeWidth(1);

        canvas.Add(shape4);

        //Shape 5
        var shape5 = Shape.Gen();
        shape5.AppendCircle(400, 400, 100, 100);
        shape5.SetFill(50, 50, 50);
        shape5.StrokeFill(255, 255, 255);
        shape5.StrokeWidth(2);

        canvas.Add(shape5);

        //Shape 6
        var shape6 = Shape.Gen();
        shape6.AppendCircle(650, 400, 100, 100);
        shape6.SetFill(50, 50, 50);
        shape6.StrokeFill(255, 255, 255);
        shape6.StrokeWidth(4);

        canvas.Add(shape6);

        //Stroke width test
        for (int i = 0; i < 10; ++i)
        {
            var hline = Shape.Gen();
            hline.MoveTo(50, 550 + (25 * i));
            hline.LineTo(300, 550 + (25 * i));
            hline.StrokeFill(255, 255, 255);            //color: r, g, b
            hline.StrokeWidth(i + 1);                   //stroke width
            hline.StrokeCap(StrokeCap.Round);            //default is Square
            canvas.Add(hline);

            var vline = Shape.Gen();
            vline.MoveTo(500 + (25 * i), 550);
            vline.LineTo(500 + (25 * i), 780);
            vline.StrokeFill(255, 255, 255);            //color: r, g, b
            vline.StrokeWidth(i + 1);                   //stroke width
            vline.StrokeCap(StrokeCap.Round);            //default is Square
            canvas.Add(vline);
        }

        //Stroke cap test
        var line1 = Shape.Gen();
        line1.MoveTo(360, 580);
        line1.LineTo(450, 580);
        line1.StrokeFill(255, 255, 255);                //color: r, g, b
        line1.StrokeWidth(15);
        line1.StrokeCap(StrokeCap.Round);

        var line2 = (Shape)line1.Duplicate();
        var line3 = (Shape)line1.Duplicate();
        canvas.Add(line1);

        line2.StrokeCap(StrokeCap.Square);
        line2.Translate(0, 50);
        canvas.Add(line2);

        line3.StrokeCap(StrokeCap.Butt);
        line3.Translate(0, 100);
        canvas.Add(line3);

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
