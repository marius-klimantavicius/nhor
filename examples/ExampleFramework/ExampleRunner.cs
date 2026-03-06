namespace ThorVG.Examples;

public static class ExampleRunner
{
    public static int Run(ExampleBase example, string[] args,
        bool clearBuffer = false, uint w = 800, uint h = 800,
        uint threadsCnt = 4, bool print = false)
    {
        int engine = 0; //0: sw, 1: gl

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--res" && i + 1 < args.Length)
            {
                ExamplePaths.SetResDir(args[++i]);
            }
            else if (args[i] == "gl") engine = 1;
            else if (args[i] == "sw") engine = 0;
        }

        unsafe
        {
            ExampleWindow window;

            if (engine == 0)
                window = new SwWindow(example, w, h, threadsCnt);
            else
                window = new GlWindow(example, w, h, threadsCnt);

            window.ClearBuffer = clearBuffer;
            window.Print = print;

            if (window.Ready())
            {
                window.Show();
            }

            window.Dispose();
        }

        return 0;
    }
}
