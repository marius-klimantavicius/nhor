using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Command Calls
        {
            //Star
            var shape1 = Shape.Gen();

            //Appends Paths
            shape1.MoveTo(199, 34);
            shape1.LineTo(253, 143);
            shape1.LineTo(374, 160);
            shape1.LineTo(287, 244);
            shape1.LineTo(307, 365);
            shape1.LineTo(199, 309);
            shape1.LineTo(97, 365);
            shape1.LineTo(112, 245);
            shape1.LineTo(26, 161);
            shape1.LineTo(146, 143);
            shape1.Close();
            shape1.SetFill(0, 0, 255);
            canvas.Add(shape1);

            //Circle
            var shape2 = Shape.Gen();

            var cx = 550.0f;
            var cy = 550.0f;
            var radius = 125.0f;
            var halfRadius = radius * 0.552284f;

            //Append Paths
            shape2.MoveTo(cx, cy - radius);
            shape2.CubicTo(cx + halfRadius, cy - radius, cx + radius, cy - halfRadius, cx + radius, cy);
            shape2.CubicTo(cx + radius, cy + halfRadius, cx + halfRadius, cy + radius, cx, cy + radius);
            shape2.CubicTo(cx - halfRadius, cy + radius, cx - radius, cy + halfRadius, cx - radius, cy);
            shape2.CubicTo(cx - radius, cy - halfRadius, cx - halfRadius, cy - radius, cx, cy - radius);
            shape2.Close();
            shape2.SetFill(255, 0, 0);
            canvas.Add(shape2);
        }

        //Commands Copy
        {
            /* Star */

            //Prepare Path Commands
            var cmds = new PathCommand[11];
            cmds[0] = PathCommand.MoveTo;
            cmds[1] = PathCommand.LineTo;
            cmds[2] = PathCommand.LineTo;
            cmds[3] = PathCommand.LineTo;
            cmds[4] = PathCommand.LineTo;
            cmds[5] = PathCommand.LineTo;
            cmds[6] = PathCommand.LineTo;
            cmds[7] = PathCommand.LineTo;
            cmds[8] = PathCommand.LineTo;
            cmds[9] = PathCommand.LineTo;
            cmds[10] = PathCommand.Close;

            //Prepare Path Points
            var pts = new Point[10];
            pts[0] = new Point(199, 34);    //MoveTo
            pts[1] = new Point(253, 143);   //LineTo
            pts[2] = new Point(374, 160);   //LineTo
            pts[3] = new Point(287, 244);   //LineTo
            pts[4] = new Point(307, 365);   //LineTo
            pts[5] = new Point(199, 309);   //LineTo
            pts[6] = new Point(97, 365);    //LineTo
            pts[7] = new Point(112, 245);   //LineTo
            pts[8] = new Point(26, 161);    //LineTo
            pts[9] = new Point(146, 143);   //LineTo

            var shape1 = Shape.Gen();
            shape1.AppendPath(cmds, 11, pts, 10);     //copy path data
            shape1.SetFill(0, 255, 0);
            shape1.Translate(400, 0);
            canvas.Add(shape1);

            /* Circle */
            var cx = 550.0f;
            var cy = 550.0f;
            var radius = 125.0f;
            var halfRadius = radius * 0.552284f;

            //Prepare Path Commands
            var cmds2 = new PathCommand[6];
            cmds2[0] = PathCommand.MoveTo;
            cmds2[1] = PathCommand.CubicTo;
            cmds2[2] = PathCommand.CubicTo;
            cmds2[3] = PathCommand.CubicTo;
            cmds2[4] = PathCommand.CubicTo;
            cmds2[5] = PathCommand.Close;

            //Prepare Path Points
            var pts2 = new Point[13];
            pts2[0] = new Point(cx, cy - radius);    //MoveTo
            //CubicTo 1
            pts2[1] = new Point(cx + halfRadius, cy - radius);      //Ctrl1
            pts2[2] = new Point(cx + radius, cy - halfRadius);      //Ctrl2
            pts2[3] = new Point(cx + radius, cy);                   //To
            //CubicTo 2
            pts2[4] = new Point(cx + radius, cy + halfRadius);      //Ctrl1
            pts2[5] = new Point(cx + halfRadius, cy + radius);      //Ctrl2
            pts2[6] = new Point(cx, cy + radius);                   //To
            //CubicTo 3
            pts2[7] = new Point(cx - halfRadius, cy + radius);      //Ctrl1
            pts2[8] = new Point(cx - radius, cy + halfRadius);      //Ctrl2
            pts2[9] = new Point(cx - radius, cy);                   //To
            //CubicTo 4
            pts2[10] = new Point(cx - radius, cy - halfRadius);     //Ctrl1
            pts2[11] = new Point(cx - halfRadius, cy - radius);     //Ctrl2
            pts2[12] = new Point(cx, cy - radius);                  //To

            var shape2 = Shape.Gen();
            shape2.AppendPath(cmds2, 6, pts2, 13);     //copy path data
            shape2.SetFill(255, 255, 0);
            shape2.Translate(-300, 0);
            canvas.Add(shape2);
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
