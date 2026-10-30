shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, fog_disabled;

// 轻微折射背景，淡蓝外壳与外扩波纹共用同一个网格，不增加灯光和粒子。
uniform sampler2D scene_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform vec4 tint : source_color = vec4(0.34, 0.85, 1.0, 1.0);
uniform float phase : hint_range(0.0, 1.0) = 0.0;
uniform float distortion : hint_range(0.0, 0.02) = 0.005;
varying vec3 local_position;

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 3.0);
	float ripple = sin(length(local_position.xz) * 28.0 - phase * 18.0);
	float fade = (1.0 - phase) * smoothstep(0.0, 0.08, phase);
	vec2 offset = NORMAL.xy * distortion * ripple * fade;
	vec3 background = textureLod(scene_texture, clamp(SCREEN_UV + offset, vec2(0.001), vec2(0.999)), 0.0).rgb;
	ALBEDO = mix(background, tint.rgb, 0.12 + rim * 0.32);
	EMISSION = tint.rgb * rim * fade * 0.24;
	ALPHA = clamp((0.10 + rim * 0.42 + max(ripple, 0.0) * 0.06) * fade, 0.0, 0.5);
}
