using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

// NOTE: This example is a simplified port. The full C++ TextMetrics example
// requires GlyphMetrics, text->metrics(char*, GlyphMetrics&), text->lines(),
// and text->text() (getter) APIs which are not yet ported to C#.
// When those APIs are available, this example should be expanded to match
// the full interactive text typing demo from the C++ version.

class UserExample : ExampleBase
{
    Text? text;
    Scene? group;
    Shape? cursor;
    Point pos;
    TextMetrics textMetric;
    uint lastElapsed = 0;
    bool toggle = true;
    bool updated = false;
    string data = "Type here: ";

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/NOTO-SANS-KR.ttf"))) return false;

        // guide line
        float border = 150.0f;
        float[] dashPattern = { 10.0f, 10.0f };
        var lines = Shape.Gen();
        lines.StrokeFill(100, 100, 100);
        lines.StrokeWidth(1);
        lines.StrokeDash(dashPattern, 2);
        lines.MoveTo(border, border);
        lines.LineTo(w - border, border);
        lines.LineTo(w - border, h - border);
        lines.LineTo(border, h - border);
        lines.Close();
        canvas.Add(lines);

        // text group
        group = Scene.Gen();
        group.Translate(border, border);
        canvas.Add(group);

        // text body
        text = Text.Gen();
        text.Ref();
        text.SetFont("NOTO-SANS-KR");
        text.SetFontSize(16.0f);
        text.SetAlign(0.0f, 0.0f);
        text.SetLayout(w - border * 2.0f, h - border * 2.0f);
        text.SetWrapping(TextWrap.Character);
        text.SetText(data);
        text.SetFill(255, 255, 255);
        group.Add(text);

        // figure out the cursor y position
        if (!Verify(text.GetMetrics(out textMetric))) return false;
        pos.y = textMetric.ascent;

        // cursor visual
        cursor = Shape.Gen();
        cursor.Ref();
        cursor.AppendRect(0, 0, 15, 3);
        cursor.SetFill(255, 255, 255);
        cursor.Translate(pos.x, pos.y);
        group.Add(cursor);

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        var upd = this.updated;
        this.updated = false;

        //blinking cursor effect
        if (elapsed - lastElapsed > 500)
        {
            toggle = !toggle;

            if (toggle) cursor!.Opacity(255);
            else cursor!.Opacity(0);

            lastElapsed = elapsed;
            upd = true;
        }

        if (upd) canvas.Update();

        return upd;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 800, 800);
}
