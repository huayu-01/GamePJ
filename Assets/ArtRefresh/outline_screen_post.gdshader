// 屏幕空间描边（遮罩法）—— 目前**没有接进任何场景**，只把这个 shader 留着备用。
//
// ── 怎么用（三步，缺一不可）──────────────────────────────────────────────
// ① 另起一个 SubViewport（transparent_bg = true, render_target_update_mode = ALWAYS），
//    里面放一个 Camera3D，cull_mask 只认"描边层"（例如第 2 层 = 2）。
// ② 要描边的单位**额外复制一份**白色剪影（StandardMaterial3D：unshaded + albedo 纯白 +
//    cull_disabled + 不投影），把副本里每个 MeshInstance3D 的 layers 设成第 2 层。
//    主相机 cull_mask 不要包含第 2 层，否则白模会出现在正片里。
// ③ 在主画面的一个 CanvasLayer（HUD 之下）放全屏 ColorRect 挂本 shader，
//    把上面 SubViewport 的 texture 绑到 mask_tex。
//
// ── 为什么是"遮罩法"而不是别的（2026-10-18 实测结论）────────────────────
// · Godot 4.7 的 canvas_item shader **不支持** hint_depth_texture（编译直接报错：
//   "'hint_depth_texture' is not supported in 'canvas_item' shaders"），
//   所以"一次 pass 读深度做全屏描边"这条路在 2D 叠层上走不通；
//   走 spatial 全屏 quad 又会把材质推进透明管线，本项目粒子多，排序会出问题。
// · 反向壳（VERTEX += NORMAL * 固定宽度）近景好看、自带部件内线，但线宽是世界空间固定值：
//   14m 外基本消失；给它加"按距离折算像素"的补偿后，远处外扩的壳会从模型表面穿出来把模型啃掉。
// · 所以：**遮罩法**是唯一既能"线宽恒定为 N 像素"、又不动透明管线的做法。
//   代价 = 被描边的单位要多画一遍（只有剪影，材质是最省的 unshaded）。
//
// ── 参数 ────────────────────────────────────────────────────────────────
// thickness_px   线宽（渲染目标像素，不随距离变化）
// outline_color  线色（偏深蓝黑比纯黑柔和）
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform sampler2D mask_tex : filter_nearest;
uniform float thickness_px : hint_range(1.0, 6.0, 1.0) = 2.0;
uniform vec4 outline_color : source_color = vec4(0.04, 0.05, 0.09, 1.0);

float mask_at(vec2 uv) {
	return texture(mask_tex, uv).a;
}

void fragment() {
	vec3 base = texture(screen_tex, SCREEN_UV).rgb;

	// 只在"剪影外缘"上色：单位内部 around ≈ center，不画；外侧才有差值。
	float center = mask_at(SCREEN_UV);
	float around = 0.0;
	for (int i = 0; i < 8; i++) {
		float a = float(i) * 0.7853981634; // 2π/8，8 方向
		vec2 dir = vec2(cos(a), sin(a));
		for (int s = 1; s <= 6; s++) {
			if (float(s) > thickness_px + 0.001) {
				break;
			}
			around = max(around, mask_at(SCREEN_UV + dir * SCREEN_PIXEL_SIZE * float(s)));
		}
	}

	float ring = clamp(around - center, 0.0, 1.0);
	COLOR = vec4(mix(base, outline_color.rgb, ring), 1.0);
}
