#include "Renderer/r_bgfx.h"

#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "bgfx/c99/bgfx.h"
#define SOKOL_NOAPI /* bgfx owns the GL context (must match Platform/main.c) */
#include "sokol_app.h" /* native window handles only */

#define HK_CLEAR_COLOR 0x303030ffu /* Q3 console grey, ABGR */

void R_Init(int width, int height)
{
	bgfx_init_t init;
	bgfx_init_ctor(&init);

	init.type = BGFX_RENDERER_TYPE_OPENGL; /* flip to D3D11/VULKAN here if ever wanted */
#if defined(_WIN32)
	/* ndt (native display) is *nix-only; on Windows bgfx needs just the HWND */
	init.swapChain.nwh = (void*)sapp_win32_get_hwnd();
#endif
	init.swapChain.width = (uint32_t)width;
	init.swapChain.height = (uint32_t)height;
	init.reset = BGFX_RESET_VSYNC;

	if (!bgfx_init(&init)) {
		fprintf(stderr, "[renderer] bgfx init failed (OpenGL backend)\n");
		exit(1);
	}
	printf("[renderer] bgfx up: %s\n", bgfx_get_renderer_name(init.type));

	bgfx_set_view_clear(0, BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH, HK_CLEAR_COLOR, 1.0f, 0);
}

void R_Shutdown(void)
{
	bgfx_shutdown();
}

void R_BeginFrame(int width, int height)
{
	bgfx_set_view_rect(0, 0, 0, (uint16_t)width, (uint16_t)height, 0.0f, 1.0f);
	bgfx_touch(0);
}

void R_EndFrame(void)
{
	bgfx_frame(0);
}

void R_Resize(int width, int height)
{
	bgfx_swap_chain_t sc;

	memset(&sc, 0, sizeof(sc));
	/* same native window, new back-buffer size; neutral fields keep current value */
	sc.nwh = (void*)sapp_win32_get_hwnd();
	sc.width = (uint32_t)width;
	sc.height = (uint32_t)height;
	sc.formatColor = BGFX_TEXTURE_FORMAT_COUNT;
	sc.formatDepthStencil = BGFX_TEXTURE_FORMAT_COUNT;
	bgfx_reset(BGFX_RESET_VSYNC, &sc);
}
