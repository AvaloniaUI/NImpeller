// Color source: samples uTexture across the drawn rect. Port of interop_runtime_stage_cs.frag.
#include <flutter/runtime_effect.glsl>

uniform vec2 uSize;
uniform sampler2D uTexture;

out vec4 fragColor;

void main() {
  fragColor = texture(uTexture, FlutterFragCoord().xy / uSize);
}
