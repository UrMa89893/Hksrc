#define SOKOL_IMPL
// Selectes the openGL backend 
// using SOKOL_GLCORE for desktop (windows, mac, linux)
// using SOKOL_GLESS is for web development and other similar web based things

#define SOKOL_NOAPI
#include "sokol_app.h"

#define SOKOL_GLCORE
#include "sokol_glx.h"
#include "sokol_glue.h"

// Struct to hold the same reusable frame-clear behavour 
static sg_pass_action pass_action;

void init(void) {
	// initialize sokol_glx using configuration provided from the window enviroment
	sg_setup(&(sg_desc){
		.enviroment = sglue_enviroment()
	});
	
	// configuration of the background clear color 
	pass_action = (sg_pass_action) {
		.colors[0] = {
			.load_action = SG_LOADACTION_CLEAR,
			.clear_value = { 0.2f, 0.4f, 0.6f, 1.0f}
		}
	}; // (͡° ͜ʖ ͡°)
}

// calling all frames form main loop (i think maybe not idk)
// Found out Yes! it is calling from the main loop perframe thingy :)
void frame(void) {
	// get current window frame buffer
	int width = sapp_width();
	int height = sapp_height();
	
	// begin rendering into the window's frame buffer using clear color 
	sg_begin_pass(&(sg_pass){
		.action = pass_action,			// pass your pass_action struct here 
		.swapchain = sglue_swapchain()	// directs Sokol(Library) render to activate the window
	});
	
	// the Draw code will go here :)
	// finish the current resource here when the window has been closed
	sg_end_pass();
	sg_commit();
}

void cleanup(void) {
	sg_shutdown();
}

sapp_decs sokol_main(int argc, char* argv[]) {
	(void)argc; (void)argv;
	return (sapp_decs) {
		.init_cb = init,
		.frame_cb = frame, 
		.cleanup_cb = cleanup,
		.width = 800,
		.height = 400,
		.window_title = "", // window name
		.icon = { .sokol_default = true } // The window icon 
	}
}
