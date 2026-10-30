shader_type spatial;
render_mode specular_disabled, ambient_light_disabled;

// 通用回退保留原纹理、顶点色与发光；已认可的模型优先复用森林样板的专用配色。
uniform vec4 base_color : source_color = vec4(1.0);
uniform sampler2D base_texture : source_color, filter_linear_mipmap_anisotropic;
uniform bool has_texture = false;
uniform bool vertex_color = false;
uniform vec3 glow_color : source_color = vec3(0.0);
varying vec3 painted;

void fragment() {
	painted = base_color.rgb;
	if (has_texture) painted *= texture(base_texture, UV).rgb;
	if (vertex_color) painted *= COLOR.rgb;
	ALBEDO = vec3(1.0);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	EMISSION = painted * 0.24 + glow_color;
}

void light() {
	float amount = dot(NORMAL, LIGHT);
	float edge = max(fwidth(amount), 0.008);
	float band = smoothstep(0.22 - edge, 0.22 + edge, amount);
	float highlight = smoothstep(0.83 - edge, 0.83 + edge, amount);
	vec3 shade = painted * vec3(0.58, 0.60, 0.68);
	vec3 lit = mix(shade, painted, band);
	lit = mix(lit, mix(painted, vec3(1.0), 0.15), highlight * 0.25);
	vec3 lamp = mix(vec3(1.0), clamp(LIGHT_COLOR / PI, vec3(0.0), vec3(1.3)), 0.18);
	DIFFUSE_LIGHT = max(DIFFUSE_LIGHT, lit * lamp * mix(0.65, 1.0, ATTENUATION));
}
