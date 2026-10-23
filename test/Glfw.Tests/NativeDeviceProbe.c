/* Real evdev enumeration/hotplug fixture using public upstream GLFW APIs. */
#include <GLFW/glfw3.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
static int connected, disconnected;
static void joystick(int jid, int event) { if (event == GLFW_CONNECTED) connected++; else disconnected++; }
static int find(const char* name)
{
    for (int jid = 0; jid <= GLFW_JOYSTICK_LAST; jid++)
    {
        const char* n = glfwGetJoystickName(jid);
        if (n && strcmp(n, name) == 0) return jid;
    }
    return -1;
}
int main(int argc, char** argv)
{
    if (argc != 2) return 2;
    glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
    if (!glfwInit()) return 3;
    glfwSetJoystickCallback(joystick);
    puts("READY"); fflush(stdout);
    char command[64]; int jid = -1;
    while (fgets(command, sizeof(command), stdin))
    {
        for (int i = 0; i < 100; i++)
        {
            glfwPollEvents();
            if (command[0] == 'c' && (jid = find(argv[1])) >= 0) break;
            if (command[0] == 'd' && !glfwJoystickPresent(jid)) break;
            if (command[0] == 's') break;
            glfwWaitEventsTimeout(0.02);
        }
        if (command[0] == 'c') printf("{\"present\":%s,\"connected\":%d}\n", jid >= 0 ? "true" : "false", connected);
        if (command[0] == 's')
        {
            int ac, bc, hc;
            const float* a = glfwGetJoystickAxes(jid, &ac);
            const unsigned char* b = glfwGetJoystickButtons(jid, &bc);
            const unsigned char* h = glfwGetJoystickHats(jid, &hc);
            printf("{\"axes\":["); for (int i = 0; i < ac; i++) printf("%s%.9g", i ? "," : "", a[i]);
            printf("],\"buttons\":["); for (int i = 0; i < bc; i++) printf("%s%d", i ? "," : "", b[i]);
            printf("],\"hats\":["); for (int i = 0; i < hc; i++) printf("%s%d", i ? "," : "", h[i]);
            printf("],\"guid\":\"%s\"}\n", glfwGetJoystickGUID(jid));
        }
        if (command[0] == 'd') { printf("{\"present\":%s,\"disconnected\":%d}\n", glfwJoystickPresent(jid) ? "true" : "false", disconnected); fflush(stdout); break; }
        fflush(stdout);
    }
    glfwTerminate(); return 0;
}
