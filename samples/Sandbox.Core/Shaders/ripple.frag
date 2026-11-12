// Image filter: concentric ripples that displace the filter input.
// Impeller binds the input to uInput and writes its size to uSize.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform float uTime;
uniform sampler2D uInput;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uSize;
  vec2 d = uv - 0.5;
  float r = length(d);
  vec2 dir = r > 0.0001 ? d / r : vec2(0.0);
  float wave = sin(r * 45.0 - uTime * 5.0) * 0.012 * (1.0 - smoothstep(0.3, 0.7, r));
  fragColor = texture(uInput, uv + dir * wave);
}
