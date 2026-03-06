using System;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    LottieAnimation? lottie;

    Point down, prv;
    Point origin;
    float rotation = 0.0f;
    uint time = 0;
    float scale = 1.0f;
    bool pressed = false;

    uint effectDuration = 2000;  //2secs
    float effectTarget = 0.0f;
    uint effectTime = 0;
    bool effectOn = false;

    float Calculate(Point prv, Point cur)
    {
        //degree with dot product
        var degree = (float)Math.Acos((prv.x * cur.x + prv.y * cur.y) / (Math.Sqrt(prv.x * prv.x + prv.y * prv.y) * Math.Sqrt(cur.x * cur.x + cur.y * cur.y)));
        degree *= 30.0f;  //weight x30

        //direction with cross product
        var dir = (prv.x * cur.y - (float)prv.y * (float)cur.x);
        if (dir < 0) degree *= -1.0f;

        return degree;
    }

    public override bool ClickDown(Canvas canvas, int x, int y)
    {
        down = new Point(x, y);
        prv = new Point(x - origin.x, y - origin.y);
        time = Elapsed;
        pressed = true;
        effectOn = false;
        effectTarget = rotation;
        return false;
    }

    public override bool ClickUp(Canvas canvas, int x, int y)
    {
        pressed = false;

        //flicking in 500ms
        if (Elapsed - time > 500) return false;
        if (Math.Abs(down.x - x) < 10 && Math.Abs(down.y - y) < 10) return false;

        var cur = new Point(x - origin.x, y - origin.y);
        var p = new Point(down.x - origin.x, down.y - origin.y);

        effectTarget = rotation + Calculate(p, cur) * 20.0f;    //target to spinning effect
        effectTime = Elapsed;
        effectOn = true;

        return false;
    }

    public override bool Motion(Canvas canvas, int x, int y)
    {
        //update cursor
        Verify(lottie!.Assign("FingerCursor", 3, "ct_xcoord", (float)x / scale));
        Verify(lottie.Assign("FingerCursor", 3, "ct_ycoord", (float)y / scale));

        if (!pressed) return false;

        var cur = new Point(x - origin.x, y - origin.y);

        rotation = (rotation + Calculate(prv, cur)) % 360.0f;   //current rotation

        Verify(lottie.Assign("SpinBoard", 10, "ct_val", rotation));

        prv = cur;

        return true;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //LottieAnimation Controller
        lottie = LottieAnimation.Gen();
        var picture = lottie.GetPicture();
        picture.SetOrigin(0.5f, 0.5f);

        //Lottie Boundary
        {
            var shape = Shape.Gen();
            shape.AppendRect(100, 100, w - 200, h - 200);
            shape.SetFill(50, 50, 50);
            canvas.Add(shape);
        }

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/spin.json"))) return false;

        //image scaling preserving its aspect ratio
        float w2, h2;
        picture.GetSize(out w2, out h2);
        scale = ((w2 > h2) ? w / w2 : h / h2) * 0.8f;
        picture.Scale(scale);
        picture.Translate((float)w * 0.5f, (float)h * 0.5f);

        canvas.Add(picture);

        origin.x = (float)(w / 2);
        origin.y = (float)(h / 2);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        //spinning effect
        if (effectOn)
        {
            var elapsedTime = elapsed - effectTime;
            var progress = (float)elapsedTime / (float)effectDuration;
            if (progress >= 1.0f)
            {
                progress = 1.0f;
                effectOn = false;
            }
            rotation = (effectTarget * (float)Math.Sin(progress)) % 360.0f;
            Verify(lottie!.Assign("SpinBoard", 10, "ct_val", rotation));
        }

        var p = Progress(elapsed, lottie!.Duration());

        //Update animation frame only when it's changed
        lottie.Frame(lottie.TotalFrame() * p);
        canvas.Update();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 1024, 1024, 0);
}
