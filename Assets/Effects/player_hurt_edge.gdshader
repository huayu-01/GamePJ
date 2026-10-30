shader_type canvas_item;
render_mode unshaded;

uniform vec4 edge_color : source_color = vec4(0.82, 0.12, 0.09, 0.2);
uniform float pulse : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	// 只在屏幕边缘显示，中心保持清晰，不改变准星与场景颜色。
	vec2 edge = abs(UV - vec2(0.5)) * 2.0;
	float vignette = pow(max(edge.x, edge.y), 4.0);
	COLOR = vec4(edge_color.rgb, edge_color.a * pulse * vignette);
}
