using System.IO;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(75, 75, 75);
        canvas.Add(shape);

        //Load a necessary font data.
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"))) return false;
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/NOTO-SANS-KR.ttf"))) return false;
        if (!Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/NanumGothicCoding.ttf"))) return false;

        //Load from memory
        var fontPath = ExamplePaths.ExampleDir + "/font/SentyCloud.ttf";
        if (File.Exists(fontPath))
        {
            var data = File.ReadAllBytes(fontPath);
            if (!Verify(Text.LoadFont("SentyCloud", data, (uint)data.Length, "ttf", true))) return false;
        }

        var text = Text.Gen();
        text.SetFont("PublicSans-Regular");
        text.SetFontSize(80);
        text.SetText("THORVG Text");
        text.SetFill(255, 255, 255);
        canvas.Add(text);

        var text2 = Text.Gen();
        text2.SetFont("PublicSans-Regular");
        text2.SetFontSize(30);
        text2.SetItalic(0.18f);
        text2.SetText("Font = \"PublicSans-Regular\", Size = 40, Style = Italic");
        text2.Translate(0, 150);
        text2.SetFill(255, 255, 255);
        canvas.Add(text2);

        var text3 = Text.Gen();
        text3.SetFont(null);  //Use any font
        text3.SetFontSize(40);
        text3.SetText("Kerning Test: VA, AV, TJ, JT");
        text3.SetFill(255, 255, 255);
        text3.Translate(0, 225);
        canvas.Add(text3);

        var text4 = Text.Gen();
        text4.SetFont("PublicSans-Regular");
        text4.SetFontSize(25);
        text4.SetText("Purple Text");
        text4.SetFill(255, 0, 255);
        text4.Translate(0, 310);
        canvas.Add(text4);

        var text5 = Text.Gen();
        text5.SetFont("PublicSans-Regular");
        text5.SetFontSize(25);
        text5.SetText("Gray Text");
        text5.SetFill(150, 150, 150);
        text5.Translate(220, 310);
        canvas.Add(text5);

        var text6 = Text.Gen();
        text6.SetFont("PublicSans-Regular");
        text6.SetFontSize(25);
        text6.SetText("Yellow Text");
        text6.SetFill(255, 255, 0);
        text6.Translate(400, 310);
        canvas.Add(text6);

        var text7 = Text.Gen();
        text7.SetFont("NOTO-SANS-KR");
        text7.SetFontSize(15);
        text7.SetText("Transformed Text - 30'");
        text7.SetFill(0, 0, 0);
        text7.Translate(600, 400);
        text7.Rotate(30);
        canvas.Add(text7);

        var text8 = Text.Gen();
        text8.SetFont("NOTO-SANS-KR");
        text8.SetFontSize(15);
        text8.SetFill(0, 0, 0);
        text8.SetText("Transformed Text - 90'");
        text8.Translate(600, 400);
        text8.Rotate(90);
        canvas.Add(text8);

        var text9 = Text.Gen();
        text9.SetFont("NOTO-SANS-KR");
        text9.SetFontSize(15);
        text9.SetFill(0, 0, 0);
        text9.SetText("Transformed Text - 180'");
        text9.Translate(800, 400);
        text9.Rotate(180);
        canvas.Add(text9);

        //gradient texts
        float x, y, w2, h2;

        var text10 = Text.Gen();
        text10.SetFont("NOTO-SANS-KR");
        text10.SetFontSize(50);
        text10.SetText("Linear Text");
        text10.Bounds(out x, out y, out w2, out h2);

        //LinearGradient
        var fill = LinearGradient.Gen();
        fill.Linear(x, y + h2 * 0.5f, x + w2, y + h2 * 0.5f);

        //Gradient Color Stops
        var colorStops = new Fill.ColorStop[3];
        colorStops[0] = new Fill.ColorStop(0, 255, 0, 0, 255);
        colorStops[1] = new Fill.ColorStop(0.5f, 255, 255, 0, 255);
        colorStops[2] = new Fill.ColorStop(1, 255, 255, 255, 255);
        fill.SetColorStops(colorStops, 3);
        text10.SetFill(fill);

        text10.Translate(0, 350);

        canvas.Add(text10);

        var text11 = Text.Gen();
        text11.SetFont("NanumGothicCoding");
        text11.SetFontSize(40);
        text11.SetText("\xeb\x82\x98\xeb\x88\x94\xea\xb3\xa0\xeb\x94\x95\xec\xbd\x94\xeb\x94\xa9\x28\x55\x54\x46\x2d\x38\x29");
        text11.Bounds(out x, out y, out w2, out h2);

        //RadialGradient
        var fill2 = RadialGradient.Gen();
        fill2.Radial(x + w2 * 0.5f, y + h2 * 0.5f, w2 * 0.5f, x + w2 * 0.5f, y + h2 * 0.5f, 0.0f);

        //Gradient Color Stops
        var colorStops2 = new Fill.ColorStop[3];
        colorStops2[0] = new Fill.ColorStop(0, 0, 255, 255, 255);
        colorStops2[1] = new Fill.ColorStop(0.5f, 255, 255, 0, 255);
        colorStops2[2] = new Fill.ColorStop(1, 255, 255, 255, 255);

        fill2.SetColorStops(colorStops2, 3);

        text11.SetFill(fill2);
        text11.Translate(0, 450);

        canvas.Add(text11);

        var text12 = Text.Gen();
        text12.SetFont("SentyCloud");
        text12.SetFontSize(50);
        text12.SetFill(255, 25, 25);
        text12.SetOutline(3, 255, 200, 200);
        text12.SetText("\xe4\xb8\x8d\xe5\x88\xb0\xe9\x95\xbf\xe5\x9f\x8e\xe9\x9d\x9e\xe5\xa5\xbd\xe6\xb1\x89\xef\xbc\x81");
        text12.Translate(0, 525);
        canvas.Add(text12);

        var text13 = Text.Gen();
        text13.SetFont("PublicSans-Regular");
        text13.SetFontSize(20);
        text13.SetFill(255, 255, 255);
        text13.SetText("LINE-FEED TEST. THIS IS THE FIRST LINE - \nTHIS IS THE SECOND LINE.");
        text13.Translate(0, 625);
        canvas.Add(text13);

        var text14 = Text.Gen();
        text14.SetFont("PublicSans-Regular");
        text14.SetFontSize(20);
        text14.SetFill(255, 255, 255);
        text14.SetSpacing(1.5f, 1.5f);
        text14.SetText("1.5x SPACING TEST. THIS IS THE FIRST LINE - \nTHIS IS THE SECOND LINE.");
        text14.Translate(0, 700);
        canvas.Add(text14);

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1024, 1024);
}
