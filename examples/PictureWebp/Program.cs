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
        bg.AppendRect(0, 0, w, h);             //x, y, w, h
        bg.SetFill(255, 255, 255);              //r, g, b
        canvas.Add(bg);

        //Load webp file from path
        var opacity = 31;

        for (int i = 0; i < 7; ++i) {
            var picture = Picture.Gen();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/image/test.webp"))) return false;
            picture.Translate(i * 150, i * 150);
            picture.Rotate(30 * i);
            picture.SetSize(200, 200);
            picture.Opacity((byte)(opacity + opacity * i));
            canvas.Add(picture);
        }

        //Open file manually
        var fileData = File.ReadAllBytes(ExamplePaths.ExampleDir + "/image/test.webp");

        var picture2 = Picture.Gen();
        if (!Verify(picture2.Load(fileData, (uint)fileData.Length, "webp", "", true))) return false;
        picture2.Translate(400, 0);
        picture2.Scale(0.8f);
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
