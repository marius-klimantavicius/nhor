using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const int WIDTH = 1100;
    const int HEIGHT = 800;

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/NOTO-SANS-KR.ttf"))) return false;

        //fixed size
        w = 800;
        h = 800;

        //guide line
        float border = 150.0f;
        float[] dashPattern = { 10.0f, 10.0f };
        var lines = Shape.Gen();
        lines.StrokeFill(100, 100, 100);
        lines.StrokeWidth(1);
        lines.StrokeDash(dashPattern, 2);
        lines.MoveTo(w / 2, 0);
        lines.LineTo(w / 2, h);
        lines.MoveTo(0, h / 2);
        lines.LineTo(w, h / 2);
        lines.MoveTo(border, border);
        lines.LineTo(w - border, border);
        lines.LineTo(w - border, h - border);
        lines.LineTo(border, h - border);
        lines.Close();
        lines.MoveTo(900, 0);
        lines.LineTo(900, h);
        canvas.Add(lines);

        var fontSize = 15.0f;
        w -= (uint)(border * 2.0f);
        h -= (uint)(border * 2.0f);

        //top left
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.0f, 0.0f);
            text.SetLayout(w, h);
            text.SetText("Top-Left");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //top center
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.5f, 0.0f);
            text.SetLayout(w, h);
            text.SetText("Top-Center");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //top right
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(1.0f, 0.0f);
            text.SetLayout(w, h);
            text.SetText("Top-End");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //middle left
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.0f, 0.5f);
            text.SetLayout(w, h);
            text.SetText("Middle-Left");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //middle center
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.5f, 0.5f);
            text.SetLayout(w, h);
            text.SetText("Middle-Center");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //middle right
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(1.0f, 0.5f);
            text.SetLayout(w, h);
            text.SetText("Middle-End");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //bottom left
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.0f, 1.0f);
            text.SetLayout(w, h);
            text.SetText("Bottom-Left");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //bottom center
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(0.5f, 1.0f);
            text.SetLayout(w, h);
            text.SetText("Bottom-Center");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //bottom right
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.Translate(border, border);
            text.SetFontSize(fontSize);
            text.SetAlign(1.0f, 1.0f);
            text.SetLayout(w, h);
            text.SetText("Bottom-End");
            text.SetFill(255, 255, 255);
            canvas.Add(text);
        }

        //origin
        Point[] alignments = {
            new Point(0.0f, 0.5f), new Point(0.25f, 0.5f), new Point(0.5f, 0.5f),
            new Point(0.75f, 0.5f), new Point(1.0f, 0.5f)
        };
        for (int i = 0; i < 5; ++i)
        {
            var text = Text.Gen();
            text.SetFont("NOTO-SANS-KR");
            text.SetFontSize(fontSize);
            text.SetText($"Alignment = {0.25 * (double)i:F2}");
            text.SetFill(255, 255, 255);
            text.Translate(900, 200 + i * 100);
            text.SetAlign(alignments[i].x, alignments[i].y);
            canvas.Add(text);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 1100, 800);
}
