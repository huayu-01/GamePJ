shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_opaque;

// 只给"遮罩层"用的白色剪影材质：进单独 SubViewport，给屏幕空间描边做 mask。

void fragment() {
	ALBEDO = vec3(1.0);
}
