shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 ink_color : source_color = vec4(1.0, 0.27, 0.20, 1.0);
uniform float opacity = 0.28;
uniform float progress = -1.0;
uniform bool line_shape = false;
uniform bool hostile = true;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float mark = 0.0;
	float time_mark = 0.0;
	if (line_shape) {
		float row = fract(UV.y * 3.0);
		float arrow = abs(abs(p.x) * 0.62 - row * 0.34);
		mark = (1.0 - smoothstep(0.02, 0.07, arrow)) * step(abs(p.x), 0.50) * (1.0-smoothstep(0.4,0.8,row));
		time_mark = progress < 0.0 ? 0.0 : (1.0 - smoothstep(0.025,0.055,abs(UV.y-(1.0-progress)))) * 0.55;
	} else {
		float r = length(p);
		if (r > 0.99) discard;
		float a = atan(p.y, p.x);
		float stroke_a = 1.0 - smoothstep(0.025,0.12,abs(r-(0.64+0.10*sin(a*1.4))));
		float stroke_b = 1.0 - smoothstep(0.018,0.07,abs(r-(0.86+0.04*sin(a*2.0))));
		mark = max(stroke_a*smoothstep(-0.3,0.6,sin(a+0.4)),stroke_b*smoothstep(0.15,0.8,sin(a-1.1)));
		mark *= 0.65 + 0.35*smoothstep(-0.5,0.7,sin(a*12.0+r*21.0));
		if (progress >= 0.0) {
			float phase = (a+3.14159265)/6.2831853;
			time_mark = (1.0-smoothstep(0.016,0.035,abs(r-0.94))) * step(phase,progress);
		}
	}
	float ink = max(mark, time_mark);
	ALBEDO = mix(ink_color.rgb, vec3(1.0,0.94,0.84), time_mark*0.32);
	ALPHA = (0.035 + ink * (hostile ? 0.32 : 0.16)) * opacity * ink_color.a;
}
