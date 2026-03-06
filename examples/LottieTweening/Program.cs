using System;
using System.Collections.Generic;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    LottieAnimation? lottie;

    //designed the states: [angry, sad, mourn, wink, laughing]
    struct AnimState
    {
        public string name;           //state name
        public float begin;           //state begin frame number
    }

    List<AnimState> states = new List<AnimState>();  //states list
    int stateIdx = 0;               //current state index

    float tweenFrom;           //tweening from frame number
    float tweenTo;             //tweening to frame number
    float tweenBeginTime;      //tweening begin time
    bool tweenActive = false;  //whether on-tweening or not

    void Init()
    {
        //get the AnimState info (state name and its begin frame number)
        for (uint i = 0; i < lottie!.MarkersCnt(); ++i)
        {
            //specify the current segment to retrieve the segment's starting frame.
            float begin;
            var name = lottie.Marker(i);
            lottie.Segment(name);
            lottie.Segment(out begin, out _);

            //save the current AnimState to the state list
            states.Add(new AnimState { name = name!, begin = begin });
        }

        //set the default state (Angry)
        lottie.Segment(states[stateIdx].name);
    }

    //stateIdx is the next desired state
    void StartTweening(int stateIdx)
    {
        //don't allow the overlapped tweening
        if (tweenActive || stateIdx == this.stateIdx) return;

        //reset the current state
        lottie!.Segment((string?)null);

        //tweening trigger time
        tweenBeginTime = Timestamp();

        //the current animation frame as the tweening "from" frame
        tweenFrom = lottie.CurFrame();

        //the next state begin frame as the tweening "to" frame
        tweenTo = states[stateIdx].begin;

        tweenActive = true;

        this.stateIdx = stateIdx;

        Console.WriteLine("tween to: " + states[stateIdx].name);
    }

    public override bool ClickDown(Canvas canvas, int x, int y)
    {
        int i = 0;
        foreach (var state in states)
        {
            var paint = lottie!.GetPicture().FindPaint(Accessor.Id(state.name));
            if (paint != null)
            {
                //hit a emoji layer!
                if (paint.Intersects(x, y, 1, 1))
                {
                    StartTweening(i);
                    return false;
                }
            }
            ++i;
        }
        return false;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //Animation Controller
        lottie = LottieAnimation.Gen();
        var picture = lottie.GetPicture();
        picture.SetOrigin(0.5f, 0.5f);  //center origin

        //Background
        var shape = Shape.Gen();
        shape.AppendRect(0, 0, w, h);
        shape.SetFill(50, 50, 50);

        canvas.Add(shape);

        if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/emoji.json"))) return false;

        //image scaling preserving its aspect ratio
        float w2, h2;
        picture.GetSize(out w2, out h2);
        var scale = (w2 > h2) ? w / w2 : h / h2;
        picture.Scale(scale);
        picture.Translate((float)w * 0.5f, (float)h * 0.5f);

        canvas.Add(picture);

        Init();

        return true;
    }

    bool Tweening(Canvas canvas)
    {
        //perform tweening for 0.25 seconds.
        //in this sample, we use linear interpolation. You can vary the progress
        //with a specific interpolation style (e.g., sine, cosine, or spring curves).
        var progress = (Timestamp() - tweenBeginTime) / 0.25f;

        //perform the tweening effect
        if (progress < 1.0f)
        {
            if (lottie!.Tween(tweenFrom, tweenTo, progress) == Result.Success)
            {
                canvas.Update();
                return true;
            }
        //tweening is over, set to the desired state
        }
        else
        {
            lottie!.Segment(states[stateIdx].name);
            tweenActive = false;
            Elapsed = 0;

            //tweening is over, start to the desired state play
            if (lottie.Frame(0) == Result.Success)
            {
                canvas.Update();
                return true;
            }
        }

        return false;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        //on state tweening
        if (tweenActive) return Tweening(canvas);

        //play the current state
        var progress = Progress(elapsed, lottie!.Duration());

        //Update animation frame only when it's changed
        if (lottie.Frame(lottie.TotalFrame() * progress) == Result.Success)
        {
            canvas.Update();
            return true;
        }

        return false;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1024, 1024);
}
