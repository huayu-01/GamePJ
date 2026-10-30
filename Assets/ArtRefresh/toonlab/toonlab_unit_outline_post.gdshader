shader_type canvas_item;
render_mode unshaded;

// 只给单位的屏幕空间描边：从"白色剪影"遮罩做外缘扩张，再压暗场景颜色。
// 与 leopeltola 的 depth/normal 全屏描边不同——这里只认 mask，所以**只描单位**，
// 薄片（斗笠/披风/伞）也不会翻黑，厚度按像素算（不随距离变化）。

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform sampler2D mask_tex : filter_nearest;
uniform float thickness_px : hint_range(1.0, 8.0, 1.0) = 2.0;
uniform vec4 outline_color : source_color = vec4(0.02, 0.02, 0.03, 1.0);
uniform float outline_alpha : hint_range(0.0, 1.0) = 1.0;

float mask_at(vec2 uv) {
	return texture(mask_tex, uv).a;
}

void fragment() {
	vec3 base = texture(screen_tex, SCREEN_UV).rgb;

	float center = mask_at(SCREEN_UV);
	float around = 0.0;
	for (int i = 0; i < 8; i++) {
		float a = float(i) * 0.7853981634; // 2π/8
		vec2 dir = vec2(cos(a), sin(a));
		for (int s = 1; s <= 8; s++) {
			if (float(s) > thickness_px + 0.001) {
				break;
			}
			vec2 uv = SCREEN_UV + dir * SCREEN_PIXEL_SIZE * float(s);
			around = max(around, mask_at(uv));
		}
	}

	// 只在"遮罩外缘"上色：单位内部 around≈center，不画
	float ring = clamp(around - center, 0.0, 1.0);
	vec3 outc = mix(base, outline_color.rgb, ring * outline_alpha);
	COLOR = vec4(outc, 1.0);
}
