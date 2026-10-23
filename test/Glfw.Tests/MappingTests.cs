using System;
using System.Collections.Generic;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    // Mirrors the independently linked native fixture. Only the device polling
    // seam is replaced; parsing, allocation, validation and transforms are real.
    private sealed class MappingPlatform : Glfw.NullPlatform, Glfw.IGlfwPlatform
    { public new bool PollJoystick(Glfw.GlfwJoystick js, int mode) => true; }
    private static void ValidateMappings()
    {
        InitHint(GLFW_PLATFORM, GLFW_PLATFORM_NULL); Check(Init() == 1, "mapping init");
        Glfw._glfw.platform = new MappingPlatform();
        Record("default-mapping-count", Glfw._glfw.mappings.Count);
        const string guid = "0123456789abcdef0123456789abcdef";
        var js = G._glfwAllocJoystick("fixture", guid, 4, 3, 1)!;
        G._glfwInputJoystick(js, GLFW_CONNECTED);
        G._glfwInputJoystickAxis(js, 0, 0.25f); G._glfwInputJoystickAxis(js, 1, -0.75f);
        G._glfwInputJoystickAxis(js, 2, 1f); G._glfwInputJoystickAxis(js, 3, -1f);
        G._glfwInputJoystickButton(js, 0, 1); G._glfwInputJoystickHat(js, 0, GLFW_HAT_RIGHT_UP);
        string[] definitions = {
            "a:b0,b:b1,x:+a0,y:-a1,leftx:a0~,lefty:+a1,rightx:h0.2,righty:b0,lefttrigger:-a3,righttrigger:a2,dpup:h0.1,dpright:h0.2,",
            "a:b99,leftx:a0,", // invalid hardware element rejects the entire mapping
            "a:b0,leftx:a99,",
            "a:b0,leftx:-a0~,lefttrigger:b1,righttrigger:h0.1,",
        };
        for (int i = 0; i < definitions.Length; i++)
        {
            Check(G.glfwUpdateGamepadMappings(guid + ",Fixture," + definitions[i]), "mapping update");
            bool success = G.glfwGetGamepadState(0, out var state);
            Record("mapping-" + i + "-valid", success);
            Record("mapping-" + i + "-buttons", Array.ConvertAll(state.Buttons, b => (int)b));
            Record("mapping-" + i + "-axes", state.Axes);
            Record("mapping-" + i + "-name", G.glfwGetGamepadName(0));
        }
        Record("hat-buttons", Array.ConvertAll(G.glfwGetJoystickButtons(0, out int count)!, b => (int)b)); Record("hat-button-count", count);
        object pointer = new(); G.glfwSetJoystickUserPointer(0, pointer); Check(ReferenceEquals(pointer, G.glfwGetJoystickUserPointer(0)), "joystick user pointer");
        G.glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
        var window = G.glfwCreateWindow(32, 32, "fixture", null, null)!;
        var events = new List<string>();
        G.glfwSetCharCallback(window, (_, code) => events.Add($"char:{code}"));
        G.glfwSetCharModsCallback(window, (_, code, mods) => events.Add($"mods:{code}:{mods}"));
        G.glfwSetWindowContentScaleCallback(window, (_, x, y) => events.Add($"scale:{x:0.0}:{y:0.0}"));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        G._glfwInputChar(window, 1, 0, true); G._glfwInputChar(window, 127, 0, true);
        G._glfwInputChar(window, 0x1f600, GLFW_MOD_CAPS_LOCK, true);
        G.glfwSetInputMode(window, GLFW_LOCK_KEY_MODS, 1);
        G._glfwInputChar(window, 0x10437, GLFW_MOD_CAPS_LOCK, false);
        G._glfwInputWindowContentScale(window, 1.5f, 2f);
        Record("unicode-scale-events", events);
        Check(events.Contains("char:128512") && events.Contains("mods:66615:16"), "supplementary Unicode scalar callbacks");
        Check(G.MergeWin32Scancode(0x100, 0x2f) == 0x12f, "extended synthetic scancode survives recovery");
        Check(G.MergeWin32Scancode(0, 0x2f) == 0x2f, "ordinary synthetic scancode recovery");
        Record("parsed-uris", G._glfwParseUriList("# comment\r\nfile://localhost/home/test/a%20b%20\r\nrelative%20path\nfile:///tmp/%E2%82%AC.txt\n"));
        G.glfwDestroyWindow(window);
        G._glfwFreeJoystick(js);
        Terminate();
    }
}
