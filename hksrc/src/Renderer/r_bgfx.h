#ifndef R_BGFX_H
#define R_BGFX_H

/* Renderer bootstrap. All bgfx calls in the engine go through this layer,
 * so game code never touches the backend directly. */

void R_Init(int width, int height);
void R_Shutdown(void);
void R_BeginFrame(int width, int height);
void R_EndFrame(void);
void R_Resize(int width, int height);

#endif /* R_BGFX_H */
