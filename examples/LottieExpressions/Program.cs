using System;
using System.Collections.Generic;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const int NUM_PER_ROW = 5;
    const int NUM_PER_COL = 5;

    List<Animation> animations = new List<Animation>();
    List<Text> labels = new List<Text>();
    uint w, h;
    uint size;

    int counter = 0;

    public override void Populate(string path)
    {
        if (counter >= NUM_PER_ROW * NUM_PER_COL) return;

        //ignore if not lottie.
        if (!path.EndsWith("json") && !path.EndsWith(".lot")) return;

        //Animation Controller
        var animation = Animation.Gen();
        var picture = animation.GetPicture();
        picture.SetOrigin(0.5f, 0.5f);

        if (!Verify(picture.Load(path))) return;

        //image scaling preserving its aspect ratio
        float w, h;
        picture.GetSize(out w, out h);
        picture.Scale((w > h) ? size / w : size / h);
        picture.Translate((counter % NUM_PER_ROW) * size + size / 2, (counter / NUM_PER_ROW) * (this.h / NUM_PER_COL) + size / 2);

        animations.Add(animation);

        //Filename label
        var filename = System.IO.Path.GetFileNameWithoutExtension(path);
        var label = Text.Gen();
        label.SetFont("PublicSans-Regular");
        label.SetFontSize(11);
        label.SetText(filename);
        label.SetFill(220, 220, 220);
        float cellX = (counter % NUM_PER_ROW) * size;
        float cellY = (counter / NUM_PER_ROW) * (this.h / NUM_PER_COL);
        label.Translate(cellX + 4, cellY + size - 16);
        labels.Add(label);

        Console.WriteLine("Lottie: " + path);

        counter++;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        foreach (var animation in animations)
        {
            var progress = Progress(elapsed, animation.Duration());
            animation.Frame(animation.TotalFrame() * progress);
        }

        canvas.Update();

        return true;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //The default font for fallback in case
        Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf");

        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(75, 75, 75);

        canvas.Add(shape);

        this.w = w;
        this.h = h;
        this.size = w / NUM_PER_ROW;

        ScanDir(ExamplePaths.ExampleDir + "/lottie/expressions");

        //Run animation loop
        foreach (var animation in animations)
        {
            canvas.Add(animation.GetPicture());
        }

        //Add filename labels on top
        foreach (var label in labels)
        {
            canvas.Add(label);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1024, 1024, 0, true);
}
