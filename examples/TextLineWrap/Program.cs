using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    Point size = new Point(230.0f, 120.0f);

    void Guide(Canvas canvas, string title, float x, float y)
    {
        var txt = Text.Gen();
        txt.SetFont("NOTO-SANS-KR");
        txt.Translate(x, y);
        txt.SetFontSize(12);
        txt.SetText(title);
        txt.SetFill(200, 200, 200);
        canvas.Add(txt);

        var lines = Shape.Gen();
        lines.StrokeFill(100, 100, 100);
        lines.StrokeWidth(1);
        lines.AppendRect(x, y + 30.0f, size.x, size.y);
        canvas.Add(lines);
    }

    void AddText(Canvas canvas, string content, Point pos, Point align, TextWrap wrapMode)
    {
        var txt = Text.Gen();
        txt.SetFont("NOTO-SANS-KR");
        txt.Translate(pos.x, pos.y + 30.0f);
        txt.SetLayout(size.x, size.y);
        txt.SetFontSize(14.5f);
        txt.SetText(content);
        txt.SetAlign(align.x, align.y);
        txt.SetWrapping(wrapMode);
        txt.SetFill(255, 255, 255);
        canvas.Add(txt);
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/NOTO-SANS-KR.ttf"))) return false;

        var character = "TextWrap::Character";
        Guide(canvas, character, 25.0f, 25.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with top-left.", new Point(25.0f, 25.0f), new Point(0.0f, 0.0f), TextWrap.Character);

        Guide(canvas, character, 290.0f, 25.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with middle-center.", new Point(290.0f, 25.0f), new Point(0.5f, 0.5f), TextWrap.Character);

        Guide(canvas, character, 550.0f, 25.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with bottom-right.", new Point(550.0f, 25.0f), new Point(1.0f, 1.0f), TextWrap.Character);

        var word = "TextWrap::Word";
        Guide(canvas, word, 25.0f, 195.0f);
        AddText(canvas, "An extreame-long-length-word to test with top-left.", new Point(25.0f, 195.0f), new Point(0.0f, 0.0f), TextWrap.Word);

        Guide(canvas, word, 290.0f, 195.0f);
        AddText(canvas, "An extreame-long-length-word to test with middle-center.", new Point(290.0f, 195.0f), new Point(0.5f, 0.5f), TextWrap.Word);

        Guide(canvas, word, 550.0f, 195.0f);
        AddText(canvas, "An extreame-long-length-word to test with bottom-right.", new Point(550.0f, 195.0f), new Point(1.0f, 1.0f), TextWrap.Word);

        var smart = "TextWrap::Smart";
        Guide(canvas, smart, 25.0f, 365.0f);
        AddText(canvas, "An extreame-long-length-word to test with top-left.", new Point(25.0f, 365.0f), new Point(0.0f, 0.0f), TextWrap.Smart);

        Guide(canvas, smart, 290.0f, 365.0f);
        AddText(canvas, "An extreame-long-length-word to test with middle-center.", new Point(290.0f, 365.0f), new Point(0.5f, 0.5f), TextWrap.Smart);

        Guide(canvas, smart, 550.0f, 365.0f);
        AddText(canvas, "An extreame-long-length-word to test with bottom-right.", new Point(550.0f, 365.0f), new Point(1.0f, 1.0f), TextWrap.Smart);

        var ellipsis = "TextWrap::Ellipsis";
        Guide(canvas, ellipsis, 25.0f, 535.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with top-left.", new Point(25.0f, 535.0f), new Point(0.0f, 0.0f), TextWrap.Ellipsis);

        Guide(canvas, ellipsis, 290.0f, 535.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with middle-center.", new Point(290.0f, 535.0f), new Point(0.5f, 0.5f), TextWrap.Ellipsis);

        Guide(canvas, ellipsis, 550.0f, 535.0f);
        AddText(canvas, "This is a lengthy text used to test line wrapping with bottom-right.", new Point(550.0f, 535.0f), new Point(1.0f, 1.0f), TextWrap.Ellipsis);

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
