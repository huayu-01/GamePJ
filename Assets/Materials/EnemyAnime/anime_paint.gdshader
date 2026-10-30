shader_type spatial;
render_mode specular_disabled, ambient_light_disabled;

// 用独立的亮部、阴影和提亮色构成动漫色块，避免底色被二次乘暗。
uniform vec4 paint_color : source_color = vec4(0.7, 0.5, 0.9, 1.0);
uniform vec4 shade_color : source_color = vec4(0.36, 0.23, 0.57, 1.0);
uniform vec4 light_color : source_color = vec4(0.86, 0.7, 1.0, 1.0);
uniform float brush_amount : hint_range(0.0, 0.15) = 0.035;
uniform float emissive_amount : hint_range(0.0, 2.0) = 0.0;
uniform float edge_light : hint_range(0.0, 0.3) = 0.055;
uniform bool mushroom_repaint = false;
uniform sampler2D region_texture : filter_linear, repeat_disable;
uniform vec4 stem_color : source_color = vec4(0.83, 0.9, 0.53, 1.0);
uniform vec4 stem_shade : source_color = vec4(0.48, 0.67, 0.46, 1.0);

varying vec3 paint_position;
varying vec3 color_lit;
varying vec3 color_shade;
varying vec3 color_high;
varying float brush_value;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float paint_noise(vec3 p) {
	vec3 cell = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(cell), hash(cell + vec3(1,0,0)), f.x),
		mix(hash(cell + vec3(0,1,0)), hash(cell + vec3(1,1,0)), f.x), f.y),
		mix(mix(hash(cell + vec3(0,0,1)), hash(cell + vec3(1,0,1)), f.x),
		mix(hash(cell + vec3(0,1,1)), hash(cell + vec3(1,1,1)), f.x), f.y), f.z);
}

void vertex() {
	paint_position = VERTEX;
}

void fragment() {
	color_lit = paint_color.rgb;
	color_shade = shade_color.rgb;
	color_high = light_color.rgb;
	float detail_mask = 1.0;
	if (mushroom_repaint) {
		// 固定 UV 区域来自未变形模型，缩地时不重新猜测菌盖和面部位置。
		vec3 regions = texture(region_texture, UV).rgb;
		bool cap = regions.r > 0.5;
		if (!cap) {
			color_lit = stem_color.rgb;
			color_shade = stem_shade.rgb;
			color_high = mix(stem_color.rgb, vec3(1.0, 0.98, 0.76), 0.28);
		}
		if (!cap) {
			float eye = smoothstep(0.25, 0.75, regions.g);
			color_lit = mix(color_lit, vec3(0.99, 0.96, 0.79), eye);
			color_shade = mix(color_shade, vec3(0.73, 0.72, 0.72), eye);
			color_high = mix(color_high, vec3(1.0, 0.98, 0.87), eye);
			detail_mask = 1.0 - eye;
		}
		if (!cap && regions.b > 0.5) {
			color_lit = vec3(0.034, 0.024, 0.055);
			color_shade = vec3(0.016, 0.013, 0.027);
			color_high = color_lit;
			detail_mask = 0.0;
		}
	}
	// 宽而稀疏的笔触明暗，幅度很小，不把模型变成带颗粒的旧化材质。
	brush_value = paint_noise(paint_position * vec3(7.0, 16.0, 7.0));
	float brush = (floor(brush_value * 4.0) / 3.0 - 0.5) * brush_amount * detail_mask;
	color_lit *= 1.0 + brush;
	color_shade *= 1.0 + brush * 0.6;
	ALBEDO = vec3(1.0);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	EMISSION = color_shade * 0.58 + color_lit * emissive_amount;
}

void light() {
	// 主色阶由主光方向决定，环境投影只轻压明度，避免叶影切出碎斑。
	float lit = dot(NORMAL, LIGHT) + 0.20;
	float edge = max(fwidth(lit) * 0.9, 0.004);
	float band = smoothstep(0.34 - edge, 0.34 + edge, lit + (brush_value - 0.5) * 0.028);
	float highlight = smoothstep(0.87 - edge, 0.87 + edge, lit);
	vec3 painted = mix(color_shade, color_lit, band);
	painted = mix(painted, color_high, highlight * 0.32);
	painted *= mix(0.97, 1.0, smoothstep(0.18, 0.65, ATTENUATION));
	float rim = smoothstep(0.76, 0.86, 1.0 - max(dot(NORMAL, VIEW), 0.0));
	painted += color_high * rim * edge_light * ATTENUATION;
	vec3 lamp = mix(vec3(1.0), clamp(LIGHT_COLOR / PI, vec3(0.0), vec3(1.3)), 0.18);
	// 多光源取最亮贡献，防止色块相加后变成白色。
	DIFFUSE_LIGHT = max(DIFFUSE_LIGHT, max(painted - color_shade * 0.58, vec3(0.0)) * lamp);
}
