shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform vec4 echo_color : source_color = vec4(0.52, 0.58, 0.70, 1.0);
uniform float progress = 1.0;
uniform float seed = 0.0;
varying vec3 local_point;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void vertex() {
	local_point = VERTEX;
}

void fragment() {
	// 硬边溶解代替多层透明混合；残影没有阴影、灯光或下一遍描边。
	float lane = floor(local_point.y * 14.0);
	float noise = hash(vec3(lane, seed, 1.0));
	if (noise < progress * 0.88 + 0.18) { discard; }
	float band = step(0.25, dot(normalize(NORMAL), normalize(vec3(-0.3, 0.7, 0.8))));
	ALBEDO = echo_color.rgb * mix(0.57, 1.0, band) * (1.0 - progress * 0.22);
}
