using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Solid Rectangle
        {
            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 400, 400);

            //Mask
            var mask = Shape.Gen();
            mask.AppendCircle(200, 200, 125, 125);
            mask.SetFill(255, 0, 0);

            var fill = LinearGradient.Gen();
            fill.Linear(0, 0, 400, 400);
            Fill.ColorStop[] colorStops = new Fill.ColorStop[2];
            colorStops[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops[1] = new Fill.ColorStop(1, 255, 255, 255, 255);
            fill.SetColorStops(colorStops, 2);
            shape.SetFill(fill);

            shape.SetMask(mask, MaskMethod.Alpha);
            canvas.Add(shape);
        }

        //Star
        {
            var shape1 = Shape.Gen();
            shape1.MoveTo(599, 34);
            shape1.LineTo(653, 143);
            shape1.LineTo(774, 160);
            shape1.LineTo(687, 244);
            shape1.LineTo(707, 365);
            shape1.LineTo(599, 309);
            shape1.LineTo(497, 365);
            shape1.LineTo(512, 245);
            shape1.LineTo(426, 161);
            shape1.LineTo(546, 143);
            shape1.Close();

            //Mask
            var mask1 = Shape.Gen();
            mask1.AppendCircle(600, 200, 125, 125);
            mask1.SetFill(255, 0, 0);

            var fill1 = LinearGradient.Gen();
            fill1.Linear(400, 0, 800, 400);
            Fill.ColorStop[] colorStops1 = new Fill.ColorStop[2];
            colorStops1[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops1[1] = new Fill.ColorStop(1, 1, 255, 255, 255);
            fill1.SetColorStops(colorStops1, 2);
            shape1.SetFill(fill1);

            shape1.SetMask(mask1, MaskMethod.Alpha);
            canvas.Add(shape1);
        }

        //Solid Rectangle
        {
            var shape2 = Shape.Gen();
            shape2.AppendRect(0, 400, 400, 400);

            //Mask
            var mask2 = Shape.Gen();
            mask2.AppendCircle(200, 600, 125, 125);
            mask2.SetFill(255, 0, 0);

            var fill2 = LinearGradient.Gen();
            fill2.Linear(0, 400, 400, 800);
            Fill.ColorStop[] colorStops2 = new Fill.ColorStop[2];
            colorStops2[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops2[1] = new Fill.ColorStop(1, 255, 255, 255, 255);
            fill2.SetColorStops(colorStops2, 2);
            shape2.SetFill(fill2);

            shape2.SetMask(mask2, MaskMethod.InvAlpha);
            canvas.Add(shape2);
        }

        // Star
        {
            var shape3 = Shape.Gen();
            shape3.MoveTo(599, 434);
            shape3.LineTo(653, 543);
            shape3.LineTo(774, 560);
            shape3.LineTo(687, 644);
            shape3.LineTo(707, 765);
            shape3.LineTo(599, 709);
            shape3.LineTo(497, 765);
            shape3.LineTo(512, 645);
            shape3.LineTo(426, 561);
            shape3.LineTo(546, 543);
            shape3.Close();

            //Mask
            var mask3 = Shape.Gen();
            mask3.AppendCircle(600, 600, 125, 125);
            mask3.SetFill(255, 0, 0);

            var fill3 = LinearGradient.Gen();
            fill3.Linear(400, 400, 800, 800);
            Fill.ColorStop[] colorStops3 = new Fill.ColorStop[2];
            colorStops3[0] = new Fill.ColorStop(0, 0, 0, 0, 255);
            colorStops3[1] = new Fill.ColorStop(1, 1, 255, 255, 255);
            fill3.SetColorStops(colorStops3, 2);
            shape3.SetFill(fill3);

            shape3.SetMask(mask3, MaskMethod.InvAlpha);
            canvas.Add(shape3);
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
