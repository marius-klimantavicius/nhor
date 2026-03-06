using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //BG
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, w, h);
        bg.SetFill(100, 100, 100);
        canvas.Add(bg);

        //Create a Scene
        var scene = Scene.Gen();
        scene.SetBlend(BlendMethod.Add);

        //Prepare Circle
        var shape1 = Shape.Gen();
        shape1.AppendCircle(400, 400, 250, 250);
        shape1.SetFill(255, 255, 0);
        scene.Add(shape1);

        //Round rectangle
        var shape2 = Shape.Gen();
        shape2.AppendRect(450, 100, 200, 200, 50, 50);
        shape2.SetFill(0, 255, 0);
        shape2.StrokeWidth(10);
        shape2.StrokeFill(255, 255, 255);
        scene.Add(shape2);

        //Draw the Scene onto the Canvas
        canvas.Add(scene);

        //Create a Scene 2
        var scene2 = Scene.Gen();
        scene2.Opacity(127);              //Apply opacity to scene (0 - 255)
        scene2.SetBlend(BlendMethod.Overlay);
        scene2.Scale(1.2f);

        //Star
        var shape3 = Shape.Gen();

        //Appends Paths
        shape3.MoveTo(199, 34);
        shape3.LineTo(253, 143);
        shape3.LineTo(374, 160);
        shape3.LineTo(287, 244);
        shape3.LineTo(307, 365);
        shape3.LineTo(199, 309);
        shape3.LineTo(97, 365);
        shape3.LineTo(112, 245);
        shape3.LineTo(26, 161);
        shape3.LineTo(146, 143);
        shape3.Close();
        shape3.SetFill(0, 0, 255);
        shape3.StrokeWidth(10);
        shape3.StrokeFill(255, 255, 255);
        shape3.Opacity(127);

        scene2.Add(shape3);

        //Circle
        var shape4 = Shape.Gen();

        var cx = 150.0f;
        var cy = 150.0f;
        var radius = 50.0f;
        var halfRadius = radius * 0.552284f;

        //Append Paths
        shape4.MoveTo(cx, cy - radius);
        shape4.CubicTo(cx + halfRadius, cy - radius, cx + radius, cy - halfRadius, cx + radius, cy);
        shape4.CubicTo(cx + radius, cy + halfRadius, cx + halfRadius, cy + radius, cx, cy + radius);
        shape4.CubicTo(cx - halfRadius, cy + radius, cx - radius, cy + halfRadius, cx - radius, cy);
        shape4.CubicTo(cx - radius, cy - halfRadius, cx - halfRadius, cy - radius, cx, cy - radius);
        shape4.Close();
        shape4.SetFill(255, 0, 0);
        shape4.StrokeWidth(10);
        shape4.StrokeFill(0, 0, 255);
        shape4.Opacity(200);
        shape4.Scale(3);
        scene2.Add(shape4);

        //Draw the Scene onto the Canvas
        canvas.Add(scene2);

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
