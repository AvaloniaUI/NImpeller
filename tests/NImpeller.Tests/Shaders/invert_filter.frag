// Image filter shader: Impeller binds the filter input to uInput and writes its size to uSize.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform sampler2D uInput;

out vec4 fragColor;

void main() {
  vec4 c = texture(uInput, FlutterFragCoord().xy / uSize);
  fragColor = vec4(c.a - c.rgb, c.a);
}
