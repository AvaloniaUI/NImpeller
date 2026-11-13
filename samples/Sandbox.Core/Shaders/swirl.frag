// Color source: samples uTexture through an animated swirl.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform float uTime;
uniform sampler2D uTexture;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uSize - 0.5;
  float r = length(uv);
  float a = atan(uv.y, uv.x) + sin(uTime) * 3.0 * max(0.0, 0.5 - r);
  fragColor = texture(uTexture, vec2(cos(a), sin(a)) * r + 0.5);
}
