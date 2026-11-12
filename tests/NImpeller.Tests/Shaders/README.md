# Test shaders

`*.frag.iplr` files are `impellerc` output for the `*.frag` sources next to them, with runtime
stages for OpenGL ES, Metal and Vulkan. Rebuild them from a Flutter engine build:

```sh
IMPELLERC=<engine out dir>/impellerc
SHADER_LIB=<flutter>/engine/src/flutter/impeller/compiler/shader_lib
for s in tinted_texture invert_filter; do
  $IMPELLERC --runtime-stage-gles --runtime-stage-gles3 --runtime-stage-metal --runtime-stage-vulkan \
    --iplr --input=$s.frag --sl=$s.frag.iplr --spirv=/tmp/$s.spv --include=$SHADER_LIB
done
```
