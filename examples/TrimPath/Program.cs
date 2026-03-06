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
        shape1.AppendCircle(245, 125, 50, 120);
        shape1.AppendCircle(245, 365, 50, 120);
        shape1.AppendCircle(125, 245, 120, 50);
        shape1.AppendCircle(365, 245, 120, 50);
        shape1.SetFill(0, 50, 155, 100);
        shape1.StrokeFill(0, 0, 255);
        shape1.StrokeJoin(StrokeJoin.Round);
        shape1.StrokeCap(StrokeCap.Round);
        shape1.StrokeWidth(12);
        shape1.Trimpath(0.25f, 0.75f, false);

        var shape2 = (Shape)shape1.Duplicate();
        shape2.Translate(300, 300);
        shape2.SetFill(0, 155, 50, 100);
        shape2.StrokeFill(0, 255, 0);

        float[] dashPatterns = { 10, 20 };
        shape2.StrokeDash(dashPatterns, 2, 10);
        shape2.Trimpath(0.25f, 0.75f, true);

        canvas.Add(shape1);
        canvas.Add(shape2);

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
