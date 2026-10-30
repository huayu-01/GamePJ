shader_type sky;
uniform vec4 zenith_color : source_color = vec4(0.26, 0.64, 0.86, 1.0);
uniform vec4 horizon_color : source_color = vec4(0.79, 0.90, 0.90, 1.0);
uniform vec4 cloud_color : source_color = vec4(1.0, 0.98, 0.90, 1.0);
uniform sampler2D cloud_noise : filter_linear_mipmap, repeat_enable;
void sky() {
	float height = max(EYEDIR.y, 0.0);
	vec3 base = mix(horizon_color.rgb, zenith_color.rgb, pow(height, 0.45));
	// 使用连续噪声纹理，保持不同图形后端下的云层轮廓一致。
	vec2 p = EYEDIR.xz / (max(height,0.0) + 0.35) * 0.55 + vec2(0.43,0.65);
	float clouds = texture(cloud_noise,p).r;
	float mask = smoothstep(0.52, 0.73, clouds) * smoothstep(0.02, 0.15, height);
	vec3 shade = mix(cloud_color.rgb * vec3(0.86,0.91,0.96), cloud_color.rgb, smoothstep(0.56,0.76,clouds));
	COLOR = mix(base, shade, mask * 0.95);
}
