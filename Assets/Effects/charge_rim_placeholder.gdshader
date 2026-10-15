// 蓄力时的角色身体表现（P7 ④ **占位版** · 美术后期替换）
//
// ⚠️⚠️ **这是占位**：形态（菲涅尔次方 / 强度 / 是不是还要一层内描边）**不追求好看**，
// 只要求"蓄力时身体上有一圈看得见的边缘光、蓄力完成时整体闪一下"。
// 正式版由美术出（替换这个文件即可，**C# 一个字都不用改** —— 路径是 ChargeBodyFx.RimShaderPath）。
//
// 怎么被用：`Scripts/Combat/ChargeBodyFx.cs` 在运行期把它包成 ShaderMaterial，
// 挂到角色模型每个 MeshInstance3D 的 **MaterialOverlay** 上（叠加通道，**不改模型资源本身**、
// 不动 MaterialOverride，所以与受击闪色 / 精英染色 / 残影互不干扰）。
//
// 两个可调量的语义（都由 C# 侧按只读战斗状态驱动）：
//   rim_strength ：0 = 完全看不见；蓄力期间由 ChargeBodyFx 拉到 RimStrength。
//   flash        ：0 = 不闪；"基础蓄力完成"那一瞬间置 1 再线性衰减到 0（角色闪一下）。
shader_type spatial;
render_mode unshaded, blend_add, cull_back, depth_draw_never;

uniform vec4 rim_color : source_color = vec4(0.45, 0.86, 1.0, 1.0);
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 4.0) = 0.0;

uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float flash : hint_range(0.0, 2.0) = 0.0;

void fragment() {
	// 菲涅尔：视线越"擦过"表面（法线几乎垂直于视线）越亮 —— 就是"沿轮廓的一圈光"。
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, rim_power);

	vec3 rim = rim_color.rgb * fresnel * rim_strength;
	vec3 burst = flash_color.rgb * flash * flash_color.a;

	ALBEDO = rim + burst;
	ALPHA = clamp((fresnel * rim_strength * rim_color.a) + (flash * flash_color.a), 0.0, 1.0);
}
