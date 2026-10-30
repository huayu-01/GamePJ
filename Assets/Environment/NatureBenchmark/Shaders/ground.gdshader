shader_type spatial;
render_mode specular_disabled;

uniform vec4 grass_dark : source_color = vec4(0.36, 0.49, 0.12, 1.0);
uniform vec4 grass_light : source_color = vec4(0.52, 0.64, 0.19, 1.0);
uniform vec4 soil_color : source_color = vec4(0.70, 0.59, 0.39, 1.0);
varying vec3 world_position;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1,0)), f.x), mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
}
float cells(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); float first = 10.0; float second = 10.0;
	for (int y=-1; y<=1; y++) for (int x=-1; x<=1; x++) {
		vec2 offset = vec2(float(x), float(y));
		vec2 point = offset + vec2(hash(i+offset), hash(i+offset+vec2(41.0))) - f;
		float d = dot(point, point);
		if (d < first) { second = first; first = d; } else { second = min(second, d); }
	}
	return sqrt(second) - sqrt(first);
}
void vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
	vec2 p = world_position.xz;
	float large = noise(p * 0.28);
	float medium = noise(p * 1.3);
	float fine = noise(p * 18.0);
	float path_x = sin(p.y * 0.19) * 2.1 + p.y * 0.045;
	float width = 1.45 + sin(p.y * 0.5) * 0.2;
	float path = 1.0 - smoothstep(width - 0.25, width + 0.5, abs(p.x - path_x) + (medium - 0.5) * 0.5);
	float clearing = 1.0 - smoothstep(3.0, 4.5, length((p - vec2(0.0, 2.0)) * vec2(0.92, 1.0)) + (large - 0.5) * 0.7);
	float dirt = max(path, clearing * 0.88);
	vec3 grass = mix(grass_dark.rgb, grass_light.rgb, large) * (0.93 + medium * 0.12);
	vec2 warp = vec2(noise(p * 1.5),noise(p * 1.5 + vec2(14.0,8.0))) * 0.8;
	float crack = 1.0 - smoothstep(0.012, 0.04, cells(p * 2.2 + warp));
	vec3 soil = soil_color.rgb * (0.84 + large * 0.24 + medium * 0.06 + fine * 0.025);
	soil *= 1.0 - crack * 0.055;
	ALBEDO = mix(grass, soil, dirt);
	ROUGHNESS = 1.0;
}
