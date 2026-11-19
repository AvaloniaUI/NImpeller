# Sandbox shaders

`*.frag.iplr` files are `impellerc` output for the `*.frag` sources next to them, with runtime
stages for OpenGL ES, Metal and Vulkan. They are embedded into `Sandbox.Core`. Rebuild them with `impellerc` from the pinned impeller-builder tools archive:

```sh
./build.sh CompileShaders
```

It runs, for each `*.frag` here:

```sh
impellerc --runtime-stage-gles --runtime-stage-gles3 --runtime-stage-metal --runtime-stage-vulkan \
  --iplr --input=<name>.frag --sl=<name>.frag.iplr --spirv=<tmp>/<name>.frag.spv --include=<shader_lib>
```

`gradient.frag` and the `runtime_stage_*.frag` files are copied unchanged from
`engine/src/flutter/impeller/fixtures/` in the Flutter repository (BSD license, see
`../Fonts/LICENSE.flutter`). They are used by `RuntimeEffectsScene`.
