shader_type canvas_item;
render_mode unshaded;

uniform sampler2D scene_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform vec2 panel_size = vec2(400.0, 400.0);
uniform bool light_surface = false;
uniform float corner_radius = 12.0;
uniform float edge_width = 18.0;
uniform float emphasis = 0.0;

void fragment() {
	vec2 p = UV * panel_size;
	vec2 q = abs(p - panel_size * 0.5) - (panel_size * 0.5 - vec2(corner_radius));
	float sd = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - corner_radius;
	float coverage = 1.0 - smoothstep(-0.8, 0.8, sd);
	float edge = 1.0 - smoothstep(1.0, edge_width, -sd);
	vec3 sharp = textureLod(scene_texture, SCREEN_UV, 0.0).rgb;
	vec3 blurred = textureLod(scene_texture, SCREEN_UV, light_surface ? 3.5 : 4.0).rgb;
	float blur_mix = light_surface ? 0.9 : mix(0.04, 0.92, edge);
	vec3 scene = mix(sharp, blurred, blur_mix);
	vec3 tint = light_surface ? vec3(0.87, 0.92, 0.94) : vec3(0.13, 0.17, 0.20);
	float opacity = light_surface ? 0.80 : 0.53;
	vec3 glass = mix(scene, tint, opacity);
	glass = mix(glass, vec3(0.84, 0.91, 0.95), edge * (light_surface ? 0.13 : 0.24));
	float border = 1.0 - smoothstep(0.8, 1.7, -sd);
	vec3 border_color = mix(vec3(0.78, 0.87, 0.91), vec3(0.20, 0.78, 0.84), emphasis);
	glass = mix(glass, border_color, border * mix(0.38, 0.75, emphasis));
	COLOR = vec4(glass, coverage) * COLOR;
}
