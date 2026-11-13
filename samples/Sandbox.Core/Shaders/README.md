# Sandbox shaders

`*.frag.iplr` files are `impellerc` output for the `*.frag` sources next to them, with runtime
stages for OpenGL ES, Metal and Vulkan. They are embedded into `Sandbox.Core`. Rebuild them from a
Flutter engine build:

```sh
IMPELLERC=<engine out dir>/impellerc
SHADER_LIB=<flutter>/engine/src/flutter/impeller/compiler/shader_lib
for s in plasma ripple texture_sample swirl gradient runtime_stage_border runtime_stage_example \
         runtime_stage_filter_circle runtime_stage_filter_example runtime_stage_filter_warp runtime_stage_vector_array; do
  $IMPELLERC --runtime-stage-gles --runtime-stage-gles3 --runtime-stage-metal --runtime-stage-vulkan \
    --iplr --input=$s.frag --sl=$s.frag.iplr --spirv=/tmp/$s.spv --include=$SHADER_LIB
done
```

`gradient.frag` and the `runtime_stage_*.frag` files are copied unchanged from
`engine/src/flutter/impeller/fixtures/` in the Flutter repository (BSD license, see
`../Fonts/LICENSE.flutter`). They are used by `RuntimeEffectsScene`.
