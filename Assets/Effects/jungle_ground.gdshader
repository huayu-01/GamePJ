shader_type spatial;
render_mode specular_disabled;
#include "res://Assets/Effects/meadow_palette.gdshaderinc"
varying vec3 world;
void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
	vec2 p=world.xz;
	vec2 cell=floor(p*2.7), leaf=fract(p*2.7)-.5;
	float seed=meadow_hash(cell), angle=seed*6.283;
	leaf=mat2(vec2(cos(angle),-sin(angle)),vec2(sin(angle),cos(angle)))*leaf;
	float litter=(1.0-smoothstep(.05,.13,abs(leaf.x)+abs(leaf.y)*.35))*step(abs(leaf.y),.32)*step(.78,seed);
	ALBEDO=mix(meadow_ground_color(p),vec3(.17,.14,.055),litter*.12);
	ROUGHNESS=1.0; SPECULAR=0.0;
}
void light() {
	DIFFUSE_LIGHT+=meadow_diffuse(NORMAL,LIGHT)*ATTENUATION*LIGHT_COLOR/PI;
}
