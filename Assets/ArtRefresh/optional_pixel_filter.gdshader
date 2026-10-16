shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform float pixel_size : hint_range(1.0, 12.0, 1.0) = 4.0;

void fragment() {
	vec2 cell = SCREEN_PIXEL_SIZE * max(pixel_size, 1.0);
	vec2 uv = (floor(SCREEN_UV / cell) + 0.5) * cell;
	COLOR = textureLod(screen_texture, uv, 0.0);
}
