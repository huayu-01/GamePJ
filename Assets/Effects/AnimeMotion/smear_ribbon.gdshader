shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, fog_disabled;

uniform vec3 path_points[8];
uniform vec3 camera_local = vec3(0.0, 3.0, 10.0);
uniform vec4 primary_color : source_color = vec4(0.58, 0.63, 0.73, 1.0);
uniform vec4 shade_color : source_color = vec4(0.23, 0.29, 0.40, 1.0);
uniform vec4 ink_color : source_color = vec4(0.08, 0.10, 0.16, 1.0);
uniform float ribbon_width = 0.4;
uniform float ribbon_height = 0.6;
uniform float visibility = 0.0;
uniform float tear_opacity = 0.32;
uniform float seed = 0.0;
uniform sampler2D actor_texture : filter_linear, repeat_disable;
uniform bool protect_silhouette = false;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	int index = int(round(UV.x * 7.0));
	vec3 p = path_points[index];
	vec3 tangent = path_points[max(index - 1, 0)] - path_points[min(index + 1, 7)];
	if (length(tangent) < 0.0001) { tangent = vec3(1.0, 0.0, 0.0); }
	tangent = normalize(tangent);
	vec3 side = cross(tangent, camera_local - p);
	if (length(side) < 0.0001) { side = cross(tangent, vec3(0.0, 1.0, 0.01)); }
	side = normalize(side);
	vec3 up = normalize(cross(side, tangent));
	float taper = pow(max(0.0, 1.0 - UV.x), 0.65);
	float edge = VERTEX.y;
	vec3 across = VERTEX.z < 0.5 ? side * ribbon_width : up * ribbon_height;
	VERTEX = p + across * edge * taper * visibility;
}

void fragment() {
	if (protect_silhouette) {
		float solid = texture(actor_texture, SCREEN_UV).a;
		if (solid > 0.55) { discard; }
	}
	// 每片带仅保留两道稀疏软线，留出主体与模糊尾部的空间。
	float first = 1.0 - smoothstep(0.012, 0.045, abs(UV.y - 0.22 - UV.x * 0.06));
	float second = 1.0 - smoothstep(0.008, 0.032, abs(UV.y - 0.80 + UV.x * 0.035));
	float fade = smoothstep(0.03, 0.15, UV.x) * (1.0 - smoothstep(0.35, 0.88, UV.x));
	ALBEDO = mix(shade_color.rgb, primary_color.rgb, 0.65);
	ALPHA = max(first, second * 0.65) * fade * visibility * tear_opacity;
	if (ALPHA < 0.008) { discard; }
}
