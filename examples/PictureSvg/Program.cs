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
        //The default font for fallback in case
        Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf");

        var opacity = 36;

        //Load svg file from path
        for (int i = 0; i < 7; ++i) {
            var picture = Picture.Gen();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/svg/logo.svg"))) return false;
            picture.Translate(i * 150, i * 150);
            picture.Rotate(30 * i);
            picture.SetSize(200, 200);
            picture.Opacity((byte)(opacity + opacity * i));
            canvas.Add(picture);
        }

        //Open file manually
        var fileData = File.ReadAllBytes(ExamplePaths.ExampleDir + "/svg/logo.svg");

        var picture2 = Picture.Gen();
        if (!Verify(picture2.Load(fileData, (uint)fileData.Length, "svg", "", true))) return false;
        picture2.Translate(400, 0);
        picture2.Scale(0.4f);
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
