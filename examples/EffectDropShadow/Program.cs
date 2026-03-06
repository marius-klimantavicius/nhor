using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    Scene? scene1 = null;
    Scene? scene2 = null;
    Scene? scene3 = null;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //background
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, w, h);
        bg.SetFill(255, 255, 255);
        canvas.Add(bg);

        //Prepare a scene for post effects
        {
            scene1 = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/thorvg-logo-clear.svg");
            picture.Scale(0.6f);
            picture.SetOrigin(0.5f, 0.0f);
            picture.Translate((float)(w / 2), 50.0f);

            scene1.Add(picture);
            canvas.Add(scene1);
        }

        //Prepare a scene for post effects
        {
            scene2 = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg/152932619-bd3d6921-72df-4f09-856b-f9743ae32a14.svg");
            picture.Scale(0.6f);
            picture.SetOrigin(0.5f, 0.0f);
            picture.Translate((float)(w / 2), 250.0f);

            scene2.Add(picture);
            canvas.Add(scene2);
        }

        //Prepare a scene for post effects
        {
            scene3 = Scene.Gen();

            var picture = Picture.Gen();
            picture.Load(ExamplePaths.ExampleDir + "/svg//circles1.svg");
            picture.Scale(0.7f);
            picture.SetOrigin(0.5f, 0.0f);
            picture.Translate((float)(w / 2), 550.0f);

            scene3.Add(picture);
            canvas.Add(scene3);
        }

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var progress = Progress(elapsed, 2.5f, true);   //2.5 seconds

        //Clear the previously applied effects
        scene1!.AddEffect(SceneEffect.Clear);
        //Apply DropShadow post effect (r, g, b, a, angle, distance, sigma of blurness, quality)
        scene1.AddEffect(SceneEffect.DropShadow, 0, 0, 0, 125, 120.0f, 20.0f * progress, 7.0f, 100);

        scene2!.AddEffect(SceneEffect.Clear);
        scene2.AddEffect(SceneEffect.DropShadow, 65, 143, 222, (int)(255.0f * progress), 135.0f, 10.0f, 3.0f, 100);

        scene3!.AddEffect(SceneEffect.Clear);
        scene3.AddEffect(SceneEffect.DropShadow, 0, 0, 0, 125, 360.0f * progress, 20.0f, 0.0f, 100);

        canvas.Update();

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 800, 800, 4, true);
}
