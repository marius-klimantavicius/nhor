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
        picture = Picture.Gen();
        picture.SetOrigin(0.5f, 0.5f);  //center origin
        picture.Translate(w / 2, h / 2);

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/image/scale.jpg"))) return false;

        canvas.Add(picture);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        picture!.Scale(0.8f);
        picture.Rotate(Progress(elapsed, 4.0f) * 360.0f);

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
