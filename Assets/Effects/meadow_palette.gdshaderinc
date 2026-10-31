// 草叶和地面共享世界坐标色场，根部不会因两套调色产生一圈硬边。
float meadow_hash(vec2 p) {
	// 整数网格哈希避免三角函数近似与浮点优化让相邻格点取到不同值。
	// 顶点草根和片元地表必须对同一个格点生成完全一致的随机值。
	uvec2 cell=uvec2(ivec2(p));
	uint value=cell.x*374761393u+cell.y*668265263u;
	value=(value^(value>>13u))*1274126177u;
	value=value^(value>>16u);
	return float(value&0x00ffffffu)/16777215.0;
}
float meadow_noise(vec2 p) {
	vec2 cell=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
	return mix(mix(meadow_hash(cell),meadow_hash(cell+vec2(1,0)),f.x),mix(meadow_hash(cell+vec2(0,1)),meadow_hash(cell+vec2(1,1)),f.x),f.y);
}
vec3 meadow_ground_color(vec2 p) {
	float broad=meadow_noise(p*.095), detail=meadow_noise(p*.44);
	float variation=smoothstep(.22,.78,broad*.78+detail*.22);
	vec3 grass=mix(vec3(.028,.077,.014),vec3(.13,.245,.050),variation);
	float earth=(1.0-smoothstep(.15,.30,meadow_noise(p*.075+vec2(21,9))))*.52;
	return mix(grass,vec3(.115,.101,.044),earth);
}
float meadow_diffuse(vec3 normal,vec3 light) { return clamp((dot(normal,light)+.32)/1.32,0.0,1.0); }
