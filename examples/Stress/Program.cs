using System;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    Random rng = new Random();

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Add random half-translucent rects with stroking
        for (int i = 0; i < 2000; ++i)
        {
            var s = Shape.Gen();
            s.AppendRect(rng.Next() % 1600, rng.Next() % 1600, rng.Next() % 100, rng.Next() % 100, rng.Next() % 10, rng.Next() % 10);
            s.SetFill((byte)(rng.Next() % 255), (byte)(rng.Next() % 255), (byte)(rng.Next() % 255), (byte)(253 + rng.Next() % 3));
            s.StrokeFill(255, 255, 255);
            s.StrokeWidth(3);
            canvas.Add(s);
        }
        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        //change rects' pos and size
        foreach (var p in canvas.GetPaints())
        {
            var s = (Shape)p;
            s.ResetShape();
            s.AppendRect(rng.Next() % 1600, rng.Next() % 1600, rng.Next() % 100, rng.Next() % 100, rng.Next() % 10, rng.Next() % 10);
        }
        return canvas.Update() == Result.Success;
    }
}

/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 1650, 1650, 4, true);
}
