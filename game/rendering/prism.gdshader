shader_type canvas_item;

uniform sampler2D object_mask : filter_nearest, repeat_disable;
uniform vec2 viewport_size = vec2(1280.0, 720.0);
// The player is a mirror image: the lens is shown left to right reversed, with the rest of her view.
uniform bool flipped = false;
// The strip around each mirror seam, as x and y of its centre, its half width, and its strength, 0 when not in use, at
// the foot and at the top of the seam. The picture inside the strip is reversed about the seam's light.
uniform vec4 band_bottom[4];
uniform vec4 band_top[4];
const float lens_radius = 0.32;
const float border_width = 0.005;
const float band_edge_width = 0.003;
const vec4 band_tint = vec4(0.72, 0.86, 1.0, 1.0);

vec3 mask_at(ivec2 position, ivec2 size) {
    return texelFetch(object_mask, clamp(position, ivec2(0), size - ivec2(1)), 0).rgb;
}

// The point of the picture that shows at uv: its mirror image about the seam when uv is inside a band.
// inside is the strength of the band, and edge is that along the band's two sides, 0 outside it.
vec2 through_bands(vec2 uv, out float inside, out float edge) {
    inside = 0.0;
    edge = 0.0;
    for (int i = 0; i < 4; i++) {
        vec4 bottom = band_bottom[i];
        vec4 top = band_top[i];
        if (bottom.w <= 0.0 || abs(top.y - bottom.y) < 0.0001) {
            continue;
        }
        float along = (uv.y - bottom.y) / (top.y - bottom.y);
        if (along < 0.0 || along > 1.0) {
            continue;
        }
        float center = mix(bottom.x, top.x, along);
        float half_width = mix(bottom.z, top.z, along);
        float distance_from_axis = abs(uv.x - center);
        if (distance_from_axis < half_width) {
            float strength = mix(bottom.w, top.w, along);
            inside = strength;
            edge = half_width - distance_from_axis < band_edge_width ? strength : 0.0;
            return vec2(2.0 * center - uv.x, uv.y);
        }
    }
    return uv;
}

void fragment() {
    vec2 offset = (UV - vec2(0.5)) * viewport_size / min(viewport_size.x, viewport_size.y);
    float radius = length(offset);
    if (radius > lens_radius) {
        discard;
    }
    vec2 seen = flipped ? vec2(1.0 - UV.x, UV.y) : UV;
    float in_band;
    float on_band_edge;
    seen = clamp(through_bands(seen, in_band, on_band_edge), vec2(0.0), vec2(0.9999));
    ivec2 mask_size = textureSize(object_mask, 0);
    ivec2 position = ivec2(seen * vec2(mask_size));
    vec3 id = mask_at(position, mask_size);
    bool edge = any(notEqual(id, vec3(0.0))) && (
        any(notEqual(id, mask_at(position + ivec2(1, 0), mask_size))) ||
        any(notEqual(id, mask_at(position - ivec2(1, 0), mask_size))) ||
        any(notEqual(id, mask_at(position + ivec2(0, 1), mask_size))) ||
        any(notEqual(id, mask_at(position - ivec2(0, 1), mask_size))));
    vec4 picture = mix(texture(TEXTURE, seen), band_tint, 0.12 * in_band);
    COLOR = radius > lens_radius - border_width ? vec4(0.65, 0.85, 0.9, 1.0)
        : edge ? vec4(0.55, 0.95, 1.0, 1.0) : mix(picture, band_tint, on_band_edge);
}
