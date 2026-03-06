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
        if (!Verify(canvas.Remove())) return false;

        var progress = Progress(elapsed, 2.0f, true);  //play time 2 sec.

        //Shape1
        var shape = Shape.Gen();
        shape.AppendRect(-285, -300, 280, 280);
        shape.AppendRect(-145, -160, 380, 380, 100, 100);
        shape.AppendCircle(155, 140, 140, 140);
        shape.AppendCircle(235, 320, 210, 140);
        shape.SetFill(255, 255, 255);
        shape.Translate(425, 480);
        shape.Scale(1.0f - 0.75f * progress);
        shape.Rotate(360 * progress);

        canvas.Add(shape);

        //Shape2
        var shape2 = Shape.Gen();
        shape2.AppendRect(-50, -50, 180, 180);
        shape2.SetFill(0, 255, 255);
        shape2.Translate(480, 480);
        shape2.Rotate(360 * progress);
        shape2.Translate(400 + progress * 300, 400);
        canvas.Add(shape2);

        //Shape3
        var shape3 = Shape.Gen();

        /* Look, how shape3's origin is different with shape2
        The center of the shape is the anchor point for transformation. */
        shape3.AppendRect(100, 100, 230, 130, 20, 20);
        shape3.SetFill(255, 0, 255);
        shape3.Translate(560, 560);
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
