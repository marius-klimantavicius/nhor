using System;
using System.Collections.Generic;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    const int NUM_PER_ROW = 4;
    const int NUM_PER_COL = 4;

    List<LottieAnimation> slots = new List<LottieAnimation>();
    LottieAnimation? marker;
    LottieAnimation?[] resolver = new LottieAnimation?[2];  //picture, text
    uint w, h;
    uint size;

    void Sizing(Picture picture, uint counter)
    {
        picture.SetOrigin(0.5f, 0.5f);

        //image scaling preserving its aspect ratio
        float w, h;
        picture.GetSize(out w, out h);
        picture.Scale((w > h) ? size / w : size / h);
        picture.Translate((counter % NUM_PER_ROW) * size + size / 2, (counter / NUM_PER_ROW) * (this.h / NUM_PER_COL) + size / 2);
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        //slots
        foreach (var slot in slots)
        {
            slot.Frame(slot.TotalFrame() * Progress(elapsed, slot.Duration()));
        }

        //marker
        marker!.Frame(marker.TotalFrame() * Progress(elapsed, marker.Duration()));

        //asset resolvers
        resolver[0]!.Frame(resolver[0]!.TotalFrame() * Progress(elapsed, resolver[0]!.Duration()));
        resolver[1]!.Frame(resolver[1]!.TotalFrame() * Progress(elapsed, resolver[1]!.Duration()));

        canvas.Update();

        return true;
    }

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        //The default font for fallback in case
        Text.LoadFont(ExamplePaths.ExampleDir + "/font/PublicSans-Regular.ttf");

        //Background
        var bg = Shape.Gen();
        bg.AppendRect(0, 0, w, h);
        bg.SetFill(75, 75, 75);
        canvas.Add(bg);

        this.w = w;
        this.h = h;
        this.size = w / NUM_PER_ROW;

        //slot (default)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot0.json"))) return false;

            Sizing(picture, 0);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (gradient)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot1.json"))) return false;

            var slotJson = "{\"gradient_fill\":{\"p\":{\"p\":2,\"k\":{\"k\":[0,0.1,0.1,0.2,1,1,0.1,0.2,0,0,1,1]}}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 1);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (solid fill)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot2.json"))) return false;

            var slotJson = "{\"ball_color\":{\"p\":{\"a\":1,\"k\":[{\"i\":{\"x\":[0.833],\"y\":[0.833]},\"o\":{\"x\":[0.167],\"y\":[0.167]},\"t\":7,\"s\":[0,0.176,0.867]},{\"i\":{\"x\":[0.833],\"y\":[0.833]},\"o\":{\"x\":[0.167],\"y\":[0.167]},\"t\":22,\"s\":[0.867,0,0.533]},{\"i\":{\"x\":[0.833],\"y\":[0.833]},\"o\":{\"x\":[0.167],\"y\":[0.167]},\"t\":37,\"s\":[0.867,0,0.533]},{\"t\":51,\"s\":[0,0.867,0.255]}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 2);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (image)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot3.json"))) return false;

            var slotJson = "{\"path_img\":{\"p\":{\"id\":\"image_0\",\"w\":200,\"h\":300,\"u\":\"images/\",\"p\":\"logo.png\",\"e\":0}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 3);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (overridden default slot)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot4.json"))) return false;

            var slotJson = "{\"bg_color\":{\"p\":{\"a\":0,\"k\":[1,0.8196,0.2275]}},\"check_color\":{\"p\":{\"a\":0,\"k\":[0.0078,0.0078,0.0078]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 4);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (duplicate slots with default)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot5.json"))) return false;

            Sizing(picture, 5);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (transform: position)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot6.json"))) return false;

            var slotJson = "{\"position_id\":{\"p\":{\"a\":1,\"k\":[{\"i\":{\"x\":0.833,\"y\":0.833},\"o\":{\"x\":0.167,\"y\":0.167},\"s\":[100,100],\"t\":0},{\"s\":[200,300],\"t\":100}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 6);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (transform: scale)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot7.json"))) return false;

            var slotJson = "{\"scale_id\":{\"p\":{\"a\":1,\"k\":[{\"i\":{\"x\":0.833,\"y\":0.833},\"o\":{\"x\":0.167,\"y\":0.167},\"s\":[0,0],\"t\":0},{\"s\":[100,100],\"t\":100}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 7);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (transform: rotation)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot8.json"))) return false;

            var slotJson = "{\"rotation_id\":{\"p\":{\"a\":1,\"k\":[{\"i\":{\"x\":0.833,\"y\":0.833},\"o\":{\"x\":0.167,\"y\":0.167},\"s\":[0],\"t\":0},{\"s\":[180],\"t\":100}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 8);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (transform: opacity)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot9.json"))) return false;

            var slotJson = "{\"opacity_id\":{\"p\":{\"a\":1,\"k\":[{\"i\":{\"x\":0.833,\"y\":0.833},\"o\":{\"x\":0.167,\"y\":0.167},\"s\":[0],\"t\":0},{\"s\":[100],\"t\":100}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 9);

            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (expression)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot10.json"))) return false;

            var slotJson = "{\"rect_rotation\":{\"p\":{\"x\":\"var $bm_rt = time * 360;\"}},\"rect_scale\":{\"p\":{\"x\":\"var $bm_rt = [];$bm_rt[0] = value[0] + Math.cos(2 * Math.PI * time) * 100;$bm_rt[1] = value[1];\"}},\"rect_position\":{\"p\":{\"x\":\"var $bm_rt = [];$bm_rt[0] = value[0] + Math.cos(2 * Math.PI * time) * 100;$bm_rt[1] = value[1];\"}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 10);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (text)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot11.json"))) return false;

            var slotJson = "{\"text_doc\":{\"p\":{\"k\":[{\"s\":{\"f\":\"Ubuntu Light Italic\",\"t\":\"ThorVG!\",\"j\":0,\"s\":48,\"fc\":[1,1,1]},\"t\":0}]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 11);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //slot (text range)
        {
            var slot = LottieAnimation.Gen();
            var picture = slot.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/slot12.json"))) return false;

            var slotJson = "{\"texty\":{\"p\":{\"a\":0,\"k\":[1,0.5,0]}}}";
            var slotId = slot.Gen(slotJson);
            if (!Verify(slot.Apply(slotId))) return false;

            Sizing(picture, 12);
            canvas.Add(picture);
            slots.Add(slot);
        }

        //marker
        {
            marker = LottieAnimation.Gen();
            var picture = marker.GetPicture();
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/marker.json"))) return false;
            if (!Verify(marker.Segment("sectionC"))) return false;

            Sizing(picture, 13);
            canvas.Add(picture);
        }

        //asset resolver (image)
        {
            resolver[0] = LottieAnimation.Gen();
            var picture = resolver[0]!.GetPicture();

            Func<Paint, string, object?, bool> func = (Paint p, string src, object? data) =>
            {
                if (p.PaintType() != ThorVG.Type.Picture) return false;
                //The engine may fail to access the source image. This demonstrates how to resolve it using a user valid source.
                var prefix = ExamplePaths.ExampleDir + "/lottie/extensions/";
                var assetPath = src.StartsWith(prefix) ? ExamplePaths.ExampleDir + "/" + src.Substring(prefix.Length) : src;
                var ret = ((Picture)p).Load(assetPath);
                return (ret == Result.Success);  //return true if the resolving is successful
            };

            //set a resolver prior to load a resource
            if (!Verify(picture.Resolver(func, null))) return false;
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/resolver1.json"))) return false;

            Sizing(picture, 14);
            canvas.Add(picture);
        }

        //asset resolver (font)
        {
            resolver[1] = LottieAnimation.Gen();
            var picture = resolver[1]!.GetPicture();

            Func<Paint, string, object?, bool> func = (Paint p, string src, object? data) =>
            {
                if (p.PaintType() != ThorVG.Type.Text) return false;
                //The engine may fail to access the source image. This demonstrates how to resolve it using a user valid source.
                var assetPath = ExamplePaths.ExampleDir + "/" + src;
                if (!Verify(Text.LoadFont(assetPath))) return false;
                var ret = ((Text)p).SetFont("SentyCloud");
                return (ret == Result.Success);  //return true if font loading is successful
            };

            if (!Verify(picture.Resolver(func, null))) return false;
            if (!Verify(picture.Load(ExamplePaths.ExampleDir + "/lottie/extensions/resolver2.json"))) return false;

            Sizing(picture, 15);
            canvas.Add(picture);
        }

        return true;
    }
}


/************************************************************************/
/* Entry Point                                                          */
/************************************************************************/

class Program
{
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 1024, 1024, 0);
}
