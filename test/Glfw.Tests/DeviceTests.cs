using System;
using System.Text.Json;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    private static void ValidateVirtualDevice(string name)
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11); Check(Init() == 1, "evdev init");
        int connected = 0, disconnected = 0;
        G.glfwSetJoystickCallback((_, evt) => { if (evt == GLFW_CONNECTED) connected++; else disconnected++; });
        Console.WriteLine("READY");
        string? command; int jid = -1;
        while ((command = Console.ReadLine()) != null)
        {
            for (int i = 0; i < 100; i++)
            {
                G.glfwPollEvents();
                if (command == "connect")
                {
                    for (int j = 0; j <= GLFW_JOYSTICK_LAST; j++) if (G.glfwGetJoystickName(j) == name) { jid = j; break; }
                    if (jid >= 0) break;
                }
                if (command == "disconnect" && !G.glfwJoystickPresent(jid)) break;
                if (command == "state") break;
                G.glfwWaitEventsTimeout(0.02);
            }
            if (command == "connect") Console.WriteLine(JsonSerializer.Serialize(new { present = jid >= 0, connected }));
            if (command == "state")
            {
                var axes = G.glfwGetJoystickAxes(jid, out _);
                var buttons = G.glfwGetJoystickButtons(jid, out _)!;
                var hats = G.glfwGetJoystickHats(jid, out _)!;
                Console.WriteLine(JsonSerializer.Serialize(new { axes, buttons = Array.ConvertAll(buttons, b => (int)b), hats = Array.ConvertAll(hats, b => (int)b), guid = G.glfwGetJoystickGUID(jid) }));
            }
            if (command == "disconnect") { Console.WriteLine(JsonSerializer.Serialize(new { present = G.glfwJoystickPresent(jid), disconnected })); break; }
        }
        Terminate();
    }
}
