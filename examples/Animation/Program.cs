using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    Animation? animation;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //The default font for fallback in case
        Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf");

        //Animation Controller
        animation = Animation.Gen();
        var picture = animation.GetPicture();
        picture.SetOrigin(0.5f, 0.5f);  //center origin

        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(50, 50, 50);

        canvas.Add(shape);

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/sample.json"))) return false;

        //image scaling preserving its aspect ratio
        float w2, h2;
        picture.GetSize(out w2, out h2);
        var scale = (w2 > h2) ? w / w2 : h / h2;
        picture.Scale(scale);
        picture.Translate((float)w * 0.5f, (float)h * 0.5f);

        canvas.Add(picture);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, animation!.Duration());

        //Update animation frame only when it's changed
        if (animation.Frame(animation.TotalFrame() * progress) == Result.Success)
        {
            canvas.Update();
            return true;
        }

        return false;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1024, 1024, 4, true);
}
