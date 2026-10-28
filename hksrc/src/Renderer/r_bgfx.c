#include "Renderer/r_bgfx.h"

#include <math.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "bgfx/c99/bgfx.h"
#define SOKOL_NOAPI /* bgfx owns the GL context (must match Platform/main.c) */
#include "sokol_app.h" /* native window handles */

#if defined(_WIN32)
#include <windows.h> /* GetModuleFileNameA for R_ExeDir */
#endif

#define HK_CLEAR_COLOR 0xFFC0CBffu 

#define HK_NEAR 0.1f
#define HK_FAR  100.0f
#define HK_FOV_DEG 60.0f

/* our interleaved vertex format: must match varying.def.sc's $input */
typedef struct {
	float    x, y, z;  /* BGFX_ATTRIB_POSITION */
	uint32_t abgr;     /* BGFX_ATTRIB_COLOR0, packed by R_Color() */
} hk_tri_vertex_t;

/* GPU objects created in R_Init, drawn every frame, freed in R_Shutdown.
 * In bgfx these are 16-bit index handles into bgfx's internal tables. */
static bgfx_program_handle_t        s_basicProgram = { UINT16_MAX };
static bgfx_vertex_buffer_handle_t  s_triVb        = { UINT16_MAX };
static bgfx_index_buffer_handle_t   s_triIb        = { UINT16_MAX };

/* ---- helpers ---- */

/* bgfx COLOR0 as UINT8-normalized is an ABGR dword in memory
 * (little-endian RGBA8): pack with this so r/g/b read naturally. */
static uint32_t R_Color(uint8_t r, uint8_t g, uint8_t b)
{
	return 0xff000000u | ((uint32_t)b << 16) | ((uint32_t)g << 8) | r;
}

/* read a whole file into a malloc'd buffer, NULL on failure.
 * caller owns the buffer; *outSize gets the byte count. */
static void* R_ReadFile(const char* path, size_t* outSize)
{
	FILE* f = fopen(path, "rb");
	void* data = NULL;
	long size;

	if (!f)
		return NULL;

	fseek(f, 0, SEEK_END);
	size = ftell(f);
	fseek(f, 0, SEEK_SET);

	if (size > 0) {
		data = malloc((size_t)size);
		if (data && fread(data, 1, (size_t)size, f) != (size_t)size) {
			free(data);
			data = NULL;
		}
	}
	fclose(f);

	if (data)
		*outSize = (size_t)size;
	return data;
}

/* folder containing the exe, trailing slash. static buffer = valid forever,
 * which is what single-threaded init-time callers want. */
static const char* R_ExeDir(void)
{
#if defined(_WIN32)
	static char s_dir[512] = { 0 };
	if (!s_dir[0]) {
		char exe[512];
		DWORD len = GetModuleFileNameA(NULL, exe, (DWORD)sizeof(exe));
		/* walk backwards past the filename, keeping the trailing slash */
		while (len > 0 && exe[len - 1] != '\\' && exe[len - 1] != '/')
			--len;
		memcpy(s_dir, exe, len);
		s_dir[len] = '\0';
	}
	return s_dir;
#else
	return ""; /* relative paths from CWD; non-Win32 not supported yet */
#endif
}

/* load a shaderc-compiled .bin (see Tools/build-shaders.ps1).
 * relPath is relative to the .exe folder, e.g. "shaders/vs_basic.bin". */
static bgfx_shader_handle_t R_LoadShader(const char* relPath)
{
	bgfx_shader_handle_t bad = { UINT16_MAX };
	bgfx_shader_handle_t handle;
	const bgfx_memory_t* mem;
	char path[512];
	size_t size = 0;
	void* data;

	/* shader dir lives next to the exe, so it works no matter where
	 * you launched the process from */
	snprintf(path, sizeof(path), "%s%s", R_ExeDir(), relPath);

	data = R_ReadFile(path, &size);
	if (!data) {
		fprintf(stderr, "[renderer] missing shader: %s\n", path);
		return bad;
	}

	/* bgfx_copy gives bgfx its own refcounted copy, so we free ours
	 * right after. (bgfx_make_ref would require keeping data alive.) */
	mem = bgfx_copy(data, (uint32_t)size);
	free(data);

	handle = bgfx_create_shader(mem);
	if (handle.idx == UINT16_MAX)
		fprintf(stderr, "[renderer] failed to create shader: %s\n", path);
	return handle;
}

/* ---- lifecycle ---- */

void R_Init(int width, int height)
{
	bgfx_init_t init;
	bgfx_vertex_layout_t layout;

	/* 1) bring up bgfx FIRST — every other bgfx call needs it alive */
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

	/* 2) interleaved vertex layout: must match varying.def.sc's $input.
	 *    BGFX_RENDERER_TYPE_NOOP = "generic", not tied to one backend.
	 *    layout_add's last param is bool asInt: true only for integer
	 *    attributes you want un-normalized, e.g. bone indices. */
	bgfx_vertex_layout_begin(&layout, BGFX_RENDERER_TYPE_NOOP);
	bgfx_vertex_layout_add(&layout, BGFX_ATTRIB_POSITION, 3, BGFX_ATTRIB_TYPE_FLOAT,
	                       false, false);
	bgfx_vertex_layout_add(&layout, BGFX_ATTRIB_COLOR0, 4, BGFX_ATTRIB_TYPE_UINT8,
	                       true, false);
	bgfx_vertex_layout_end(&layout);

	{	
		const hk_tri_vertex_t verts[] = {
			{  0.0f,  1.2f, 0.0f, R_Color(200,   0,   0) },
			{ -1.1f, -1.1f, 0.0f, R_Color(  0, 192,   0) },
			{  1.1f, -1.1f, 0.0f, R_Color(  0,   0, 203) },
		};
		const uint16_t indices[] = { 0, 1, 2 };
		const bgfx_memory_t* vbMem = bgfx_copy(verts, sizeof(verts));
		const bgfx_memory_t* ibMem = bgfx_copy(indices, sizeof(indices));

		s_triVb = bgfx_create_vertex_buffer(vbMem, &layout, BGFX_BUFFER_NONE);
		s_triIb = bgfx_create_index_buffer(ibMem, BGFX_BUFFER_NONE);
	}

	/* 4) load compiled shaders and link them into a draw program */
	s_basicProgram = bgfx_create_program(
		R_LoadShader("shaders/vs_basic.bin"),
		R_LoadShader("shaders/fs_basic.bin"),
		true /* destroy shader handles once linked into the program */);
}

void R_Shutdown(void)
{
	if (s_triVb.idx != UINT16_MAX)  bgfx_destroy_vertex_buffer(s_triVb);
	if (s_triIb.idx != UINT16_MAX)  bgfx_destroy_index_buffer(s_triIb);
	if (s_basicProgram.idx != UINT16_MAX) bgfx_destroy_program(s_basicProgram);
	bgfx_shutdown();
}

void R_BeginFrame(int width, int height)
{
	float view[16], proj[16], model[16];
	float aspect, cotHalfFov, c, s;
	/* clock() counts CPU seconds since launch: cheap, dependency-free time */
	const float t = (float)clock() / (float)CLOCKS_PER_SEC;

	/* 7 args: current bgfx master added min/max depth to set_view_rect */
	bgfx_set_view_rect(0, 0, 0, (uint16_t)width, (uint16_t)height, 0.0f, 1.0f);
	bgfx_touch(0); /* prime view 0 so the clear happens even with no draws */

	/* row-vector math throughout (v' = v * M), row-major, D3D-style
	 * clip space: y up, z in [0,1] — bgfx's convention on every backend. */

	/* view matrix: camera at origin, world pushed 3 units down -Z */
	memset(view, 0, sizeof(view));
	view[0] = view[5] = view[10] = view[15] = 1.0f;
	view[14] = -3.0f; /* translation z */

	/* projection matrix: perspective, right-handed, 60 deg vertical FOV.
	 * right-handed clip space: w = -z (positive for stuff in front of the
	 * camera, since visible z is negative); depth maps to [0,1] */
	aspect = (float)width / (float)height;
	cotHalfFov = 1.0f / tanf((HK_FOV_DEG * 0.5f) * 3.14159265f / 180.0f);
	memset(proj, 0, sizeof(proj));
	proj[0]  = cotHalfFov / aspect;
	proj[5]  = cotHalfFov;
	proj[10] = HK_FAR / (HK_NEAR - HK_FAR);
	proj[11] = -1.0f;                 /* clip.w = -world z (right-handed)  */
	proj[14] = -(HK_FAR * HK_NEAR) / (HK_FAR - HK_NEAR);
	proj[15] = 0.0f;

	bgfx_set_view_transform(0, view, proj);

	/* model matrix: spin around Y */
	c = cosf(t);
	s = sinf(t);
	memset(model, 0, sizeof(model));
	model[0] = c;   model[2]  = -s;
	model[5] = 1.0f;
	model[8] = s;   model[10] = c;
	model[15] = 1.0f;

	bgfx_set_transform(model, 1); /* (matrix, how many consecutive matrices) */

	bgfx_set_state(BGFX_STATE_WRITE_RGB | BGFX_STATE_WRITE_A |
	               BGFX_STATE_WRITE_Z | BGFX_STATE_DEPTH_TEST_LESS, 0);
	bgfx_set_vertex_buffer(0, s_triVb, 0, UINT32_MAX);
	bgfx_set_index_buffer(s_triIb, 0, UINT32_MAX);
	bgfx_submit(0, s_basicProgram, 0, 0);
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
