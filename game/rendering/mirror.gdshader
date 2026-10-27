shader_type canvas_item;

// Shows what has been drawn so far, the 3D view, reversed left to right.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear;

void fragment() {
    COLOR = texture(screen_texture, vec2(1.0 - SCREEN_UV.x, SCREEN_UV.y));
}
