using System.IO;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Background
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, w, h);
        bg.SetFill(255, 255, 255);
        canvas.Add(bg);

        var path = ExamplePaths.ExampleDir + "/image/rawimage_200x300.raw";
        var bytes = File.ReadAllBytes(path);
        var data = new uint[200 * 300];
        System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

        var picture = Picture.Gen();
        if (!Verify(picture.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;
        picture.Translate(400, 250);
        canvas.Add(picture);

        var picture2 = Picture.Gen();
        if (!Verify(picture2.Load(data, 200, 300, ColorSpace.ARGB8888, true))) return false;

        picture2.Translate(400, 200);
        picture2.Rotate(47);
        picture2.Scale(1.5f);
        picture2.Opacity(128);

        var circle = Shape.Gen();
        circle.AppendCircle(350, 350, 200, 200);

        picture2.Clip(circle);

        canvas.Add(picture2);

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
