using System;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Saving Contents                                               */
/************************************************************************/

class GifSaverExample
{
    static void ExportGif()
    {
        var animation = Animation.Gen();
        var picture = animation.GetPicture();
        if (!ExampleBase.Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/sample.json"))) return;

        picture.SetSize(800, 800);

        var saver = Saver.Gen();
        if (!ExampleBase.Verify(saver.Save(animation, "./test.gif"))) return;
        saver.Sync();

        Console.WriteLine("Successfully exported to test.gif.");
    }

    /************************************************************************/
    /* Entry Point                                                          */
    /************************************************************************/

    static int Main(string[] args)
    {
        if (ExampleBase.Verify(Initializer.Init()))
        {
            ExportGif();
            Initializer.Term();
        }
        return 0;
    }
}
