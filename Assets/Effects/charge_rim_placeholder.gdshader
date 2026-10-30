// 蓄力期间保留菲涅尔边缘；真实阶段到达时增加薄层身体闪光。
// 主体闪光透明度封顶0.12，轮廓合成封顶0.62，原模型贴图和明暗始终可见。
// 只用于叠加通道，不改模型材质、骨架或动画。
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never;

uniform vec4 rim_color : source_color = vec4(0.45, 0.86, 1.0, 1.0);
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 4.0) = 0.0;

uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float flash : hint_range(0.0, 2.0) = 0.0;

void fragment() {
	// 菲涅尔：视线越"擦过"表面（法线几乎垂直于视线）越亮 —— 就是"沿轮廓的一圈光"。
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, rim_power);

	// 身体薄层保证正面也能感知升档，轮廓仍承担主要亮度。
	float total = max(0.001, rim_strength + flash);
	ALBEDO = (rim_color.rgb * rim_strength + flash_color.rgb * flash) / total;
	EMISSION = ALBEDO * 0.85;
	float body_flash = min(flash, 1.0) * flash_color.a * 0.12;
	float edge_alpha = fresnel * clamp(rim_strength * rim_color.a * 0.45 + flash * flash_color.a * 0.45, 0.0, 0.52);
	ALPHA = clamp(body_flash + edge_alpha, 0.0, 0.62);
}
