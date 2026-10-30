shader_type spatial;
render_mode diffuse_toon, specular_disabled;

uniform vec4 grass_color : source_color = vec4(0.34, 0.48, 0.19, 1.0);
uniform vec4 grass_light : source_color = vec4(0.43, 0.56, 0.24, 1.0);
uniform vec4 soil_color : source_color = vec4(0.65, 0.56, 0.34, 1.0);
varying vec3 world_position;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(cell), hash(cell + vec2(1,0)), f.x),
               mix(hash(cell + vec2(0,1)), hash(cell + vec2(1,1)), f.x), f.y);
}
void vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec2 p = world_position.xz;
    float n = noise(p * 0.22);
    float ring = 1.0 - smoothstep(2.0, 3.3, abs(length(p) - 34.0) + (n - 0.5) * 0.6);
    float cross_path = 1.0 - smoothstep(2.0, 3.2, min(abs(p.x), abs(p.y)) + (n - 0.5) * 0.5);
    float clearing = 1.0 - smoothstep(17.5, 21.5, length(p) + (n - 0.5) * 1.4);
    float path = max(ring, max(cross_path * 0.8, clearing * 0.6));
    vec3 grass = mix(grass_color.rgb, grass_light.rgb, n);
    ALBEDO = mix(grass, soil_color.rgb * mix(0.93, 1.05, n), path);
    ROUGHNESS = 1.0;
}
