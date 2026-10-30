shader_type spatial;
render_mode specular_disabled;
varying vec3 world;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
	vec2 c=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
	return mix(mix(hash(c),hash(c+vec2(1,0)),f.x),mix(hash(c+vec2(0,1)),hash(c+vec2(1,1)),f.x),f.y);
}
void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
	vec2 p=world.xz;
	float broad=noise(p*.16), small=noise(p*1.1);
	vec3 soil=mix(vec3(.065,.050,.022),vec3(.12,.095,.045),broad);
	vec3 moss=mix(vec3(.055,.095,.025),vec3(.105,.165,.055),small);
	float patch=smoothstep(.28,.70,broad+small*.16);
	vec2 cell=floor(p*2.7), leaf=fract(p*2.7)-.5;
	float seed=hash(cell), angle=seed*6.283;
	leaf=mat2(vec2(cos(angle),-sin(angle)),vec2(sin(angle),cos(angle)))*leaf;
	float litter=(1.0-smoothstep(.05,.13,abs(leaf.x)+abs(leaf.y)*.35))*step(abs(leaf.y),.32)*step(.78,seed);
	ALBEDO=mix(mix(soil,moss,patch),vec3(.22,.14,.045),litter*.5);
	ROUGHNESS=1.0;
}
