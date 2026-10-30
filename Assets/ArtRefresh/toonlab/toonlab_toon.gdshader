shader_type spatial;

// 卡通分档着色（L1-a/b 预览用）
// 改写自 Flexible Toon Shader（MIT）by CaptainProton42
// https://github.com/CaptainProton42/FlexibleToonShaderGD
// 只保留预览需要的部分：分档 diffuse + 硬边高光 + rim；不采样粗糙度（用户裁定 roughness = 0）。

uniform vec4 albedo_color : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_texture = false;

uniform int cuts : hint_range(1, 8) = 3;
uniform float wrap : hint_range(-1.0, 1.0) = 0.15;
uniform float steepness : hint_range(1.0, 8.0) = 1.6;

uniform bool use_specular = true;
uniform float specular_strength : hint_range(0.0, 2.0) = 0.85;
uniform float specular_shininess : hint_range(1.0, 128.0) = 40.0;
uniform float specular_cutoff : hint_range(0.0, 1.0) = 0.5;

uniform bool use_rim = true;
uniform float rim_width : hint_range(0.5, 16.0) = 5.0;
uniform vec4 rim_color : source_color = vec4(1.0, 1.0, 1.0, 0.35);

void fragment() {
	vec3 base = albedo_color.rgb;
	if (use_texture) {
		base *= texture(albedo_texture, UV).rgb;
	}
	ALBEDO = base;
	// 用户裁定 roughness = 0：这里根本不参与光照，写 0 只为表意
	ROUGHNESS = 0.0;
	METALLIC = 0.0;
	// 高光完全由 light() 里的硬边 blob 负责，关掉内置高光
	SPECULAR = 0.0;
}

void light() {
	// Godot 4：light() 里的 ATTENUATION 是 float（Godot 3 才是 vec3）
	float atten = ATTENUATION;
	float ndotl = dot(NORMAL, LIGHT);
	// 阴影/衰减并入亮度：atten = 0 时 lit 直接掉到 wrap 以下 → 变暗档
	float lit = clamp(ndotl + (atten - 1.0) + wrap, 0.0, 1.0);

	// 分档：cuts 个硬台阶；steepness 收窄过渡
	float steps = float(cuts);
	float band = floor(lit * steps + 0.5) / steps;
	band = pow(band, steepness);

	DIFFUSE_LIGHT += LIGHT_COLOR / PI * ALBEDO * band;

	// 硬边高光：Blinn-Phong 阈值化 —— 这就是"roughness = 0"在 toon 里的落地形式
	if (use_specular) {
		vec3 half_vec = normalize(LIGHT + VIEW);
		float spec = pow(max(dot(NORMAL, half_vec), 0.0), specular_shininess);
		float kept = step(specular_cutoff, spec);
		SPECULAR_LIGHT += LIGHT_COLOR / PI * kept * specular_strength * atten;
	}

	// 边缘光
	if (use_rim) {
		float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), rim_width);
		DIFFUSE_LIGHT += LIGHT_COLOR / PI * rim_color.rgb * rim * rim_color.a * atten;
	}
}
