shader_type spatial;
render_mode specular_disabled;
uniform bool bark = false;
varying vec3 world;
varying float up;
float hash(vec3 p) { return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453); }
void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; up=clamp((MODEL_NORMAL_MATRIX*NORMAL).y,0.0,1.0); }
void fragment() {
	float coarse=hash(floor(world*2.3));
	float bands=sin(world.y*2.8+sin(world.x*7.0+world.z*5.0)*.8)*.5+.5;
	vec3 stone=mix(vec3(.07,.085,.065),vec3(.16,.18,.13),coarse*.5+bands*.25);
	vec3 wood=mix(vec3(.04,.025,.01),vec3(.12,.08,.035),bands);
	float moss=smoothstep(.35,.75,sin(world.x*.8+world.z*.65+world.y*.4)*.25+.45+up*.3);
	ALBEDO=mix(bark?wood:stone,vec3(.085,.16,.045),moss*(bark?.25:.68));
	ROUGHNESS=1.0;
}
