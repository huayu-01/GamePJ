shader_type spatial;
render_mode cull_disabled, specular_disabled, alpha_to_coverage;

uniform sampler2D leaf_texture : source_color, filter_linear_mipmap_anisotropic;
uniform bool has_texture = true;
uniform bool use_vertex_color = true;
uniform vec4 leaf_color : source_color = vec4(1.0);
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform vec3 canopy_center = vec3(0.0, 3.0, 0.0);
uniform vec3 canopy_radius = vec3(2.0);
uniform float normal_roundness : hint_range(0.0, 1.0) = .78;
uniform float side_fade : hint_range(0.0, 1.0) = .24;
uniform float alpha_cutoff : hint_range(.01, .95) = .20;
uniform float wrap_light : hint_range(0.0, 1.0) = .35;
varying vec3 soft_world_normal;
varying float face_visibility;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 face_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
	vec3 eye = PROJECTION_MATRIX[3][3] > .5 ? INV_VIEW_MATRIX[2].xyz : normalize(CAMERA_POSITION_WORLD - world);
	// 侧向只温和减弱；树冠需要持续覆盖，不能照搬草片的完全消失。
	face_visibility = mix(1.0 - side_fade, 1.0, smoothstep(.08, .40, abs(dot(face_normal, eye))));
	// 投影固定保留原叶轮廓，避免镜头转动使地面的树影跟着变稀。
	if (IN_SHADOW_PASS) { face_visibility = 1.0; }
	vec3 radial = (VERTEX - canopy_center) / max(canopy_radius, vec3(.01));
	radial.y = max(radial.y, -.15) + .38;
	vec3 crown_normal = normalize(MODEL_NORMAL_MATRIX * normalize(radial));
	vec3 upward_face = face_normal.y < 0.0 ? -face_normal : face_normal;
	soft_world_normal = normalize(mix(upward_face, crown_normal, normal_roundness));
	UV = UV * uv_scale.xy + uv_offset.xy;
}

void fragment() {
	vec4 texel = has_texture ? texture(leaf_texture, UV) : vec4(1.0);
	vec4 tint = use_vertex_color ? COLOR : vec4(1.0);
	ALBEDO = texel.rgb * leaf_color.rgb * tint.rgb;
	NORMAL = normalize((VIEW_MATRIX * vec4(soft_world_normal, 0.0)).xyz);
	ALPHA = texel.a * leaf_color.a * tint.a * face_visibility;
	ALPHA_SCISSOR_THRESHOLD = alpha_cutoff;
	ALPHA_ANTIALIASING_EDGE = alpha_cutoff * .65;
	ALPHA_TEXTURE_COORDINATE = UV * vec2(textureSize(leaf_texture, 0));
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float diffuse = clamp((dot(NORMAL, LIGHT) + wrap_light) / (1.0 + wrap_light), 0.0, 1.0);
	DIFFUSE_LIGHT += diffuse * ATTENUATION * LIGHT_COLOR / PI;
}
