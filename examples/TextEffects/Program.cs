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
        // Load fonts
        return Verify(Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf"));
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        if (!Verify(canvas.Remove())) return false;

        var waveText = "WAVE EFFECT";
        float time = elapsed * 0.001f * 3.0f;

        var shape = Shape.Gen();
        shape.AppendRect(0, 0, 600, 600);
        shape.SetFill(50, 50, 50);
        canvas.Add(shape);

        // Rainbow effect on title
        float r = (float)Math.Sin(time) * 127 + 128;
        float g = (float)Math.Sin(time + 2) * 127 + 128;
        float b = (float)Math.Sin(time + 4) * 127 + 128;

        var animTitle = Text.Gen();
        animTitle.SetFont("PublicSans-Regular");
        animTitle.SetFontSize(48);
        animTitle.SetText("ThorVG");
        animTitle.SetFill((byte)r, (byte)g, (byte)b);
        animTitle.SetOutline(2, 0, 0, 0);

        float x, y, w2, h2;
        animTitle.Bounds(out x, out y, out w2, out h2);
        animTitle.Translate(300 - w2 * 0.5f, 150 - h2 * 0.5f);
        canvas.Add(animTitle);

        // Pulsing subtitle
        float scale = 1 + (float)Math.Sin(time * 2) * 0.1f;
        var animSubtitle = Text.Gen();
        animSubtitle.SetFont("PublicSans-Regular");
        animSubtitle.SetFontSize(24 * scale);
        animSubtitle.SetText("High-Performance Vector Graphics");
        animSubtitle.SetFill(100, 200, 255);

        animSubtitle.Bounds(out x, out y, out w2, out h2);
        animSubtitle.Translate(300 - w2 * 0.5f, 220 - h2 * 0.5f);
        canvas.Add(animSubtitle);

        // Rotating text
        var rotatingText = Text.Gen();
        rotatingText.SetFont("PublicSans-Regular");
        rotatingText.SetFontSize(32);
        rotatingText.SetText("Animated!");
        rotatingText.SetFill(255, 200, 100);

        rotatingText.Bounds(out x, out y, out w2, out h2);
        float cx = 300;
        float cy = 300;

        const float PI = 3.141592f;
        float degree = time * 20;
        float radian = degree / 180.0f * PI;
        float cosVal = (float)Math.Cos(radian);
        float sinVal = (float)Math.Sin(radian);

        float textCenterX = cx - w2 * 0.5f;
        float textCenterY = cy - h2 * 0.5f;

        float tx = cx + (textCenterX - cx) * cosVal - (textCenterY - cy) * sinVal;
        float ty = cy + (textCenterX - cx) * sinVal + (textCenterY - cy) * cosVal;

        var m = new Matrix(cosVal, -sinVal, tx, sinVal, cosVal, ty, 0, 0, 1);
        rotatingText.Transform(m);
        canvas.Add(rotatingText);

        // Wave effect text
        int waveLen = waveText.Length;
        for (int i = 0; i < waveLen; i++)
        {
            float yOffset = (float)Math.Sin(time * 2 + i * 0.5f) * 20;
            float charColor = (float)Math.Sin(time + i * 0.3f) * 127 + 128;

            var charText = Text.Gen();
            charText.SetFont("PublicSans-Regular");
            charText.SetFontSize(28);
            charText.SetText(waveText[i].ToString());
            charText.SetFill((byte)charColor, 150, (byte)(255 - charColor));

            charText.Bounds(out x, out y, out w2, out h2);
            charText.Translate(150 + i * 30 - w2 * 0.5f, 400 + yOffset - h2 * 0.5f);
            canvas.Add(charText);
        }

        canvas.Update();
        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 600, 600);
}
