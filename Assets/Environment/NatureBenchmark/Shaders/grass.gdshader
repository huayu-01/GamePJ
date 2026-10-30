shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 root_color : source_color = vec4(0.32, 0.49, 0.075, 1.0);
uniform vec4 tip_color : source_color = vec4(0.66, 0.81, 0.22, 1.0);
uniform float wind_strength : hint_range(0.0, 0.5) = 0.16;
varying float blade_height;
varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	blade_height = clamp(VERTEX.y / 1.3, 0.0, 1.0);
	float gust = sin(world_position.x * 0.62 + world_position.z * 0.47 - TIME * 1.8);
	float ripple = sin(world_position.x * 2.2 + TIME * 3.0) * 0.2;
	VERTEX.x += (gust + ripple) * blade_height * blade_height * wind_strength;
	VERTEX.z += gust * blade_height * blade_height * wind_strength * 0.45;
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.88));
}

void fragment() {
	float patches = sin(world_position.x * 0.29 + sin(world_position.z * 0.32)) * 0.5 + 0.5;
	vec3 tint = mix(vec3(0.90, 1.0, 0.80), vec3(1.12, 1.05, 0.85), patches);
	ALBEDO = mix(root_color.rgb, tip_color.rgb, smoothstep(0.0, 0.95, blade_height)) * tint;
	ROUGHNESS = 1.0;
	BACKLIGHT = ALBEDO * 0.2;
}

void light() {
	float diffuse = smoothstep(-0.25, 0.8, dot(NORMAL, LIGHT));
	DIFFUSE_LIGHT += mix(vec3(0.32, 0.42, 0.20), vec3(1.0), diffuse) * LIGHT_COLOR * ATTENUATION / PI;
}
