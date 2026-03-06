using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    private uint last = 0;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Prepare Round Rectangle
        var shape1 = Shape.Gen();
        shape1.AppendRect(0, 0, 480, 480, 50, 50);  //x, y, w, h, rx, ry
        shape1.SetFill(0, 255, 0);                   //r, g, b
        canvas.Add(shape1);

        //Prepare Round Rectangle2
        var shape2 = Shape.Gen();
        shape2.AppendRect(140, 140, 480, 480, 50, 50);  //x, y, w, h, rx, ry
        shape2.SetFill(255, 255, 0);                     //r, g, b
        canvas.Add(shape2);

        //Prepare Round Rectangle3
        var shape3 = Shape.Gen();
        shape3.AppendRect(280, 280, 480, 480, 50, 50);  //x, y, w, h, rx, ry
        shape3.SetFill(0, 255, 255);                     //r, g, b
        canvas.Add(shape3);

        //Prepare Scene
        var scene = Scene.Gen();

        var shape4 = Shape.Gen();
        shape4.AppendCircle(520, 520, 140, 140);
        shape4.SetFill(255, 0, 0);
        shape4.StrokeWidth(5);
        shape4.StrokeFill(255, 255, 255);
        scene.Add(shape4);

        var shape5 = Shape.Gen();
        shape5.AppendCircle(630, 630, 190, 190);
        shape5.SetFill(255, 0, 255);
        shape5.StrokeWidth(5);
        shape5.StrokeFill(255, 255, 255);
        scene.Add(shape5);

        canvas.Add(scene);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        //update per every 250ms
        //reorder with a circular list
        if (elapsed - last < 250) return false;

        //Acquire the first paint from the root scene
        var paints = canvas.GetPaints();
        var paint = paints[0];

        //Prevent deleting from canvas.Remove()
        paint.Ref();

        //Add again the front paint to the end of the root scene
        Verify(canvas.Remove(paint));
        Verify(canvas.Add(paint));

        //Make it pair ref() - unref()
        paint.Unref();

        last = elapsed;

        canvas.Update();

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
