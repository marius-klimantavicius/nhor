// Public GLFW API plus owned deterministic RandR responses. Link this against
// the pristine target's static library, with the matching platform defines.
#include "internal.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static XRRModeInfo mi;
static XRRScreenResources resources;
static XRRCrtcInfo ci;
static int missingCrtc, resourcesFreed, crtcsFreed, assertions;
static XRRScreenResources* getResources(Display* d, Window w) { return &resources; }
static XRRCrtcInfo* getCrtc(Display* d, XRRScreenResources* r, RRCrtc c) { return missingCrtc ? NULL : &ci; }
static void freeResources(XRRScreenResources* r) { resourcesFreed++; }
static void freeCrtc(XRRCrtcInfo* c) { crtcsFreed++; }
static void check(int pass, const char* description) { assertions++; if (!pass) { fprintf(stderr, "%s\n", description); exit(1); } }

int main(void)
{
    glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
    check(glfwInit(), "native deterministic dispatch init");
    GLFWmonitor* monitor = glfwGetPrimaryMonitor();
    check(monitor != NULL, "primary monitor");
    _glfw.x11.randr.available = GLFW_TRUE;
    _glfw.x11.randr.monitorBroken = GLFW_FALSE;
    _glfw.x11.randr.GetScreenResourcesCurrent = getResources;
    _glfw.x11.randr.GetCrtcInfo = getCrtc;
    _glfw.x11.randr.FreeScreenResources = freeResources;
    _glfw.x11.randr.FreeCrtcInfo = freeCrtc;
    mi.id = 7; mi.width = 800; mi.height = 600; mi.dotClock = 121; mi.hTotal = 2; mi.vTotal = 1;
    resources.nmode = 1; resources.modes = &mi; ci.mode = 7;
    const int rotations[] = { 1, 2, 4, 8, 17, 18 };
    printf("{");
    for (int i = 0; i < 6; i++)
    {
        ci.rotation = rotations[i];
        const GLFWvidmode* mode = glfwGetVideoMode(monitor);
        check(mode != NULL, "native rotation result");
        int swap = ci.rotation == 2 || ci.rotation == 8;
        check(mode->width == (swap ? 600 : 800) && mode->height == (swap ? 800 : 600), "native rotation dimensions");
        check(mode->refreshRate == 61, "native round tie");
        printf("\"rotation-%d\":[%d,%d,%d,%d,%d,%d],", rotations[i], mode->width, mode->height, mode->redBits, mode->greenBits, mode->blueBits, mode->refreshRate);
    }
    ci.mode = 8;
    check(glfwGetVideoMode(monitor) == NULL, "native missing mode fails");
    const char* message;
    check(glfwGetError(&message) == GLFW_PLATFORM_ERROR && strcmp(message, "X11: Failed to query video mode") == 0, "native missing mode exact error");
    missingCrtc = 1;
    check(glfwGetVideoMode(monitor) == NULL, "native missing CRTC fails");
    check(glfwGetError(NULL) == GLFW_PLATFORM_ERROR, "native missing CRTC error");
    check(resourcesFreed == 8 && crtcsFreed == 7, "native response cleanup");
    printf("\"resources-freed\":%d,\"crtcs-freed\":%d,\"assertions\":%d}\n", resourcesFreed, crtcsFreed, assertions);
    // Process exit releases this fixture's state; don't let monitor teardown
    // consume fake RandR objects. No display configuration was modified.
    _glfw.x11.randr.available = GLFW_FALSE;
    glfwTerminate();
    return 0;
}
