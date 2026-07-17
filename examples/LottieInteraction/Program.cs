using System;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    LottieAnimation? lottie;

    uint cursorSlot;
    uint rotationSlot;

    Point down, prv, cur;
    Point origin;
    float rotation;
    uint time;
    float scale = 1.0f;
    bool pressed;

    const uint EffectDuration = 2000;
    float effectTarget;
    uint effectTime;
    bool effectOn;

    static float Calculate(Point prv, Point cur)
    {
        // Degree with dot product.
        var degree = (float)Math.Acos((prv.x * cur.x + prv.y * cur.y) /
            (Math.Sqrt(prv.x * prv.x + prv.y * prv.y) * Math.Sqrt(cur.x * cur.x + cur.y * cur.y)));
        degree *= 30.0f;

        // Direction with cross product.
        if (prv.x * cur.y - prv.y * cur.x < 0) degree *= -1.0f;
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

        // Flicking in 500ms.
        if (Elapsed - time > 500) return false;
        if (Math.Abs(down.x - x) < 10 && Math.Abs(down.y - y) < 10) return false;

        var current = new Point(x - origin.x, y - origin.y);
        var previous = new Point(down.x - origin.x, down.y - origin.y);

        effectTarget = rotation + Calculate(previous, current) * 20.0f;
        effectTime = Elapsed;
        effectOn = true;
        return false;
    }

    void Rotate(float value)
    {
        rotation = value;
        var json = FormattableString.Invariant($"{{\"spin_rotation\":{{\"p\":{{\"x\":\"var $bm_rt = {rotation};\"}}}}}}");
        lottie!.Del(rotationSlot);
        rotationSlot = lottie.Gen(json);
        Verify(lottie.Apply(rotationSlot));
    }

    public override bool Motion(Canvas canvas, int x, int y)
    {
        cur = new Point(x - origin.x, y - origin.y);
        if (!pressed) return false;

        Rotate((rotation + Calculate(prv, cur)) % 360.0f);
        prv = cur;
        return true;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        lottie = LottieAnimation.Gen();
        var picture = lottie.GetPicture();
        picture.SetOrigin(0.5f, 0.5f);

        var boundary = Shape.Gen();
        boundary.AppendRect(100, 100, w - 200, h - 200);
        boundary.SetFill(50, 50, 50);
        canvas.Add(boundary);

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/spin.json"))) return false;

        picture.GetSize(out var pictureWidth, out var pictureHeight);
        scale = ((pictureWidth > pictureHeight) ? w / pictureWidth : h / pictureHeight) * 0.8f;
        picture.Scale(scale);
        picture.Translate(w * 0.5f, h * 0.5f);
        canvas.Add(picture);

        origin = new Point(w * 0.5f, h * 0.5f);
        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        // Update the cursor slot.
        var wiggle = (float)Math.Sin(elapsed * 0.01f) * 20.0f + 320.0f;
        var cx = (cur.x + origin.x) / scale + wiggle;
        var cy = (cur.y + origin.y) / scale;
        var cursorJson = FormattableString.Invariant($"{{\"finger_cursor\":{{\"p\":{{\"x\":\"var $bm_rt; $bm_rt = [{cx}, {cy}];\"}}}}}}");
        lottie!.Del(cursorSlot);
        cursorSlot = lottie.Gen(cursorJson);
        Verify(lottie.Apply(cursorSlot));

        if (effectOn)
        {
            var progress = (float)(elapsed - effectTime) / EffectDuration;
            if (progress >= 1.0f)
            {
                progress = 1.0f;
                effectOn = false;
            }
            Rotate(effectTarget * (float)Math.Sin(progress) % 360.0f);
        }

        var frameProgress = Progress(elapsed, lottie.Duration());
        lottie.Frame(lottie.TotalFrame() * frameProgress);
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
