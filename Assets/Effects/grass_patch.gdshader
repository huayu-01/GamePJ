shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 root_color : source_color = vec4(0.17, 0.29, 0.10, 1.0);
uniform vec4 tip_color : source_color = vec4(0.48, 0.64, 0.22, 1.0);
uniform float wind_time = 0.0;
uniform float wind_strength = 0.13;
uniform float wind_frequency = 0.45;
uniform float draw_distance = 80.0;
varying float blade_height;
varying float tint;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	blade_height = UV.y;
	tint = sin(world.x * 0.71 + world.z * 0.53) * 0.5 + 0.5;
	float time = wind_time * wind_frequency;
	float gust = sin(dot(world.xz, vec2(0.55, 0.38)) - time * 1.55);
	float ripple = sin(world.x * 2.1 + world.z * 1.3 - time * 2.6) * 0.18;
	float weight = blade_height * blade_height;
	float fade = draw_distance > 0.0 ? 1.0 - smoothstep(draw_distance - 14.0, draw_distance, distance(world.xz, CAMERA_POSITION_WORLD.xz)) : 1.0;
	// 每株只做顶点弯曲；不使用透明混合，不给每株挂脚本或碰撞。
	vec2 world_sway = vec2(0.85, 0.40) * (gust + ripple) * wind_strength;
	vec3 local_sway = inverse(mat3(MODEL_MATRIX)) * vec3(world_sway.x, 0.0, world_sway.y);
	VERTEX = (VERTEX + local_sway * weight) * fade;
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.75));
}

void fragment() {
	ALBEDO = mix(root_color.rgb, tip_color.rgb, smoothstep(0.0, 1.0, blade_height)) * mix(0.86, 1.10, tint);
	ROUGHNESS = 1.0;
	BACKLIGHT = ALBEDO * 0.12;
}
