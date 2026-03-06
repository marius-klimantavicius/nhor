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
        return Update(canvas, 0);
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        if (!Verify(canvas.Remove())) return false;

        var progress = Progress(elapsed, 2.0f, true);  //play time 2 sec.

        //Shape
        var shape = Shape.Gen();

        shape.MoveTo(0, -114.5f);
        shape.LineTo(54, -5.5f);
        shape.LineTo(175, 11.5f);
        shape.LineTo(88, 95.5f);
        shape.LineTo(108, 216.5f);
        shape.LineTo(0, 160.5f);
        shape.LineTo(-102, 216.5f);
        shape.LineTo(-87, 96.5f);
        shape.LineTo(-173, 12.5f);
        shape.LineTo(-53, -5.5f);
        shape.Close();
        shape.SetFill(0, 0, 255);
        shape.StrokeWidth(3);
        shape.StrokeFill(255, 255, 255);

        //Transform Matrix
        var m = new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);

        //scale x
        m.e11 = 1 - (progress * 0.5f);

        //scale y
        m.e22 = 1 + (progress * 2.0f);

        //rotation
        const float PI = 3.141592f;
        var degree = 45.0f;
        var radian = degree / 180.0f * PI;
        var cosVal = MathF.Cos(radian);
        var sinVal = MathF.Sin(radian);

        var t11 = m.e11 * cosVal + m.e12 * sinVal;
        var t12 = m.e11 * -sinVal + m.e12 * cosVal;
        var t21 = m.e21 * cosVal + m.e22 * sinVal;
        var t22 = m.e21 * -sinVal + m.e22 * cosVal;
        var t13 = m.e31 * cosVal + m.e32 * sinVal;
        var t23 = m.e31 * -sinVal + m.e32 * cosVal;

        m.e11 = t11;
        m.e12 = t12;
        m.e21 = t21;
        m.e22 = t22;
        m.e13 = t13;
        m.e23 = t23;

        //translate
        m.e13 = progress * 500.0f + 300.0f;
        m.e23 = progress * -100.0f + 380.0f;

        shape.Transform(m);

        canvas.Add(shape);

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, true, 960, 960);
}
