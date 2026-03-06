using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Create a Scene
        var scene = Scene.Gen();

        //Prepare Round Rectangle
        var shape1 = Shape.Gen();
        shape1.AppendRect(0, 0, 400, 400, 50, 50);  //x, y, w, h, rx, ry
        shape1.SetFill(0, 255, 0);                   //r, g, b
        scene.Add(shape1);

        //Prepare Circle
        var shape2 = Shape.Gen();
        shape2.AppendCircle(400, 400, 200, 200);    //cx, cy, radiusW, radiusH
        shape2.SetFill(255, 255, 0);                 //r, g, b
        scene.Add(shape2);

        //Prepare Ellipse
        var shape3 = Shape.Gen();
        shape3.AppendCircle(600, 600, 150, 100);    //cx, cy, radiusW, radiusH
        shape3.SetFill(0, 255, 255);                 //r, g, b
        scene.Add(shape3);

        //Create another Scene
        var scene2 = Scene.Gen();

        //Star
        var shape4 = Shape.Gen();

        //Appends Paths
        shape4.MoveTo(199, 34);
        shape4.LineTo(253, 143);
        shape4.LineTo(374, 160);
        shape4.LineTo(287, 244);
        shape4.LineTo(307, 365);
        shape4.LineTo(199, 309);
        shape4.LineTo(97, 365);
        shape4.LineTo(112, 245);
        shape4.LineTo(26, 161);
        shape4.LineTo(146, 143);
        shape4.Close();
        shape4.SetFill(0, 0, 255);
        scene2.Add(shape4);

        //Circle
        var shape5 = Shape.Gen();

        var cx = 550.0f;
        var cy = 550.0f;
        var radius = 125.0f;
        var halfRadius = radius * 0.552284f;

        //Append Paths
        shape5.MoveTo(cx, cy - radius);
        shape5.CubicTo(cx + halfRadius, cy - radius, cx + radius, cy - halfRadius, cx + radius, cy);
        shape5.CubicTo(cx + radius, cy + halfRadius, cx + halfRadius, cy + radius, cx, cy + radius);
        shape5.CubicTo(cx - halfRadius, cy + radius, cx - radius, cy + halfRadius, cx - radius, cy);
        shape5.CubicTo(cx - radius, cy - halfRadius, cx - halfRadius, cy - radius, cx, cy - radius);
        shape5.SetFill(255, 0, 0);
        scene2.Add(shape5);

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
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args);
}
