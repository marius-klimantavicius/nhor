using System;
using System.Collections.Generic;
using ThorVG;
using ThorVG.Examples;

/************************************************************************/
/* ThorVG Drawing Contents                                              */
/************************************************************************/

class UserExample : ExampleBase
{
    struct Particle
    {
        public Paint obj;
        public float x, y;
        public float speed;
        public float size;
    }

    const float COUNT = 1200.0f;
    List<Particle> raindrops = new List<Particle>();
    List<Particle> clouds = new List<Particle>();

    uint w, h;
    Random rng = new Random(100);

    public override bool Content(Canvas canvas, uint w, uint h)
    {
        var city = Picture.Gen();
        city.Load(ExamplePaths.ExampleDir + "/image/particle.jpg");
        canvas.Add(city);

        var cloud1 = Picture.Gen();
        cloud1.Load(ExamplePaths.ExampleDir + "/image/clouds.png");
        cloud1.Opacity(60);
        canvas.Add(cloud1);

        float size;
        cloud1.GetSize(out size, out _);
        clouds.Add(new Particle { obj = cloud1, x = 0, y = 0, speed = 0.25f, size = size });

        var cloud2 = cloud1.Duplicate();
        cloud2.Opacity(30);
        cloud2.Translate(400, 100);
        canvas.Add(cloud2);

        clouds.Add(new Particle { obj = cloud2, x = 400, y = 100, speed = 0.125f, size = size });

        var cloud3 = cloud1.Duplicate();
        cloud3.Opacity(20);
        cloud3.Translate(1200, 200);
        canvas.Add(cloud3);

        clouds.Add(new Particle { obj = cloud3, x = 1200, y = 200, speed = 0.075f, size = size });

        var darkness = Shape.Gen();
        darkness.AppendRect(0, 0, w, h);
        darkness.SetFill(0, 0, 0, 150);
        canvas.Add(darkness);

        //rain drops
        float dropSize = w / COUNT;

        for (int i = 0; i < (int)COUNT; ++i)
        {
            var shape = Shape.Gen();
            float x = dropSize * i;
            raindrops.Add(new Particle { obj = shape, x = x, y = (float)(rng.Next() % h), speed = 10 + (float)(rng.Next() % 100) * 0.1f, size = 0 });
            shape.AppendRect(0, 0, 1, rng.Next() % 15 + dropSize);
            shape.SetFill(255, 255, 255, (byte)(55 + rng.Next() % 100));
            canvas.Add(shape);
        }

        this.w = w;
        this.h = h;

        return true;
    }

    public override bool Update(Canvas canvas, uint elapsed)
    {
        for (int i = 0; i < raindrops.Count; i++)
        {
            var p = raindrops[i];
            p.y += p.speed;
            if (p.y > h)
            {
                p.y -= h;
            }
            p.obj.Translate(p.x, p.y);
            raindrops[i] = p;
        }

        for (int i = 0; i < clouds.Count; i++)
        {
            var p = clouds[i];
            p.x -= p.speed;
            if (p.x + p.size < 0)
            {
                p.x = w;
            }
            p.obj.Translate(p.x, p.y);
            clouds[i] = p;
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
    static int Main(string[] args) => ExampleRunner.Run(new UserExample(), args, false, 2440, 1280, 0, true);
}
