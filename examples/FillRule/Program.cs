using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Star
        var shape1 = Shape.Gen();
        shape1.MoveTo(205, 35);
        shape1.LineTo(330, 355);
        shape1.LineTo(25, 150);
        shape1.LineTo(385, 150);
        shape1.LineTo(80, 355);
        shape1.Close();
        shape1.SetFill(255, 255, 255);
        // Use the NonZero fill rule: fills all areas enclosed by paths with non-zero winding numbers
        shape1.SetFillRule(FillRule.NonZero);

        canvas.Add(shape1);

        //Star 2
        var shape2 = Shape.Gen();
        shape2.MoveTo(535, 335);
        shape2.LineTo(660, 655);
        shape2.LineTo(355, 450);
        shape2.LineTo(715, 450);
        shape2.LineTo(410, 655);
        shape2.Close();
        shape2.SetFill(255, 255, 255);
        // Use the EvenOdd fill rule: fills areas where path overlaps an odd number of times
        shape2.SetFillRule(FillRule.EvenOdd);

        canvas.Add(shape2);

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
