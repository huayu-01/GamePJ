shader_type spatial;
render_mode diffuse_toon, specular_disabled;

uniform vec4 stone_color : source_color = vec4(0.29, 0.36, 0.30, 1.0);
uniform vec4 seam_color : source_color = vec4(0.20, 0.27, 0.23, 1.0);
uniform vec4 path_color : source_color = vec4(0.43, 0.46, 0.35, 1.0);
uniform float tile_size = 3.0;
uniform float seam_width = 0.018;
uniform float path_width = 2.3;
uniform vec4 grass_color : source_color = vec4(0.20, 0.35, 0.22, 1.0);
uniform float courtyard_radius = 22.0;
varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_position.xz / tile_size;
	p.x += mod(floor(p.y), 2.0) * 0.5;
	vec2 cell = floor(p);
	vec2 edge = min(fract(p), 1.0 - fract(p));
	float seam = 1.0 - smoothstep(seam_width, seam_width + 0.012, min(edge.x, edge.y));
	float variation = fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
	float path = 1.0 - smoothstep(path_width, path_width + 0.18, min(abs(world_position.x), abs(world_position.z)));
	vec3 surface = mix(stone_color.rgb, path_color.rgb, path * 0.65) * (0.94 + variation * 0.12);
	float patch = sin(world_position.x * 0.23) * cos(world_position.z * 0.19);
	float grass = smoothstep(courtyard_radius - 2.0, courtyard_radius + 2.0, length(world_position.xz) + patch * 2.5);
	vec3 paving = mix(surface, seam_color.rgb, seam * 0.30);
	ALBEDO = mix(paving, grass_color.rgb * (0.96 + variation * 0.08), grass * (1.0 - path * 0.70));
	ROUGHNESS = 0.94;
}
