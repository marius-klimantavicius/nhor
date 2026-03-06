using System;
using System.Text;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //background
        {
            var bg = Shape.Gen();
            bg.AppendRect(0, 0, w, h);    //x, y, w, h
            bg.SetFill(200, 200, 255);     //r, g, b
            canvas.Add(bg);
        }

        //wild
        {
            float top = 100.0f;
            float bot = 700.0f;

            var path = Shape.Gen();
            path.MoveTo(300, top / 2);
            path.LineTo(100, bot);
            path.LineTo(350, 400);
            path.LineTo(420, bot);
            path.LineTo(430, top * 2);
            path.LineTo(500, bot);
            path.LineTo(460, top * 2);
            path.LineTo(750, bot);
            path.LineTo(460, top / 2);
            path.Close();

            path.SetFill(150, 150, 255);
            path.StrokeWidth(20);
            path.StrokeFill(120, 120, 255);

            path.StrokeJoin(StrokeJoin.Miter);

            path.StrokeMiterlimit(10);
            var ml = path.GetStrokeMiterlimit();
            Console.WriteLine($"stroke miterlimit = {ml}");

            canvas.Add(path);
        }

        //blueprint
        {
            // Load png file from path.
            var filePath = ExamplePaths.ExampleDir + "/image/stroke-miterlimit.png";

            var picture = Picture.Gen();
            if (!Verify(picture.Load(filePath))) return false;

            picture.Opacity(42);
            picture.Translate(24, 0);
            canvas.Add(picture);
        }

        //svg
        {
            //SVG stroke-miterlimit test.
            var svgText = @"
        <svg viewBox=""0 0 38 30"">
        <!-- Impact of the default miter limit -->
        <path
            stroke=""black""
            fill=""none""
            stroke-linejoin=""miter""
            id=""p1""
            d=""M1,9 l7   ,-3 l7   ,3
            m2,0 l3.5 ,-3 l3.5 ,3
            m2,0 l2   ,-3 l2   ,3
            m2,0 l0.75,-3 l0.75,3
            m2,0 l0.5 ,-3 l0.5 ,3"" />

        <!-- Impact of the smallest miter limit (1) -->
        <path
            stroke=""black""
            fill=""none""
            stroke-linejoin=""miter""
            stroke-miterlimit=""1""
            id=""p2""
            d=""M1,19 l7   ,-3 l7   ,3
            m2, 0 l3.5 ,-3 l3.5 ,3
            m2, 0 l2   ,-3 l2   ,3
            m2, 0 l0.75,-3 l0.75,3
            m2, 0 l0.5 ,-3 l0.5 ,3"" />

        <!-- Impact of a large miter limit (8) -->
        <path
            stroke=""black""
            fill=""none""
            stroke-linejoin=""miter""
            stroke-miterlimit=""8""
            id=""p3""
            d=""M1,29 l7   ,-3 l7   ,3
            m2, 0 l3.5 ,-3 l3.5 ,3
            m2, 0 l2   ,-3 l2   ,3
            m2, 0 l0.75,-3 l0.75,3
            m2, 0 l0.5 ,-3 l0.5 ,3"" />

        <!-- the following pink lines highlight the position of the path for each stroke -->
        <path
            stroke=""pink""
            fill=""none""
            stroke-width=""0.05""
            d=""M1, 9 l7,-3 l7,3 m2,0 l3.5,-3 l3.5,3 m2,0 l2,-3 l2,3 m2,0 l0.75,-3 l0.75,3 m2,0 l0.5,-3 l0.5,3
            M1,19 l7,-3 l7,3 m2,0 l3.5,-3 l3.5,3 m2,0 l2,-3 l2,3 m2,0 l0.75,-3 l0.75,3 m2,0 l0.5,-3 l0.5,3
            M1,29 l7,-3 l7,3 m2,0 l3.5,-3 l3.5,3 m2,0 l2,-3 l2,3 m2,0 l0.75,-3 l0.75,3 m2,0 l0.5,-3 l0.5,3"" />
        </svg>
        ";

            var svgBytes = Encoding.UTF8.GetBytes(svgText);
            var picture = Picture.Gen();
            if (!Verify(picture.Load(svgBytes, (uint)svgBytes.Length, "svg", "", true))) return false;
            picture.Scale(20);
            canvas.Add(picture);
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
