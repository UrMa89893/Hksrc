#define SOKOL_IMPL
#define SOKOL_GLCORE
#include "sokol_app.h"
#include "sokol_log.h"
#include "../Renderer/hsm_renderer.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <chrono>

static const char* g_hsm = nullptr;
static std::chrono::steady_clock::time_point g_last;

static void init(void) {
    const char* path = g_hsm ? g_hsm : "Main.hsm";
    if (!renderer_init(path)) {
        std::fprintf(stderr, "Hacksaw renderer failed to initialize for %s\\n", path);
        // Keep the window alive so initialization errors can be read from the
        // console instead of making the application appear to instantly quit.
        return;
    }
    sapp_lock_mouse(true);
    g_last = std::chrono::steady_clock::now();
}

static void frame(void) {
    auto now = std::chrono::steady_clock::now();
    float dt = std::chrono::duration<float>(now - g_last).count();
    g_last = now;
    if (dt > 0.1f) dt = 0.1f;
    renderer_frame(dt);
}

static void event(const sapp_event* ev) { renderer_event(ev); }
static void cleanup(void) { renderer_shutdown(); }

sapp_desc sokol_main(int argc, char* argv[]) {
    if (argc > 1) g_hsm = argv[1];
    return (sapp_desc){
        .init_cb = init,
        .frame_cb = frame,
        .cleanup_cb = cleanup,
        .event_cb = event,
        .width = 1280,
        .height = 720,
        .sample_count = 1,
        .swap_interval = 1,
        .window_title = "Hacksaw - HSM Renderer",
        .enable_clipboard = true,
        .icon = { .sokol_default = true },
        .logger = { .func = slog_func },
    };
}
