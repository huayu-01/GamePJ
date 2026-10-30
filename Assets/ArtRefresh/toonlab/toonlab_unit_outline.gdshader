shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, shadows_disabled;

// 单位描边：反向壳（inverted hull）。
// 画背面 + 顶点沿法线外扩 = 只在该物体轮廓外形成一圈粗线，只作用于挂了它的物体。
// 与 leopeltola 的全屏后处理描边是两条路：这条不会碰到别的物体、也不依赖 depth/normal 纹理。

uniform float width : hint_range(0.001, 0.2) = 0.03;
uniform float scale_factor : hint_range(1.0, 1.3) = 1.03;
uniform int mode = 0; // 0 = 沿法线外扩（等厚） 1 = 以原点为中心整体放大（更干净，但厚度随离原点距离变）
uniform vec4 outline_color : source_color = vec4(0.02, 0.02, 0.03, 1.0);

void vertex() {
	if (mode == 0) {
		VERTEX += normalize(NORMAL) * width;
	} else {
		VERTEX *= scale_factor;
	}
}

void fragment() {
	ALBEDO = outline_color.rgb;
}
