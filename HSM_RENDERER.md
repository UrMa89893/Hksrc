# Hacksaw HSM renderer

This renderer is intentionally renderer-only. It does not add gameplay, physics,
collision, entities, networking, or a player controller.

## What was changed

- Replaced the old bgfx/Effekseer renderer entry point with a Sokol App based
  desktop OpenGL renderer.
- Added `hksrc/src/Renderer/hsm_renderer.cpp` and `.h`.
- Loads HSM v4 JSON directly and renders every `brush` side in `nodes`.
- Recursively handles `group`/`entity` children and supports legacy v1 `meshes`.
- Triangulates polygon faces, calculates normals, and applies HSM material UV
  axes/shift/scale.
- Loads PNG materials through the existing stb_image dependency.
- Starts a free-fly editor-style camera over the complete map bounds.
- WASD = move, Q/E = down/up, Shift = fast movement, mouse = look.
- Escape releases mouse lock; F1 toggles mouse lock again.
- The map path is supplied as the first command-line argument; `Main.hsm` is the
  default.

## Build/run

```text
cmake -B build -G "MinGW Makefiles"
cmake --build build -j
Working\\hacksaw.exe Main.hsm
```

Material paths are resolved relative to the HSM first, then the working
folder and `Working/Assets` fallback locations.

## Important Sokol note

The supplied project snapshot contains `sokol_app.h`, but it does not contain
`sokol_gfx.h`/`sokol_glue.h`. Therefore this renderer uses Sokol App for the
window/context/input layer and renders through the OpenGL context it creates.
It deliberately removes the previous bgfx dependency. Once the project carries
its normal Sokol GFX headers, the draw backend can be moved to `sokol_gfx.h`
without changing the HSM scene representation or camera code.
