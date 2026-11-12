// Color source shader: samples uTexture across uSize and multiplies by uTint.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform vec4 uTint;
uniform sampler2D uTexture;

out vec4 fragColor;

void main() {
  fragColor = texture(uTexture, FlutterFragCoord().xy / uSize) * uTint;
}
