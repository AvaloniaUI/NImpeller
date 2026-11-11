# Impeller .NET bindings

The general approach for NImpeller is to treat impeller.h as the only source of truth and manually write only some convenience code or code dealing with native->managed callbacks (there is only a few of those). The generator is supposed to generate the entire P/Invoke layer and at least 90% of .NET classes exposed as the public API.

Generated bindings file is NOT checked out into the repo, because right now the bindings considered to be versionless, i. e. being compatible with any version of impeller.h. You can find an example of generated code [here](https://gist.github.com/kekekeks/cc8b7dfbf2dae07e55ca9d4ee6a8e2d6) if you don't want to clone/build the repo and want a sneak peek.

### Running this thing

You need the .NET 10 SDK.

1) Generate the Impeller bindings. If you have never generated the bindings, they will be generated on first build of NImpeller via an MSBuild task. You should see this as `DownloadSdkAndGenerateBindings`. This task downloads the SDK for your own platform and generates the bindings from its headers. You can also use the InteropGen program directly with the `impeller.h` header, or use the Nuke task

```sh
./build.sh GenerateBindings
```

to build the bindings. It uses the SDK for your own platform and downloads it if it is missing. To use the SDK of a different platform, add `--platform <platform>`.

2) Run the Sandbox sample. The runtime file used is based on the `Impeller.targets` file. You can either manually set up the library you want to test with, or use the Nuke tasks below for automatically setting it up per commit, or with the latest commit. 

It will only work for platforms where SDL2 can create a GLES context. I've only tested it with Linux with an Intel iGPU, so good luck.

### Using pre-build Impeller binaries

Flutter builds toolkit binaries on a nightly feed, that you can download from.

Typical link is `https://storage.googleapis.com/flutter_infra_release/flutter/$FLUTTER_SHA/$PLATFORM_ARCH/impeller_sdk.zip`
Where:
- `$FLUTTER_SHA` is a commit hash
- `$PLATFORM_ARCH` is one of `linux-arm64`, `linux-x64`, `windows-arm64`, `windows-x64`, `darwin-arm64`, `darwin-x64` or `android-arm64`

From this zip you will need `include/impeller.h` and `lib/impeller.so` (or `impeller.dll`/`impeller.dylib`).

See [Impeller Toolkit: Prebuilt Artifacts](https://github.com/flutter/flutter/blame/908eab56e04a0925fc3c1a7d4bff1f4ae6351ae3/engine/src/flutter/impeller/toolkit/interop/README.md#L22)

The files can be automatically downloaded via [Nuke](https://nuke.build/docs/getting-started/installation/) using the Powershell, Bash, or global Nuke command. Using either:

```sh
./build.sh DownloadLatestImpeller --All
```

to download the newest Impeller build by the newest commit to Flutter. To download less, replace `--All` with `--platform <platform>`, `--include-host` (the platform of this machine), or both:

```sh
./build.sh DownloadLatestImpeller --platform linux-x64 --include-host
```

Or use

```sh
./build.sh DownloadImpeller --impeller-sha (Commit Sha) --All
```

For a specific commit.

### NuGet packages

`Pack` builds NuGet packages from a Flutter release tag or commit, using a local
[flutter/flutter](https://github.com/flutter/flutter) checkout (default `../flutter`) to find the
engine build. Only `git fetch <remote> --tags` is run on it.

```sh
./build.sh Pack --flutter-ref 3.47.9                  # stable  -> 3.47.9
./build.sh Pack --flutter-ref 3.47.9 --revision 1     # binding fix for the same release -> 3.47.9.1
./build.sh Pack --flutter-ref 3.50.0-0.4.pre          # beta    -> 3.50.0-pre.0.4.0
./build.sh Pack --flutter-ref a55d35a8b73             # commit  -> 3.51.0-dev.20261110.a55d35a
./build.sh ResolveFlutterVersion --flutter-ref 3.47.9 # show the commit, engine hash and version only
```

Options: `--flutter-root <path>`, `--flutter-remote <name>` (default `upstream`), `--no-fetch`.

Tags use `bin/internal/engine.version`, other commits use Flutter's content-aware hash. The SDK for
every desktop RID is downloaded and bindings are generated into `artifacts/staging/<version>/`
(`external/impeller_sdk` and `Generated/Bindings.g.cs` are not touched). The target then checks
that `impeller.h` is the same on all platforms and that the host native library matches the
generated `IMPELLER_VERSION`, and runs the unit tests. Packages go to `artifacts/packages/`:

- `NImpeller`: managed bindings, depends on `NImpeller.Native` (exact version)
- `NImpeller.Native`: meta package that depends on every RID package
- `NImpeller.Native.<rid>`: `runtimes/<rid>/native/` for `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`

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

The rendering tests need Linux with Mesa `llvmpipe` (software GL). The test assembly sets the Mesa and SDL environment variables for this itself. On macOS, the rendering tests are skipped.

To run one test class, use an xUnit filter, for example:

```sh
dotnet test tests/NImpeller.Tests/NImpeller.Tests.csproj --filter-class "*GoldenImageTests"
```
