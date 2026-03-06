/*
 * lottie2gif-gl — converts Lottie animations to GIF using ThorVG GL renderer.
 * Requires GLFW for GL context creation.
 */

#include <iostream>
#include <string>
#include <cstring>
#include <vector>
#include <thorvg.h>
#include <GLFW/glfw3.h>

// Access the internal GIF encoder directly
#include "tvgGifEncoder.h"

// Need glReadPixels — declared in tvgGl.h but we access via GLFW's loader
typedef void (*PFNGLREADPIXELSPROC_LOCAL)(int, int, int, int, unsigned int, unsigned int, void*);

using namespace std;
using namespace tvg;

static const unsigned int GL_RGBA_C = 0x1908;
static const unsigned int GL_UNSIGNED_BYTE_C = 0x1401;

struct App
{
private:
    uint32_t fps = 30;
    uint32_t width = 600;
    uint32_t height = 600;
    uint8_t r, g, b;
    bool background = false;

    void helpMsg()
    {
        cout << "Usage: \n   lottie2gif-gl [Lottie file] or [Lottie folder] [-r resolution] [-f fps] [-b background color]\n\nExamples: \n    $ lottie2gif-gl input.json\n    $ lottie2gif-gl input.json -r 600x600\n    $ lottie2gif-gl input.json -f 30\n    $ lottie2gif-gl input.json -r 600x600 -f 30 -b fa7410\n    $ lottie2gif-gl input.lot\n    $ lottie2gif-gl lottiefolder\n\n";
    }

    bool validate(string& name)
    {
        auto len = name.size();
        if ((len > 5 && name.substr(len - 5) == ".json") ||
            (len > 4 && name.substr(len - 4) == ".lot")) {
            return true;
        }
        cout << "Error: \"" << name << "\" is invalid." << endl;
        return false;
    }

    bool convert(string& in, string& out)
    {
        // 1. Init GLFW
        if (!glfwInit()) {
            cout << "Error: Failed to initialize GLFW." << endl;
            return false;
        }

        glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
#ifdef __APPLE__
        glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);
#endif
        glfwWindowHint(GLFW_FOCUSED, GLFW_FALSE);
        glfwWindowHint(GLFW_FOCUS_ON_SHOW, GLFW_FALSE);

        auto window = glfwCreateWindow((int)width, (int)height, "lottie2gif-gl", nullptr, nullptr);
        if (!window) {
            cout << "Error: Failed to create GLFW window." << endl;
            glfwTerminate();
            return false;
        }

        glfwMakeContextCurrent(window);
        glfwSwapInterval(0);

        // Load glReadPixels via GLFW
        auto glReadPixels = (PFNGLREADPIXELSPROC_LOCAL)glfwGetProcAddress("glReadPixels");
        if (!glReadPixels) {
            cout << "Error: glReadPixels not available." << endl;
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        // 2. Init ThorVG
        if (Initializer::init() != Result::Success) {
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        auto canvas = GlCanvas::gen();
        if (!canvas) {
            cout << "Error: Failed to create GL canvas." << endl;
            Initializer::term();
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        if (canvas->target(nullptr, nullptr, (void*)window, 0, width, height, ColorSpace::ABGR8888S) != Result::Success) {
            cout << "Error: Failed to set GL canvas target." << endl;
            delete canvas;
            Initializer::term();
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        // 3. Load animation
        auto animation = Animation::gen();
        auto picture = animation->picture();
        if (picture->load(in.c_str()) != Result::Success) {
            cout << "Error: Failed to load \"" << in << "\"." << endl;
            delete canvas;
            Initializer::term();
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        float origW, origH;
        picture->size(&origW, &origH);
        float scale = static_cast<float>(width) / origW;
        picture->size(origW * scale, origH * scale);

        // Use picture bounds for GIF dimensions (matches GifSaver)
        float bx, by, bw, bh;
        picture->bounds(&bx, &by, &bw, &bh);
        if (bx < 0) bw += bx;
        if (by < 0) bh += by;
        auto w = static_cast<uint32_t>(bw);
        auto h = static_cast<uint32_t>(bh);

        // Resize window if needed
        if (w != width || h != height) {
            glfwSetWindowSize(window, (int)w, (int)h);
            canvas->target(nullptr, nullptr, (void*)window, 0, w, h, ColorSpace::ABGR8888S);
        }

        // Background
        if (background) {
            auto bg = Shape::gen();
            bg->fill(r, g, b);
            bg->appendRect(0, 0, bw, bh);
            canvas->add(bg);
        }

        canvas->add(animation->picture());

        // 4. GIF encoding — matches GifSaver timing exactly
        auto totalFrames = animation->totalFrame();
        auto duration = animation->duration();

        auto actualFps = static_cast<float>(fps);
        if (actualFps > 60.0f) actualFps = 60.0f;
        else if (actualFps <= 0.0f) actualFps = totalFrames / duration;

        auto delay = 1.0f / actualFps;
        auto transparent = !background;

        auto buffer = new uint32_t[w * h];
        auto tmpRow = new uint32_t[w];

        GifWriter writer = {};
        if (!gifBegin(&writer, out.c_str(), w, h, static_cast<uint32_t>(delay * 100.0f))) {
            cout << "Error: Failed to begin GIF encoding." << endl;
            delete[] buffer;
            delete[] tmpRow;
            delete canvas;
            Initializer::term();
            glfwDestroyWindow(window);
            glfwTerminate();
            return false;
        }

        // 5. Frame loop
        for (auto p = 0.0f; p < duration; p += delay) {
            auto frameNo = totalFrames * (p / duration);
            animation->frame(frameNo);
            canvas->update();
            if (canvas->draw() == Result::Success) {
                canvas->sync();
            }

            // Read pixels from GL framebuffer
            glReadPixels(0, 0, (int)w, (int)h, GL_RGBA_C, GL_UNSIGNED_BYTE_C, buffer);

            // Flip vertically (GL origin at bottom-left)
            int rowWidth = (int)w;
            for (int y = 0; y < (int)h / 2; y++) {
                int topOff = y * rowWidth;
                int botOff = ((int)h - 1 - y) * rowWidth;
                memcpy(tmpRow, buffer + topOff, rowWidth * sizeof(uint32_t));
                memcpy(buffer + topOff, buffer + botOff, rowWidth * sizeof(uint32_t));
                memcpy(buffer + botOff, tmpRow, rowWidth * sizeof(uint32_t));
            }

            if (!gifWriteFrame(&writer, reinterpret_cast<const uint8_t*>(buffer), w, h, static_cast<uint32_t>(delay * 100.0f), transparent)) {
                cout << "Error: Failed to encode GIF frame." << endl;
                break;
            }

            glfwSwapBuffers(window);
            glfwPollEvents();
        }

        gifEnd(&writer);

        // 6. Cleanup
        delete[] buffer;
        delete[] tmpRow;
        delete canvas;
        Initializer::term();
        glfwDestroyWindow(window);
        glfwTerminate();

        return true;
    }

    void convert(string& lottieName)
    {
        auto gifName = lottieName;
        auto dot = gifName.rfind('.');
        if (dot != string::npos) gifName = gifName.substr(0, dot);
        gifName += ".gif";

        if (convert(lottieName, gifName)) {
            cout << "Generated Gif file : " << gifName << endl;
        } else {
            cout << "Failed Converting Gif file : " << lottieName << endl;
        }
    }

public:
    int setup(int argc, char** argv)
    {
        vector<const char*> inputs;

        for (int i = 1; i < argc; ++i) {
            const char* p = argv[i];
            if (*p == '-') {
                const char* p_arg = (i + 1 < argc) ? argv[++i] : nullptr;

                if (p[1] == 'r') {
                    if (!p_arg) {
                        cout << "Error: Missing resolution. Expected eg. -r 600x600." << endl;
                        return 1;
                    }
                    const char* x = strchr(p_arg, 'x');
                    if (x) {
                        width = atoi(p_arg);
                        height = atoi(x + 1);
                    }
                    if (!x || width <= 0 || height <= 0) {
                        cout << "Error: Resolution (" << p_arg << ") is corrupted." << endl;
                        return 1;
                    }
                } else if (p[1] == 'f') {
                    if (!p_arg) {
                        cout << "Error: Missing fps value." << endl;
                        return 1;
                    }
                    fps = atoi(p_arg);
                } else if (p[1] == 'b') {
                    if (!p_arg) {
                        cout << "Error: Missing background color." << endl;
                        return 1;
                    }
                    auto bgColor = (uint32_t)strtol(p_arg, NULL, 16);
                    r = (uint8_t)((bgColor & 0xff0000) >> 16);
                    g = (uint8_t)((bgColor & 0x00ff00) >> 8);
                    b = (uint8_t)((bgColor & 0x0000ff));
                    background = true;
                } else {
                    cout << "Warning: Unknown flag (" << p << ")." << endl;
                }
            } else {
                inputs.push_back(argv[i]);
            }
        }

        if (inputs.empty()) {
            helpMsg();
            return 0;
        }

        for (auto input : inputs) {
            string lottieName(input);
            if (!validate(lottieName)) continue;
            convert(lottieName);
        }

        return 0;
    }
};

int main(int argc, char** argv)
{
    App app;
    return app.setup(argc, argv);
}
