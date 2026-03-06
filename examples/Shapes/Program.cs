using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Prepare a Composite Shape (Rectangle + Rectangle + Circle + Circle)
        var shape4 = Shape.Gen();
        shape4.AppendRect(0, 0, 300, 300, 50, 50);     //x, y, w, h, rx, ry
        shape4.AppendCircle(400, 150, 150, 150);       //cx, cy, radiusW, radiusH
        shape4.AppendCircle(600, 150, 150, 100);       //cx, cy, radiusW, radiusH
        shape4.SetFill(255, 255, 0);                   //r, g, b
        canvas.Add(shape4);

        //Prepare Round Rectangle
        var shape1 = Shape.Gen();
        shape1.AppendRect(0, 450, 300, 300, 50, 50);  //x, y, w, h, rx, ry
        shape1.SetFill(0, 255, 0);                     //r, g, b
        canvas.Add(shape1);

        //Prepare Circle
        var shape2 = Shape.Gen();
        shape2.AppendCircle(400, 600, 150, 150);      //cx, cy, radiusW, radiusH
        shape2.SetFill(255, 255, 0);                   //r, g, b
        canvas.Add(shape2);

        //Prepare Ellipse
        var shape3 = Shape.Gen();
        shape3.AppendCircle(600, 600, 150, 100);      //cx, cy, radiusW, radiusH
        shape3.SetFill(0, 255, 255);                   //r, g, b
        canvas.Add(shape3);

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
