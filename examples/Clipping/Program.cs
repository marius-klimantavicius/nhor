using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    void Compose(Shape star)
    {
        star.MoveTo(199, 34);
        star.LineTo(253, 143);
        star.LineTo(374, 160);
        star.LineTo(287, 244);
        star.LineTo(307, 365);
        star.LineTo(199, 309);
        star.LineTo(97, 365);
        star.LineTo(112, 245);
        star.LineTo(26, 161);
        star.LineTo(146, 143);
        star.Close();
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(255, 255, 255);
        canvas.Add(shape);

        {
            var scene = Scene.Gen();

            var star1 = Shape.Gen();
            Compose(star1);
            star1.SetFill(255, 255, 0);
            star1.StrokeFill(255, 0, 0);
            star1.StrokeWidth(10);

            //Move Star1
            star1.Translate(-10, -10);

            // color/alpha/opacity are ignored for a clip object - no need to set them
            var clipStar = Shape.Gen();
            clipStar.AppendCircle(200, 230, 110, 110);
            clipStar.Translate(10, 10);

            star1.Clip(clipStar);

            var star2 = Shape.Gen();
            Compose(star2);
            star2.SetFill(0, 255, 255);
            star2.StrokeFill(0, 255, 0);
            star2.StrokeWidth(10);
            star2.Opacity(100);

            //Move Star2
            star2.Translate(10, 40);

            // color/alpha/opacity are ignored for a clip object - no need to set them
            var clip = Shape.Gen();
            clip.AppendCircle(200, 230, 130, 130);
            clip.Translate(10, 10);

            scene.Add(star1);
            scene.Add(star2);

            //Clipping scene to shape
            scene.Clip(clip);

            canvas.Add(scene);
        }

        {
            var star3 = Shape.Gen();
            Compose(star3);

            //Fill Gradient
            var fill = LinearGradient.Gen();
            fill.Linear(100, 100, 300, 300);
            Fill.ColorStop[] colorStops = new Fill.ColorStop[2];
            colorStops[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops[1] = new Fill.ColorStop(1, 255, 255, 255, 255);
            fill.SetColorStops(colorStops, 2);
            star3.SetFill(fill);

            star3.StrokeFill(255, 0, 0);
            star3.StrokeWidth(10);
            star3.Translate(400, 0);

            // color/alpha/opacity are ignored for a clip object - no need to set them
            var clipRect = Shape.Gen();
            clipRect.AppendRect(500, 120, 200, 200);          //x, y, w, h
            clipRect.Translate(20, 20);

            //Clipping scene to rect(shape)
            star3.Clip(clipRect);

            canvas.Add(star3);
        }

        {
            var picture = Picture.Gen();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/svg/cartman.svg"))) return false;

            picture.Scale(3);
            picture.Translate(50, 400);

            // color/alpha/opacity are ignored for a clip object - no need to set them
            var clipPath = Shape.Gen();
            clipPath.AppendCircle(200, 510, 50, 50);          //x, y, w, h, rx, ry
            clipPath.AppendCircle(200, 650, 50, 50);          //x, y, w, h, rx, ry
            clipPath.Translate(20, 20);

            //Clipping picture to path
            picture.Clip(clipPath);

            canvas.Add(picture);
        }

        {
            var shape1 = Shape.Gen();
            shape1.AppendRect(500, 420, 250, 250, 20, 20);
            shape1.SetFill(255, 0, 255, 160);

            // color/alpha/opacity are ignored for a clip object - no need to set them
            var clipShape = Shape.Gen();
            clipShape.AppendCircle(600, 550, 150, 150);
            clipShape.StrokeWidth(20);

            //Clipping shape1 to clipShape
            shape1.Clip(clipShape);

            canvas.Add(shape1);
        }

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
