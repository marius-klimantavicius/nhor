using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const int SIZE = 400;

    Scene?[] blur = new Scene?[3];   //(for direction both, horizontal, vertical)
    Scene? fill = null;
    Scene? tint = null;
    Scene? tritone = null;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //blur scene
        for (int i = 0; i < 3; ++i)
        {
            blur[i] = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            picture.SetSize(SIZE, SIZE);
            picture.Translate(SIZE * i, 0);

            blur[i]!.Add(picture);
            canvas.Add(blur[i]!);
        }

        //fill scene
        {
            fill = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            picture.SetSize(SIZE, SIZE);
            picture.Translate(0, SIZE);

            fill.Add(picture);
            canvas.Add(fill);
        }

        //tint scene
        {
            tint = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            picture.SetSize(SIZE, SIZE);
            picture.Translate(SIZE, SIZE);

            tint.Add(picture);
            canvas.Add(tint);
        }

        //tritone scene
        {
            tritone = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            picture.SetSize(SIZE, SIZE);
            picture.Translate(SIZE * 2, SIZE);

            tritone.Add(picture);
            canvas.Add(tritone);
        }

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, 2.5f, true);   //2.5 seconds

        //Apply GaussianBlur post effect (sigma, direction, border option, quality)
        for (int i = 0; i < 3; ++i)
        {
            blur[i]!.AddEffect(SceneEffect.Clear);
            blur[i]!.AddEffect(SceneEffect.GaussianBlur, 10.0f * progress, i, 0, 100);
        }

        //Apply Fill post effect (rgba)
        fill!.AddEffect(SceneEffect.Clear);
        fill.AddEffect(SceneEffect.Fill, 0, (int)(progress * 255), 0, (int)(255.0f * progress));

        //Apply Tint post effect (black:rgb, white:rgb, intensity)
        tint!.AddEffect(SceneEffect.Clear);
        tint.AddEffect(SceneEffect.Tint, 0, 0, 0, 0, (int)(progress * 255), 0, (double)(progress * 100.0f));

        //Apply Tritone post effect (shadow:rgb, midtone:rgb, highlight:rgb, blending with original)
        tritone!.AddEffect(SceneEffect.Clear);
        tritone.AddEffect(SceneEffect.Tritone, 0, (int)(progress * 255), 0, 199, 110, 36, 255, 255, 255, 0);

        canvas.Update();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 400 * 3, 400 * 2, 4, true);
}
