#define SOKOL_IMPL
#define SOKOL_NOAPI
#include "sokol_app.h"

#include "Renderer/r_bgfx.h"

static void sokol_on_init(void)
{
    R_Init(sapp_width(), sapp_height());
}

// on frame change
static void sokol_on_frame(void)
{
    R_BeginFrame(sapp_width(), sapp_height());
    R_EndFrame();
}

static void sokol_on_event(const sapp_event* ev)
{
    if (ev->type == SAPP_EVENTTYPE_KEY_DOWN && ev->key_code == SAPP_KEYCODE_ESCAPE)
        sapp_quit();
    if (ev->type == SAPP_EVENTTYPE_RESIZED)
        R_Resize(ev->framebuffer_width, ev->framebuffer_height);
}

static void sokol_on_cleanup(void)
{
	R_Shutdown();
}

sapp_desc sokol_main(int argc, char** argv)
{
	(void)argc; (void)argv;
	return (sapp_desc)
	{
		.window_title = "Lord Goober Femboy window 👑",
		.width		  = 1280,
		.height       = 720,
		.init_cb      = sokol_on_init,
		.frame_cb     = sokol_on_frame,
		.event_cb     = sokol_on_event,
		.cleanup_cb   = sokol_on_cleanup,
	};
}