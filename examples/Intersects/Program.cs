using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    //Use a scene for selection effect
    Shape? shape;
    Picture? picture;
    Text? text;
    Picture? tiger;

    Shape? marquee;
    int mx = 0, my = 0, mw = 20, mh = 20;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //dash stroke filled shape
        {
            shape = Shape.Gen();
            shape.MoveTo(255, 85);
            shape.LineTo(380, 405);
            shape.LineTo(75, 200);
            shape.LineTo(435, 200);
            shape.LineTo(130, 405);
            shape.Close();
            shape.SetFill(255, 255, 255);
            shape.SetFillRule(FillRule.EvenOdd);

            shape.StrokeWidth(20);
            shape.StrokeFill(0, 255, 0);
            float[] dashPattern = { 40, 40 };
            shape.StrokeCap(StrokeCap.Butt);
            shape.StrokeDash(dashPattern, 2);

            shape.Scale(1.25f);

            canvas.Add(shape);
        }

        //clipped, rotated image
        {
            picture = Picture.Gen();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/image/test.jpg"))) return false;

            picture.Translate(800, 100);
            picture.Rotate(47);

            var clip = Shape.Gen();
            clip.AppendCircle(900, 350, 200, 200);
            picture.Clip(clip);

            canvas.Add(picture);
        }

        //normal text
        {
            if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"))) return false;
            text = Text.Gen();
            text.SetFont("PublicSans-Regular");
            text.SetFontSize(100);
            text.SetText("Intersect?!");
            text.Translate(25, 800);
            text.SetFill(255, 255, 255);

            canvas.Add(text);
        }

        //vector scene
        {
            tiger = Picture.Gen();
            if (!Verify(tiger.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg"))) return false;
            tiger.Translate(700, 640);
            tiger.Scale(0.5f);

            canvas.Add(tiger);
        }

        //marquee
        {
            marquee = Shape.Gen();
            marquee.AppendRect(mx, my, mw, mh);
            marquee.StrokeWidth(2);
            marquee.StrokeFill(255, 255, 0);
            marquee.SetFill(255, 255, 0, 50);
            canvas.Add(marquee);
        }

        return true;
    }

    public override bool Motion(Canvas canvas, int x, int y)
    {
        //center align
        mx = x - (mw / 2);
        my = y - (mh / 2);

        return false;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        marquee!.Translate(mx, my);

        //reset
        shape!.Opacity(255);
        picture!.Opacity(255);
        text!.Opacity(255);
        tiger!.Opacity(255);

        if (shape.Intersects(mx, my, mw, mh)) shape.Opacity(127);
        else if (picture.Intersects(mx, my, mw, mh)) picture.Opacity(127);
        else if (text.Intersects(mx, my, mw, mh)) text.Opacity(127);
        else if (tiger.Intersects(mx, my, mw, mh)) tiger.Opacity(127);

        canvas.Update();

        return true;
    }
}

/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 1200, 1200);
}
