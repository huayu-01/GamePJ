shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, fog_disabled;

uniform sampler2D actor_texture : filter_linear, repeat_disable;
uniform vec2 motion_uv;
uniform vec2 center_uv;
uniform vec2 radius_uv;
uniform float strength = 0.0;
uniform float contour_opacity = 0.85;

float region(vec2 p) {
	float d = length((p - center_uv) / max(radius_uv, vec2(0.0001)));
	return 1.0 - smoothstep(0.65, 1.0, d);
}

void fragment() {
	vec2 p = SCREEN_UV;
	// 完整剪影保护内部，抗锯齿边缘渐变衔接拖尾，不向外扩张形成空隙。
	float solid = texture(actor_texture, p).a;
	float outside = 1.0 - smoothstep(0.2, 0.95, solid);
	if (outside < 0.008) { discard; }
	vec3 actor_sum = vec3(0.0);
	float alpha_sum = 0.0;
	float weights = 0.0;
	float jitter = fract(sin(dot(FRAGCOORD.xy, vec2(12.9898, 78.233))) * 43758.5453);
	// 仅在剪影外做单向曝光；挂点遮罩只约束取色，不切开内部保护区域。
	for (int i = 0; i < 13; i++) {
		float t = (float(i) + jitter) / 13.0;
		float w = 1.0 - t * 0.8;
		vec2 q = p + motion_uv * t;
		vec4 actor = texture(actor_texture, q);
		float a = actor.a * region(q);
		actor_sum += actor.rgb * a * w;
		alpha_sum += a * w;
		weights += w;
	}
	float opacity = alpha_sum / weights * contour_opacity * strength * outside;
	if (opacity < 0.008) { discard; }
	ALBEDO = actor_sum / max(alpha_sum, 0.0001);
	ALPHA = opacity;
}
