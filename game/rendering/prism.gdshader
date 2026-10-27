shader_type canvas_item;

uniform sampler2D object_mask : filter_nearest, repeat_disable;
uniform vec2 viewport_size = vec2(1280.0, 720.0);
const float lens_radius = 0.32;
const float border_width = 0.005;

vec3 mask_at(ivec2 position, ivec2 size) {
    return texelFetch(object_mask, clamp(position, ivec2(0), size - ivec2(1)), 0).rgb;
}

void fragment() {
    vec2 offset = (UV - vec2(0.5)) * viewport_size / min(viewport_size.x, viewport_size.y);
    float radius = length(offset);
    if (radius > lens_radius) {
        discard;
    }
    ivec2 mask_size = textureSize(object_mask, 0);
    ivec2 position = ivec2(UV * vec2(mask_size));
    vec3 id = mask_at(position, mask_size);
    bool edge = any(notEqual(id, vec3(0.0))) && (
        any(notEqual(id, mask_at(position + ivec2(1, 0), mask_size))) ||
        any(notEqual(id, mask_at(position - ivec2(1, 0), mask_size))) ||
        any(notEqual(id, mask_at(position + ivec2(0, 1), mask_size))) ||
        any(notEqual(id, mask_at(position - ivec2(0, 1), mask_size))));
    COLOR = radius > lens_radius - border_width ? vec4(0.65, 0.85, 0.9, 1.0)
        : edge ? vec4(0.55, 0.95, 1.0, 1.0) : texture(TEXTURE, UV);
}
