shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled;

// 按位置扩大外壳，让硬边法线拆分的低面数表面不出现描边裂缝。
uniform vec4 ink_color : source_color = vec4(0.075, 0.09, 0.13, 1.0);
uniform float ink_width = 0.004;

void vertex() {
	VERTEX += normalize(VERTEX - vec3(0.0, 1.5, 0.0)) * ink_width;
}

void fragment() {
	ALBEDO = ink_color.rgb;
}
