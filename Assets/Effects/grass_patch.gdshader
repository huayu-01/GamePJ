shader_type spatial;
render_mode cull_disabled, specular_disabled, alpha_to_coverage;
#include "res://Assets/Effects/meadow_palette.gdshaderinc"

uniform float wind_time = 0.0;
uniform sampler2D blade_texture : filter_linear_mipmap;
uniform float wind_strength = 0.065;
uniform float wind_frequency = 0.45;
uniform float draw_distance = 80.0;
varying float blade_height;
varying vec2 root_world;
varying vec3 ground_color;
varying float face_visibility;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	blade_height = UV.y;
	root_world = (MODEL_MATRIX * vec4(UV2.x,0.0,UV2.y,1.0)).xz;
	ground_color = meadow_ground_color(root_world);
	vec3 original_normal=normalize(MODEL_NORMAL_MATRIX*NORMAL);
	vec3 eye=PROJECTION_MATRIX[3][3]>.5 ? INV_VIEW_MATRIX[2].xyz : normalize(CAMERA_POSITION_WORLD-MODEL_MATRIX[3].xyz);
	// 使用原始面法线判断侧向角度；受光法线另行朝上，避免两种用途互相干扰。
	face_visibility=smoothstep(.10,.38,abs(dot(original_normal,eye)));
	float time = wind_time * wind_frequency;
	float gust = sin(dot(world.xz, vec2(0.55, 0.38)) - time * 1.55);
	float ripple = sin(world.x * 2.1 + world.z * 1.3 - time * 2.6) * 0.18;
	float weight = blade_height * blade_height;
	float fade = draw_distance > 0.0 ? 1.0 - smoothstep(draw_distance - 14.0, draw_distance, distance(world.xz, CAMERA_POSITION_WORLD.xz)) : 1.0;
	// 草片只做顶点风动，不给每株挂脚本或碰撞。
	vec2 world_sway = vec2(0.85, 0.40) * (gust + ripple) * wind_strength;
	vec3 local_sway = inverse(mat3(MODEL_MATRIX)) * vec3(world_sway.x, 0.0, world_sway.y);
	VERTEX = (VERTEX + local_sway * weight) * fade;
	NORMAL = vec3(0.0, 1.0, 0.0);
}

void fragment() {
	float tip=smoothstep(.18,1.0,blade_height);
	// 片元阶段统一正反面的世界朝上法线，消除背面突然发黑的纸片明暗。
	NORMAL=normalize((VIEW_MATRIX*vec4(0.0,1.0,0.0,0.0)).xyz);
	vec3 soft_tip=ground_color*1.42+vec3(.012,.018,.003);
	ALBEDO=mix(ground_color,soft_tip,tip);
	vec2 mask_uv=vec2(UV.x,1.0-UV.y);
	ALPHA=texture(blade_texture,mask_uv).a*face_visibility;
	ALPHA_SCISSOR_THRESHOLD=.35;
	ALPHA_ANTIALIASING_EDGE=.20;
	ALPHA_TEXTURE_COORDINATE=mask_uv*vec2(textureSize(blade_texture,0));
	ROUGHNESS=1.0; SPECULAR=0.0;
}
void light() {
	DIFFUSE_LIGHT+=meadow_diffuse(NORMAL,LIGHT)*ATTENUATION*LIGHT_COLOR/PI;
}
