# NImpeller - .NET Bindings for Impeller

NImpeller are .NET Bindings for [Impeller](https://github.com/flutter/flutter/tree/master/engine/src/flutter/impeller), the rendering runtime for Flutter.

The general approach for NImpeller is to treat impeller.h as the only source of truth and manually write only some convenience code or code dealing with native->managed callbacks (there is only a few of those). The generator is supposed to generate the entire P/Invoke layer and at least 90% of .NET classes exposed as the public API.

Generated bindings file are NOT checked into the repo. Instead, versions are bound by generators and CI. Specific breaking API changes are split into branches and are bundled into Nuget packages. The bindings are intended to be slim and only require what is needed to operate the SDK, to make it easier to work with any upstream changes.

### Running this thing

You need the .NET 10 SDK.

1) Generate the Impeller bindings. If you have never generated the bindings, they are generated on the first build of NImpeller by an MSBuild task (`DownloadSdkAndGenerateBindings`). It downloads the pinned Impeller SDK for your platform and generates the bindings from its `impeller.h`. You can also run the Nuke target:

```sh
./build.sh GenerateBindings                 # the SDK of this machine
./build.sh GenerateBindings --rid linux-x64 # the SDK of another RID
```

2) Run the Sandbox sample. `Impeller.targets` copies `external/impeller_sdk/<rid>/lib/libimpeller.*` of the host RID to the output directory.

```sh
dotnet run --project samples/Sandbox -- --api OpenGL --scene mmark --backend SDL
```

#### Android Sandbox

`samples/Sandbox.Android` runs the same scenes on Android with OpenGL ES or Vulkan. It needs the `android` workload. The project is not in `NImpeller.slnx`, so the solution still builds without the Android workload.

```sh
./build.sh DownloadImpeller --rid android-arm64
./build.sh DownloadImpeller --rid android-x64
dotnet build samples/Sandbox.Android -t:Run
# Open a scene directly:
adb shell am start -n com.nimpeller.sandbox/.RenderActivity --es scene shaders --es api OpenGLES
```

**NOTE:** The Impeller C SDK and upstream libraries are broken for Vulkan on Android. It currently shows a single stretched pixel on swapchain at 1x1 and does not resize it on Android [(TODO(163070) in `khr_swapchain_vk.cc`)](https://github.com/flutter/flutter/blob/6b2bb92cec12bc71e01dd04b659e863e0b3c6d6f/engine/src/flutter/impeller/renderer/backend/vulkan/swapchain/khr/khr_swapchain_vk.cc#L76).

#### iOS, tvOS and Mac Catalyst Sandbox

`samples/Sandbox.Apple` runs the scenes with Metal in an `MTKView` on iOS, tvOS and Mac Catalyst. A tap (or a click of the Siri Remote) shows the next scene. It needs macOS with Xcode and the `ios`, `tvos` and `maccatalyst` workloads, and is not in `NImpeller.slnx`. `Impeller.targets` embeds `impeller.framework` from `external/impeller_sdk/<platform>/frameworks/` for the device or simulator RID.

```sh
./build.sh DownloadImpeller --rid ios            # or tvos, maccatalyst
dotnet build samples/Sandbox.Apple -f net10.0-ios -t:Run    # the iOS simulator; -f net10.0-tvos, net10.0-maccatalyst
# Unattended: start at a scene, and move to the next one every 3 seconds with one console line per scene.
SIMCTL_CHILD_SANDBOX_SCENE=mmark SIMCTL_CHILD_SANDBOX_CYCLE=3 xcrun simctl launch --console booted com.avaloniaui.nimpeller.sandbox
```

### Impeller natives

The native libraries and `impeller.h` come from [AvaloniaUI/impeller-builder](https://github.com/AvaloniaUI/impeller-builder). It builds the Impeller SDK from [AvaloniaUI/flutter](https://github.com/AvaloniaUI/flutter): an `avalonia/<flutter tag>` patch branch, or an upstream release tag. `impeller.lock.json` pins one of its releases: the source commit, the Flutter version, the binding revision, and the size and SHA-256 of each archive.

| Target | Does |
|---|---|
| `DownloadImpeller [--rid <rid or platform>\|--all] [--symbols] [--tools]` | Downloads and checks the pinned archives, and unpacks them to `external/impeller_sdk/<rid or platform>/` (`symbols/` and `tools/` below it). The default is the host RID. |
| `DownloadImpeller --builder-dir <path>` | The same from a local impeller-builder `release/` folder, checked against its `manifest.json` and `SHA256SUMS`. Use it to test a builder change before it is published. |
| `BumpImpeller --tag <version>-<sha10>` | Pins another release: rewrites the lock, sets the revision (0 for a new Flutter version, +1 for another build of the same version), regenerates the bindings and prints the API change. |
| `CompileShaders` | Rebuilds `samples/Sandbox.Core/Shaders/*.frag.iplr` with `impellerc` from the pinned tools archive. |

The RIDs are `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, `win-arm64`, `android-arm64`, `android-arm` and `android-x64`. The Apple mobile platforms are `ios` (iOS 15.0 or newer, device arm64, simulator arm64 and x64), `tvos` (tvOS 16.0 or newer, the same slices) and `maccatalyst` (15.0 or newer, arm64 and x64); their archives hold `impeller.framework` under `frameworks/<RID root>/`. The libraries are release builds with LTO and without debug information; the symbols are in the `-symbols` archives.

### NuGet packages

`Pack` builds the NuGet packages of the pinned release:

```sh
./build.sh Pack                                               # the release in impeller.lock.json
./build.sh Pack --builder-dir ../impeller-builder/release     # local builds (all 9 RIDs and 3 Apple platforms)
```

The version comes from the lock: Flutter `3.50.0` is `3.50.0`, or `3.50.0.N` with binding revision `N`; the beta `3.50.0-0.4.pre` is `3.50.0-pre.0.4.N`. Raise `revision` in the lock by hand for a release that changes only NImpeller.

The archives of every RID and Apple platform and the bindings are staged in `artifacts/staging/<version>/` (`external/impeller_sdk` and `Generated/Bindings.g.cs` are not touched). The target checks that `impeller.h` is the same everywhere and that the host native library matches the generated `IMPELLER_VERSION`, and runs the unit tests. Packages go to `artifacts/packages/`:

- `NImpeller`: managed bindings, depends on `NImpeller.Native` (exact version)
- `NImpeller.Native`: meta package that depends on every RID and Apple platform package
- `NImpeller.Native.<rid>`: the library in `runtimes/<rid>/native/`
- `NImpeller.Native.<rid>.Symbols`: its debug symbols (`.so.debug`, `.dylib.dSYM` or `.dll.pdb`) in `runtimes/<rid>/native/`, next to the library
- `NImpeller.Native.ios`, `.tvos`: `impeller.framework` in `runtimes/<ios|tvos>/native/` and `runtimes/<ios|tvos>simulator/native/`
- `NImpeller.Native.maccatalyst`: `runtimes/maccatalyst/native/impeller.framework.zip` (the versioned framework; NuGet cannot hold its symbolic links)
- `NImpeller.Native.<platform>.Symbols`: the dSYMs in `symbols/<RID root>/impeller.framework.dSYM`, outside `runtimes/` so apps do not embed them

On iOS, tvOS and Mac Catalyst, NImpeller loads `@rpath/impeller.framework/impeller` (`ImpellerNativeLibrary`); elsewhere `impeller` is found by the default probing.

### Releasing

1. Push the Avalonia patches to `avalonia/<flutter tag>` in AvaloniaUI/flutter (or use an upstream tag as is).
2. Run **Build Impeller** in AvaloniaUI/impeller-builder with that ref. It publishes `<flutter tag>-<sha10>`.
3. Run `./build.sh BumpImpeller --tag <flutter tag>-<sha10>` and `./test-headless.sh`, and commit `impeller.lock.json`.
4. Run the Release workflow (`.github/workflows/release.yml`) on that commit.

### Handles

Native impeller handles are represented by two separate types:

- FooHandle - a SafeHandle passed to P/Invoke layer
- Foo - a managed wrapper around FooHandle that exposes actual methods and factories in idiomatic .NET style

There is no object tracking, for each call to a native function that returns you an existing `Foo` instance you will get a new `Foo` object with reference counter of the native one being increased by one. 

### Interop generator

The bindings are generated by InteropGen, a simple C# console app that uses CppAst library to parse impeller.h and conver it into `NativeModel` that has some .NET-specific stuff already mapped.

The model is then used to generate the bindings code. Generator is aware of impeller.h naming conventions, so it will place functions to appropriate classes, properly wrap factories for `New`-suffixed methods, call `*Retain` function when obtaining and existing object counter, etc.

### Incantations

1) Follow https://github.com/flutter/flutter/blob/cb94cc07057451941b0e1e6b75919fa0fae19c3a/docs/engine/contributing/Compiling-the-engine.md

```
# Update ninja files without attempting to rebuild THE ENTIRE FLUTTER
./engine/src/flutter/tools/gn --runtime-mode debug --no-stripped --no-lto --no-rbe --no-goma

# Compile just interop library and not THE ENTIRE FLUTTER
ninja -C engine/src/out/host_debug  library

# Generate compile_commands.json for IDE integration for just the interop library and deps and not THE ENTIRE FLUTTER
mkdir -p ide
/path/to/1.13.1/ninja -C engine/src/out/host_debug -t compdb-targets library > ide/compile_commands.json


#jq 'map(select(.command | startswith("vpython") or startswith("rm") or startswith("ln") | not))'

```

### Tests

Tests are included in `tests/NImpeller.Tests`. Run them with

```sh
dotnet test tests/NImpeller.Tests/NImpeller.Tests.csproj
```

or run `./test-headless.sh`

The rendering tests need Linux with Mesa `llvmpipe` for software GL. The test assembly sets the Mesa and SDL environment variables for this itself.
