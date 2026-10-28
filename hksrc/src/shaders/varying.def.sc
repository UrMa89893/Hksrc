// Declares the vertex attributes shaderc feeds into the vertex shader
// and the varyings passed from vertex shader to fragment shader.
// Modern bgfx format: plain declarations, varyings first (with defaults),
// then attributes. Which ones each stage actually uses is selected with
// $input/$output at the top of each .sc file.
// Keep in sync with the bgfx_vertex_layout_t in r_bgfx.c.
vec4 v_color0   : COLOR0   = vec4(0.0, 0.0, 0.0, 0.0);

vec3 a_position : POSITION;
vec4 a_color0   : COLOR0;
