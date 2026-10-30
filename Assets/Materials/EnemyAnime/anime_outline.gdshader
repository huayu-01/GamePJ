shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled;

uniform vec4 ink_color : source_color = vec4(0.10, 0.07, 0.15, 1.0);
uniform float ink_width : hint_range(0.0, 0.03) = 0.007;

void vertex() {
	VERTEX += NORMAL * ink_width;
}

void fragment() {
	ALBEDO = ink_color.rgb;
}
