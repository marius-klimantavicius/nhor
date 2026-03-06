using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    private Picture? picture;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Original
        picture = Picture.Gen();

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/image/scale.jpg"))) return false;

        picture.SetOrigin(0.5f, 0.5f);  //center origin
        picture.Translate(w / 2, h / 2);
        picture.Scale(1.5f);

        canvas.Add(picture);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, 3.0f, true);  //play time 3 secs.

        picture!.Scale((1.0f - progress) * 1.5f);

        canvas.Update();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: true, w: 1024, h: 1024);
}
