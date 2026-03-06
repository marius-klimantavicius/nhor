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

        //Create a Scene1
        var scene = Scene.Gen();

        //Prepare Round Rectangle (Scene1)
        var shape1 = Shape.Gen();
        shape1.AppendRect(-235, -250, 400, 400, 50, 50);  //x, y, w, h, rx, ry
        shape1.SetFill(0, 255, 0);                         //r, g, b
        shape1.StrokeWidth(5);                              //width
        shape1.StrokeFill(255, 255, 255);                   //r, g, b
        scene.Add(shape1);

        //Prepare Circle (Scene1)
        var shape2 = Shape.Gen();
        shape2.AppendCircle(-165, -150, 200, 200);    //cx, cy, radiusW, radiusH
        shape2.SetFill(255, 255, 0);                   //r, g, b
        scene.Add(shape2);

        //Prepare Ellipse (Scene1)
        var shape3 = Shape.Gen();
        shape3.AppendCircle(265, 250, 150, 100);      //cx, cy, radiusW, radiusH
        shape3.SetFill(0, 255, 255);                   //r, g, b
        scene.Add(shape3);

        scene.Translate(430, 430);
        scene.Scale(0.7f);
        scene.Rotate(360 * progress);

        //Create Scene2
        var scene2 = Scene.Gen();

        //Star (Scene2)
        var shape4 = Shape.Gen();

        //Appends Paths
        shape4.MoveTo(0, -114.5f);
        shape4.LineTo(54, -5.5f);
        shape4.LineTo(175, 11.5f);
        shape4.LineTo(88, 95.5f);
        shape4.LineTo(108, 216.5f);
        shape4.LineTo(0, 160.5f);
        shape4.LineTo(-102, 216.5f);
        shape4.LineTo(-87, 96.5f);
        shape4.LineTo(-173, 12.5f);
        shape4.LineTo(-53, -5.5f);
        shape4.Close();
        shape4.SetFill(0, 0, 255, 127);
        shape4.StrokeWidth(3);                          //width
        shape4.StrokeFill(0, 0, 255);                   //r, g, b
        scene2.Add(shape4);

        //Circle (Scene2)
        var shape5 = Shape.Gen();

        var cx = -150.0f;
        var cy = -150.0f;
        var radius = 100.0f;
        var halfRadius = radius * 0.552284f;

        //Append Paths
        shape5.MoveTo(cx, cy - radius);
        shape5.CubicTo(cx + halfRadius, cy - radius, cx + radius, cy - halfRadius, cx + radius, cy);
        shape5.CubicTo(cx + radius, cy + halfRadius, cx + halfRadius, cy + radius, cx, cy + radius);
        shape5.CubicTo(cx - halfRadius, cy + radius, cx - radius, cy + halfRadius, cx - radius, cy);
        shape5.CubicTo(cx - radius, cy - halfRadius, cx - halfRadius, cy - radius, cx, cy - radius);
        shape5.Close();
        shape5.SetFill(255, 0, 0, 127);
        scene2.Add(shape5);

        scene2.Translate(500, 350);
        scene2.Rotate(360 * progress);

        //Add scene2 to the scene
        scene.Add(scene2);

        //Add the Scene to the Canvas
        canvas.Add(scene);

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
