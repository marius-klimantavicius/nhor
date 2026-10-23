/* Independent fixture linked to pristine GLFW, not the managed implementation.
 * Copyright (c) 2026 Nhor contributors. zlib license (ref/glfw/LICENSE.md).
 */
#include "internal.h"
#include <stdio.h>
#include <string.h>

static GLFWbool pollFixture(_GLFWjoystick* js, int mode) { return GLFW_TRUE; }
static char inputEvents[2048];
static void character(GLFWwindow* w, unsigned int code) { char s[80]; snprintf(s,sizeof(s),"%s\"char:%u\"",inputEvents[0] ? "," : "",code); strcat(inputEvents,s); }
static void charmods(GLFWwindow* w, unsigned int code, int mods) { char s[80]; snprintf(s,sizeof(s),"%s\"mods:%u:%d\"",inputEvents[0] ? "," : "",code,mods); strcat(inputEvents,s); }
static void scale(GLFWwindow* w, float x, float y) { char s[80]; snprintf(s,sizeof(s),"%s\"scale:%.1f:%.1f\"",inputEvents[0] ? "," : "",x,y); strcat(inputEvents,s); }
int main(void)
{
    glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_NULL);
    if (!glfwInit()) return 1;
    _glfw.platform.pollJoystick = pollFixture;
    const char* guid = "0123456789abcdef0123456789abcdef";
    _GLFWjoystick* js = _glfwAllocJoystick("fixture", guid, 4, 3, 1);
    _glfwInputJoystick(js, GLFW_CONNECTED);
    _glfwInputJoystickAxis(js, 0, 0.25f); _glfwInputJoystickAxis(js, 1, -0.75f);
    _glfwInputJoystickAxis(js, 2, 1.f); _glfwInputJoystickAxis(js, 3, -1.f);
    _glfwInputJoystickButton(js, 0, 1); _glfwInputJoystickHat(js, 0, GLFW_HAT_RIGHT_UP);
    const char* definitions[] = {
        "a:b0,b:b1,x:+a0,y:-a1,leftx:a0~,lefty:+a1,rightx:h0.2,righty:b0,lefttrigger:-a3,righttrigger:a2,dpup:h0.1,dpright:h0.2,",
        "a:b99,leftx:a0,", "a:b0,leftx:a99,", "a:b0,leftx:-a0~,lefttrigger:b1,righttrigger:h0.1,"
    };
    puts("{");
    printf("\"default-mapping-count\":%d,\n", _glfw.mappingCount);
    for (int scenario = 0; scenario < 4; scenario++)
    {
        char mapping[1024]; snprintf(mapping, sizeof(mapping), "%s,Fixture,%s", guid, definitions[scenario]);
        if (!glfwUpdateGamepadMappings(mapping)) return 2;
        GLFWgamepadstate state;
        int valid = glfwGetGamepadState(0, &state);
        printf("\"mapping-%d-valid\":%s,\n", scenario, valid ? "true" : "false");
        printf("\"mapping-%d-buttons\":[", scenario);
        for (int i = 0; i < 15; i++) printf("%s%d", i ? "," : "", state.buttons[i]);
        puts("],");
        printf("\"mapping-%d-axes\":[", scenario);
        for (int i = 0; i < 6; i++) printf("%s%.9g", i ? "," : "", state.axes[i]);
        puts("],");
        const char* name = glfwGetGamepadName(0);
        printf("\"mapping-%d-name\":%s,\n", scenario, name ? "\"Fixture\"" : "null");
    }
    int count; const unsigned char* buttons = glfwGetJoystickButtons(0, &count);
    printf("\"hat-button-count\":%d,\n\"hat-buttons\":[", count);
    for (int i = 0; i < count; i++) printf("%s%d", i ? "," : "", buttons[i]);
    puts("],");
    glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
    GLFWwindow* window = glfwCreateWindow(32, 32, "fixture", NULL, NULL);
    glfwSetCharCallback(window, character); glfwSetCharModsCallback(window, charmods); glfwSetWindowContentScaleCallback(window, scale);
    _glfwInputChar((_GLFWwindow*)window, 1, 0, GLFW_TRUE);
    _glfwInputChar((_GLFWwindow*)window, 127, 0, GLFW_TRUE);
    _glfwInputChar((_GLFWwindow*)window, 0x1f600, GLFW_MOD_CAPS_LOCK, GLFW_TRUE);
    glfwSetInputMode(window, GLFW_LOCK_KEY_MODS, GLFW_TRUE);
    _glfwInputChar((_GLFWwindow*)window, 0x10437, GLFW_MOD_CAPS_LOCK, GLFW_FALSE);
    _glfwInputWindowContentScale((_GLFWwindow*)window, 1.5f, 2.f);
    printf("\"unicode-scale-events\":[%s],\n", inputEvents);
    char uriText[] = "# comment\r\nfile://localhost/home/test/a%20b%20\r\nrelative%20path\nfile:///tmp/%E2%82%AC.txt\n";
    int pathCount; char** paths = _glfwParseUriList(uriText, &pathCount);
    printf("\"parsed-uris\":[");
    for (int i = 0; i < pathCount; i++) { printf("%s\"%s\"", i ? "," : "", paths[i]); _glfw_free(paths[i]); }
    puts("]}"); _glfw_free(paths);
    glfwDestroyWindow(window);
    _glfwFreeJoystick(js); glfwTerminate(); return 0;
}
