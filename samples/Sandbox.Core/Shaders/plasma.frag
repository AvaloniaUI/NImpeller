// Color source: an animated plasma over a uSize-sized rect.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform float uTime;

out vec4 fragColor;

const float kPi = 3.14159265;

void main() {
  vec2 uv = FlutterFragCoord().xy / uSize;
  vec2 p = uv * 6.0;
  float v = sin(p.x + uTime);
  v += sin((p.y + uTime) * 0.5);
  v += sin((p.x + p.y + uTime) * 0.5);
  vec2 c = uv + 0.5 * vec2(sin(uTime / 5.0), cos(uTime / 3.0));
  v += sin(sqrt(100.0 * dot(c, c) + 1.0) + uTime);
  v *= 0.5;
  vec3 rgb = 0.5 + 0.5 * vec3(sin(v * kPi), sin(v * kPi + 2.0 * kPi / 3.0), sin(v * kPi + 4.0 * kPi / 3.0));
  fragColor = vec4(rgb, 1.0);
}
