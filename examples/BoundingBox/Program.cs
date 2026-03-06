using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    void Bbox(Canvas canvas, Paint paint)
    {
        // Ensure the paint is updated.
        canvas.Update();

        //aabb
        float x, y, w, h;
        if (Verify(paint.Bounds(out x, out y, out w, out h)))
        {
            var bound = Shape.Gen();
            bound.MoveTo(x, y);
            bound.LineTo(x + w, y);
            bound.LineTo(x + w, y + h);
            bound.LineTo(x, y + h);
            bound.Close();
            bound.StrokeWidth(2.0f);
            bound.StrokeFill(255, 0, 0, 255);

            canvas.Add(bound);
        }

        //obb
        var pts = new Point[4];
        if (Verify(paint.Bounds(pts)))
        {
            var bound = Shape.Gen();
            bound.MoveTo(pts[0].x, pts[0].y);
            bound.LineTo(pts[1].x, pts[1].y);
            bound.LineTo(pts[2].x, pts[2].y);
            bound.LineTo(pts[3].x, pts[3].y);
            bound.Close();
            bound.StrokeWidth(2.0f);
            float[] dash = { 3.0f, 10.0f };
            bound.StrokeDash(dash, 2);
            bound.StrokeFill(255, 255, 255, 255);

            canvas.Add(bound);
        }
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        {
            var shape = Shape.Gen();
            shape.AppendCircle(50, 100, 40, 100);
            shape.SetFill(0, 30, 255);
            canvas.Add(shape);
            Bbox(canvas, shape);
        }

        {
            if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"))) return false;
            var text = Text.Gen();
            text.SetFont("PublicSans-Regular");
            text.SetFontSize(30);
            text.SetText("Text Test");
            text.SetFill(255, 255, 0);
            text.Translate(100, 20);
            text.Rotate(16.0f);
            canvas.Add(text);
            Bbox(canvas, text);
        }

        {
            var shape = Shape.Gen();
            shape.AppendRect(200, 30, 100, 20);
            shape.SetFill(200, 150, 55);
            shape.Rotate(30);
            canvas.Add(shape);
            Bbox(canvas, shape);
        }

        {
            var shape = Shape.Gen();
            shape.AppendRect(225, -50, 75, 50, 20, 25);
            shape.AppendCircle(225, 25, 50, 25);
            shape.StrokeWidth(10);
            shape.StrokeFill(255, 255, 255);
            shape.SetFill(50, 50, 155);

            var m = new Matrix(1.732f, -1.0f, 30.0f, 1.0f, 1.732f, -70.0f, 0.0f, 0.0f, 1.0f);
            shape.Transform(m);

            canvas.Add(shape);
            Bbox(canvas, shape);
        }

        {
            var svg = Picture.Gen();
            svg.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            svg.Scale(0.3f);
            svg.Translate(620, 50);
            canvas.Add(svg);
            Bbox(canvas, svg);
        }

        {
            var svg = Picture.Gen();
            svg.Load(ExamplePaths.ExampleDir + "/svg/tiger.svg");
            svg.Scale(0.2f);
            svg.Translate(140, 215);
            svg.Rotate(45);
            canvas.Add(svg);
            Bbox(canvas, svg);
        }

        {
            var scene = Scene.Gen();
            scene.Scale(0.3f);
            scene.Translate(280, 330);

            var img = Picture.Gen();
            img.Load(ExamplePaths.ExampleDir + "/image/test.png");
            scene.Add(img);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var scene = Scene.Gen();
            scene.Scale(0.3f);
            scene.Rotate(80);
            scene.Translate(200, 480);

            var img = Picture.Gen();
            img.Load(ExamplePaths.ExampleDir + "/image/test.jpg");
            scene.Add(img);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var line = Shape.Gen();
            line.MoveTo(470, 350);
            line.LineTo(770, 350);
            line.StrokeWidth(20);
            line.StrokeFill(55, 55, 0);
            canvas.Add(line);
            Bbox(canvas, line);
        }

        {
            var curve = Shape.Gen();
            curve.MoveTo(0, 0);
            curve.CubicTo(40.0f, -10.0f, 120.0f, -150.0f, 80.0f, 0.0f);
            curve.Translate(50, 770);
            curve.StrokeWidth(2.0f);
            curve.StrokeFill(255, 255, 255);
            canvas.Add(curve);
            Bbox(canvas, curve);
        }

        {
            var curve = Shape.Gen();
            curve.MoveTo(0, 0);
            curve.CubicTo(40.0f, -10.0f, 120.0f, -150.0f, 80.0f, 0.0f);
            curve.Translate(150, 750);
            curve.Rotate(20.0f);
            curve.StrokeWidth(2.0f);
            curve.StrokeFill(255, 0, 255);
            canvas.Add(curve);
            Bbox(canvas, curve);
        }

        {
            var scene = Scene.Gen();
            scene.Translate(550, 370);
            scene.Scale(0.7f);

            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            shape.LineTo(300, 200);
            shape.LineTo(0, 200);
            shape.SetFill(255, 0, 0);
            shape.Close();
            shape.Rotate(20);
            scene.Add(shape);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var scene = Scene.Gen();
            scene.Translate(330, 640);
            scene.Scale(0.7f);

            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            shape.LineTo(300, 200);
            shape.LineTo(0, 200);
            shape.SetFill(0, 255, 0);
            shape.Close();

            shape.StrokeWidth(30);
            shape.StrokeFill(255, 255, 255);
            shape.StrokeJoin(StrokeJoin.Bevel);

            scene.Add(shape);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var scene = Scene.Gen();
            scene.Translate(650, 650);
            scene.Scale(0.7f);
            scene.Rotate(20);

            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            shape.LineTo(300, 200);
            shape.LineTo(0, 200);
            shape.SetFill(0, 255, 255);
            shape.Close();

            shape.StrokeWidth(20);
            shape.StrokeFill(0, 0, 255);

            scene.Add(shape);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var scene = Scene.Gen();
            scene.Translate(800, 420);
            scene.Scale(0.5f);
            scene.Rotate(20);

            var shape = Shape.Gen();
            shape.MoveTo(0, 0);
            shape.LineTo(150, 100);
            shape.LineTo(0, 100);
            shape.Close();
            shape.SetFill(255, 0, 255);
            shape.StrokeWidth(30);
            shape.StrokeFill(0, 255, 255);
            shape.StrokeJoin(StrokeJoin.Miter);

            var m = new Matrix(1.8794f, -0.6840f, 0.0f, 0.6840f, 1.8794f, 0.0f, 0.0f, 0.0f, 1.0f);
            shape.Transform(m);

            scene.Add(shape);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        {
            var scene = Scene.Gen();
            scene.Translate(250, 490);
            scene.Scale(0.7f);

            var text = Text.Gen();
            text.SetFont("PublicSans-Regular");
            text.SetFontSize(50);
            text.SetText("Text Test");
            text.SetFill(255, 255, 0);
            text.Translate(0, 0);
            text.Rotate(16.0f);
            scene.Add(text);

            canvas.Add(scene);
            Bbox(canvas, scene);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 900, 900);
}
