shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform sampler2D leaf_texture : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 leaf_color : source_color = vec4(0.62, 0.79, 0.22, 1.0);
uniform vec3 crown_center = vec3(0.0, 4.5, 0.0);
uniform float crown_radius = 3.0;
uniform float normal_blend : hint_range(0.0, 1.0) = 0.85;
uniform float wind_strength : hint_range(0.0, 0.5) = 0.12;
uniform float texture_color_weight : hint_range(0.0, 1.0) = 0.0;
varying float crown_height;
varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	crown_height = clamp((VERTEX.y - crown_center.y) / max(crown_radius, 0.1) * 0.5 + 0.5, 0.0, 1.0);
	// 用树冠整体法线减轻叶片之间生硬的碎阴影，保留模型轮廓。
	vec3 rounded_normal = normalize((VERTEX - crown_center) * vec3(1.0, 0.65, 1.0) + vec3(0.0, 1.4, 0.0));
	NORMAL = normalize(mix(NORMAL, rounded_normal, normal_blend));
	float wave = sin(world_position.x * 0.75 + world_position.z * 0.42 + TIME * 1.65);
	float flutter = sin(world_position.z * 2.6 + TIME * 3.4) * 0.22;
	float bend = smoothstep(0.0, 3.0, VERTEX.y);
	VERTEX.x += (wave + flutter) * wind_strength * bend;
	VERTEX.z += wave * wind_strength * 0.35 * bend;
}

void fragment() {
	vec4 sample_color = texture(leaf_texture, UV);
	if (sample_color.a < 0.25) discard;
	float patch = sin(world_position.x * 1.2 + world_position.z * 0.65) * 0.035;
	vec3 base = leaf_color.rgb * mix(vec3(0.66, 0.83, 0.55), vec3(1.12, 1.08, 0.88), crown_height);
	ALBEDO = base * (1.0 + patch) * mix(vec3(1.0), sample_color.rgb, texture_color_weight);
	ROUGHNESS = 1.0;
	BACKLIGHT = ALBEDO * 0.25;
}

void light() {
	float wrapped = dot(NORMAL, LIGHT) * 0.5 + 0.5;
	float light_band = smoothstep(0.25, 0.82, wrapped);
	DIFFUSE_LIGHT += mix(vec3(0.30, 0.40, 0.22), vec3(1.0), light_band) * LIGHT_COLOR * ATTENUATION / PI;
}
