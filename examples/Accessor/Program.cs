using System;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //load the svg file
        var picture = Picture.Gen();
        var result = picture.Load(ExamplePaths.ExampleDir + "/svg/favorite_on.svg");
        if (!Verify(result)) return false;
        picture.SetSize(w, h);

        var accessor = Accessor.Gen();

        //The callback function from lambda expression.
        //This function will be called for every paint nodes of the picture tree.
        Func<Paint, object?, bool> f = (Paint paint, object? data) =>
        {
            if (paint.PaintType() == ThorVG.Type.Shape)
            {
                var shape = (Shape)paint;
                //override color?
                byte r, g, b, a;
                shape.GetFillColor(out r, out g, out b, out a);
                if (r == 255 && g == 180 && b == 0)
                    shape.SetFill(0, 0, 255);
            }

            //You can return false, to stop traversing immediately.
            return true;
        };

        if (!Verify(accessor.Set(picture, f, null))) return false;

        // Try to retrieve the shape that corresponds to the SVG node with the unique ID "star".
        var paint = picture.FindPaint(Accessor.Id("star"));
        if (paint != null)
        {
            var shape = (Shape)paint;
            shape.StrokeFill(255, 255, 0);
            shape.StrokeWidth(5);
        }

        canvas.Add(picture);

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
