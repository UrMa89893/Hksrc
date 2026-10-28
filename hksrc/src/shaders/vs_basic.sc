$input a_position, a_color0
$output v_color0

#include <bgfx_shader.sh>

// u_modelViewProj is a builtin: bgfx fills it from
// bgfx_set_view_transform() * bgfx_set_transform() automatically.
void main()
{
	gl_Position = mul(u_modelViewProj, vec4(a_position, 1.0));
	v_color0 = a_color0;
}
