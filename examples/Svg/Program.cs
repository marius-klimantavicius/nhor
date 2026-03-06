using System;
using System.Collections.Generic;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const int NUM_PER_ROW = 9;
    const int NUM_PER_COL = 9;

    List<Picture> pictures = new List<Picture>();
    uint w, h;
    uint size;

    int counter = 0;

    public override void Populate(string path)
    {
        if (counter >= NUM_PER_ROW * NUM_PER_COL) return;

        //ignore if not svg.
        if (!path.EndsWith("svg")) return;

        var picture = Picture.Gen();
        picture.SetOrigin(0.5f, 0.5f);

        if (!Verify(picture.Load(path))) return;

        //image scaling preserving its aspect ratio
        float w, h;
        picture.GetSize(out w, out h);
        picture.Scale((w > h) ? size / w : size / h);
        picture.Translate((counter % NUM_PER_ROW) * size + size / 2, (counter / NUM_PER_ROW) * (this.h / NUM_PER_COL) + size / 2);

        pictures.Add(picture);

        Console.WriteLine("SVG: " + path);

        counter++;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //The default font for fallback in case
        Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf");

        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(150, 150, 150);

        canvas.Add(shape);

        this.w = w;
        this.h = h;
        this.size = w / NUM_PER_ROW;

        ScanDir(ExamplePaths.ExampleDir + "/svg");

        /* This showcase demonstrates the asynchronous loading of tvg.
           For this, pictures are added at a certain sync time.
           This allows time for the tvg resources to finish loading;
           otherwise, you can add pictures immediately. */
        foreach (var paint in pictures)
        {
            canvas.Add(paint);
        }

        pictures.Clear();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1280, 1280);
}
