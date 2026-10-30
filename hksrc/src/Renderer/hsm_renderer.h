#pragma once

// Hacksaw renderer: HSM scene loading + a free-flying map viewer.
// The platform/window/input layer is Sokol App; rendering happens in the
// graphics context created by Sokol. No gameplay, physics or entity simulation
// is part of this module.

bool renderer_init(const char* hsm_path);
void renderer_frame(float dt);
void renderer_event(const void* event);
void renderer_shutdown(void);
