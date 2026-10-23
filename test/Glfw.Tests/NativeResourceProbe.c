#include <GLFW/glfw3.h>
#include <dirent.h>
#include <stdio.h>
#include <string.h>
static int descriptors(void) { int count = 0; DIR* dir = opendir("/proc/self/fd"); struct dirent* entry; while ((entry=readdir(dir))) if (strcmp(entry->d_name,".") && strcmp(entry->d_name,"..")) count++; closedir(dir); return count; }
static int cycle(void)
{
    if (!glfwInit()) return 1;
    glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE); glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
    for (int i = 0; i < 10; i++) { GLFWwindow* w = glfwCreateWindow(32,32,"native resource test",NULL,NULL); if (!w) return 2; glfwDestroyWindow(w); }
    glfwTerminate(); return 0;
}
int main(void)
{
    glfwInitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
    for (int i = 0; i < 3; i++) if (cycle()) return 3;
    int before = descriptors();
    for (int i = 0; i < 20; i++) if (cycle()) return 4;
    int after = descriptors();
    printf("{\"initializationCycles\":23,\"windowCycles\":230,\"descriptorsBefore\":%d,\"descriptorsAfter\":%d}\n",before,after);
    return before == after ? 0 : 5;
}
