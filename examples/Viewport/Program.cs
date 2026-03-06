using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const uint VPORT_SIZE = 300;
    uint _w, _h;
    Picture? picture;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //set viewport before canvas become dirty.
        if (!Verify(canvas.Viewport(0, 0, (int)VPORT_SIZE, (int)VPORT_SIZE))) return false;

        var mask = Shape.Gen();
        mask.AppendCircle(w / 2.0f, h / 2.0f, w / 2.0f, h / 2.0f);
        mask.SetFill(255, 255, 255);
        //Use the opacity for a half-translucent mask.
        mask.Opacity(125);

        picture = Picture.Gen();
        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg"))) return false;
        picture.SetSize(w, h);
        picture.SetMask(mask, MaskMethod.Alpha);
        canvas.Add(picture);

        _w = w;
        _h = h;

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, 2.0f, true);  //play time 2 sec.

        if (!Verify(canvas.Viewport((int)((_w - VPORT_SIZE) * progress), (int)((_h - VPORT_SIZE) * progress), (int)VPORT_SIZE, (int)VPORT_SIZE))) return false;

        canvas.Update();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, clearBuffer: true, w: 1024, h: 1024, print: true);
}
