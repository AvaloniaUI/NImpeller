using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

// Builds the Impeller SDK from the external/flutter submodule.
partial class Build
{
    [Parameter("Force a gclient sync of external/flutter, even if the sync stamp is current")]
    readonly bool Sync;

    [Parameter("Engine runtime mode for BuildImpeller: release (default), profile or debug")]
    readonly string ImpellerRuntimeMode = "release";

    [Parameter("Emscripten version of the .NET wasm-tools packs used for the wasm build - Default is 3.1.56")]
    readonly string EmscriptenVersion = "3.1.56";

    const string DepotToolsRepo = "https://chromium.googlesource.com/chromium/tools/depot_tools.git";

    static readonly string[] RuntimeModes = { "release", "profile", "debug" };

    AbsolutePath FlutterRoot => RootDirectory / "external" / "flutter";
    AbsolutePath EngineSrc => FlutterRoot / "engine" / "src";
    AbsolutePath DepotToolsDirectory => RootDirectory / "external" / "depot_tools";
    AbsolutePath SyncStampFile => RootDirectory / ".nuke" / "temp" / "flutter-sync.json";
    // DEPS installs ninja at the solution root.
    AbsolutePath EngineNinja => FlutterRoot / "third_party" / "ninja" / (IsWindowsHost ? "ninja.exe" : "ninja");

    static bool IsWindowsHost => OperatingSystem.IsWindows();
    static bool IsMacHost => OperatingSystem.IsMacOS();
    static bool IsLinuxHost => OperatingSystem.IsLinux();
    static string HostCpu => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
    static string HostPlatform => (IsWindowsHost ? "windows-" : IsMacHost ? "darwin-" : "linux-") + HostCpu;

    Target SyncFlutter => _ => _
        .Description("Fetch depot_tools and gclient sync the external/flutter engine dependencies")
        .Executes(() =>
        {
            EnsureDepotTools();

            var platforms = Platform != null || All ? ResolveBuildPlatforms() : Array.Empty<string>();
            var customVars = new SortedSet<string>(platforms.SelectMany(RequiredCustomVars));
            SyncFlutterIfNeeded(customVars);
        });

    Target BuildImpeller => _ => _
        .Description("Build the Impeller SDK from external/flutter into artifacts/impeller/<platform> and regenerate bindings")
        .Requires(() => Platform != null || All)
        .DependsOn(SyncFlutter)
        .Executes(() =>
        {
            if (!RuntimeModes.Contains(ImpellerRuntimeMode))
            {
                throw new Exception($"Invalid runtime mode: {ImpellerRuntimeMode}. Valid modes: {string.Join(", ", RuntimeModes)}");
            }

            var platforms = ResolveBuildPlatforms();
            foreach (var platform in platforms)
            {
                BuildImpellerPlatform(platform, ImpellerRuntimeMode);
            }

            GenerateBindingsWithPlatform(platforms.Contains(HostPlatform) ? HostPlatform : platforms[0]);
        });

    string[] ResolveBuildPlatforms()
    {
        if (!string.IsNullOrEmpty(Platform) && All)
        {
            throw new Exception("Cannot specify both --platform and --all");
        }

        var candidates = SupportedPlatforms.Concat(LocallyBuiltPlatforms).ToArray();

        if (!All)
        {
            if (!candidates.Contains(Platform))
            {
                throw new Exception($"Invalid platform: {Platform}. Valid platforms: {string.Join(", ", candidates)}");
            }

            if (!CanBuildOnHost(Platform, out var reason))
            {
                throw new Exception($"Cannot build {Platform} on {HostPlatform}: {reason}");
            }

            return new[] { Platform };
        }

        var buildable = new List<string>();
        foreach (var platform in candidates.Except(OptInPlatforms))
        {
            if (CanBuildOnHost(platform, out var reason))
            {
                buildable.Add(platform);
            }
            else
            {
                Log.Warning("Skipping {Platform}: {Reason}", platform, reason);
            }
        }

        return buildable.ToArray();
    }

    static bool CanBuildOnHost(string platform, out string reason)
    {
        reason = null;
        if (platform == "wasm")
        {
            reason = "the wasm build needs a Linux or macOS host";
            return !IsWindowsHost;
        }
        if (platform == "linux-x64")
        {
            reason = "needs a linux-x64 host";
            return IsLinuxHost && HostCpu == "x64";
        }
        if (platform.StartsWith("linux-"))
        {
            reason = "needs a Linux host";
            return IsLinuxHost;
        }
        if (platform.StartsWith("darwin-"))
        {
            reason = "needs a macOS host";
            return IsMacHost;
        }
        if (platform.StartsWith("windows-"))
        {
            reason = "needs a Windows host";
            return IsWindowsHost;
        }
        if (platform.StartsWith("android-"))
        {
            reason = "needs a macOS or linux-x64 host";
            return IsMacHost || (IsLinuxHost && HostCpu == "x64");
        }

        reason = "unknown platform";
        return false;
    }

    static IEnumerable<string> RequiredCustomVars(string platform)
    {
        // tools/gn --wasm requires it, though the build uses the .NET Emscripten.
        if (platform == "wasm")
        {
            yield return "download_emsdk";
        }
    }

    void EnsureDepotTools()
    {
        if (Directory.Exists(DepotToolsDirectory / ".git"))
        {
            return;
        }

        Log.Information("Cloning depot_tools into {Directory}...", DepotToolsDirectory);
        Run("git", new[] { "clone", DepotToolsRepo, DepotToolsDirectory.ToString() }, RootDirectory);
    }

    record SyncStamp(string Head, string DepsHash, string[] CustomVars);

    void SyncFlutterIfNeeded(SortedSet<string> requiredVars)
    {
        if (!File.Exists(FlutterRoot / "DEPS"))
        {
            throw new Exception($"{FlutterRoot} is not checked out. Run: git submodule update --init external/flutter");
        }

        var head = Capture("git", new[] { "rev-parse", "HEAD" }, FlutterRoot);
        var depsHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FlutterRoot / "DEPS")));

        SyncStamp previous = null;
        if (File.Exists(SyncStampFile))
        {
            previous = JsonSerializer.Deserialize<SyncStamp>(File.ReadAllText(SyncStampFile));
        }

        // Never drop vars from earlier syncs.
        var customVars = new SortedSet<string>(requiredVars);
        customVars.UnionWith(previous?.CustomVars ?? Array.Empty<string>());

        string reason = null;
        if (Sync)
            reason = "--sync was passed";
        else if (previous == null || !File.Exists(EngineNinja))
            reason = "engine dependencies are missing";
        else if (previous.Head != head)
            reason = $"submodule HEAD changed ({previous.Head[..10]} -> {head[..10]})";
        else if (previous.DepsHash != depsHash)
            reason = "DEPS changed";
        else if (!requiredVars.IsSubsetOf(previous.CustomVars))
            reason = $"new gclient vars are needed ({string.Join(", ", requiredVars.Except(previous.CustomVars))})";

        if (reason == null)
        {
            Log.Information("external/flutter dependencies are up to date (use --sync to force a sync)");
            return;
        }

        Log.Information("Running gclient sync: {Reason}", reason);
        WriteGclientFile(customVars);

        var gclient = DepotToolsDirectory / (IsWindowsHost ? "gclient.bat" : "gclient");
        Run(gclient, new[] { "sync", "-D" }, FlutterRoot, ToolEnvironment());

        SyncStampFile.Parent.CreateDirectory();
        File.WriteAllText(SyncStampFile, JsonSerializer.Serialize(new SyncStamp(head, depsHash, customVars.ToArray())));
    }

    // Mirrors engine/scripts/standard.gclient.
    void WriteGclientFile(IEnumerable<string> customVars)
    {
        var vars = string.Concat(customVars.Select(v => $"\n      \"{v}\": True,"));
        File.WriteAllText(FlutterRoot / ".gclient", $$"""
            solutions = [
              {
                "custom_deps": {},
                "deps_file": "DEPS",
                "managed": False,
                "name": ".",
                "safesync_url": "",
                "url": "https://github.com/flutter/flutter.git",
                "custom_vars": {{{vars}}
                },
              },
            ]

            """);
    }

    void BuildImpellerPlatform(string platform, string mode)
    {
        Log.Information("Building Impeller SDK for {Platform} ({Mode})...", platform, mode);

        var outName = $"nimpeller_{platform.Replace('-', '_')}_{mode}";
        var outDir = EngineSrc / "out" / outName;

        var gnArgs = new List<string> { "--runtime-mode", mode, "--target-dir", outName, "--no-rbe", "--no-goma", "--no-enable-unittests" };
        if (mode == "debug" || platform == "wasm")
        {
            gnArgs.Add("--no-lto");
        }
        if (mode == "debug")
        {
            gnArgs.Add("--no-stripped");
        }

        var ninjaTarget = "flutter/impeller/toolkit/interop:sdk";
        var cpu = platform.Split('-').Last();

        switch (platform.Split('-')[0])
        {
            case "wasm":
                SetupEmscriptenShim(out var shimDir);
                gnArgs.AddRange(new[] { "--wasm", "--gn-args", $"emsdk_dir=\"{shimDir}\"" });
                ninjaTarget = "flutter/wasm:impeller_sdk";
                break;
            case "linux":
                if (cpu != "x64")
                    gnArgs.AddRange(new[] { "--linux", "--linux-cpu", cpu });
                // System font matching (ImpellerTypographyContextMatch*).
                gnArgs.Add("--enable-fontconfig");
                break;
            case "darwin":
                gnArgs.AddRange(new[] { "--mac", "--mac-cpu", cpu });
                break;
            case "windows":
                gnArgs.AddRange(new[] { "--windows-cpu", cpu });
                break;
            case "android":
                gnArgs.AddRange(new[] { "--android", "--android-cpu", cpu });
                break;
        }

        var gn = EngineSrc / "flutter" / "tools" / (IsWindowsHost ? "gn.bat" : "gn");
        Run(gn, gnArgs, EngineSrc, ToolEnvironment());
        Run(EngineNinja, new[] { "-C", outDir.ToString(), ninjaTarget }, EngineSrc, ToolEnvironment());

        var zips = (outDir / "zip_archives").GlobFiles("**/impeller_sdk.zip");
        var zip = zips.FirstOrDefault(z => z.Parent.Name == platform)
            ?? zips.OrderByDescending(z => File.GetLastWriteTimeUtc(z)).FirstOrDefault()
            ?? throw new Exception($"impeller_sdk.zip not found under {outDir / "zip_archives"}");

        var platformDir = ArtifactsDirectory / platform;
        platformDir.CreateOrCleanDirectory();
        ZipFile.ExtractToDirectory(zip, platformDir);

        // Export only the C API, so the archive links next to SkiaSharp/HarfBuzzSharp and into NativeAOT apps.
        InternalizeStaticLibrary(platform, platformDir, outDir / "nimpeller_internalize");

        Log.Information("Wrote {Platform} SDK to {Directory}", platform, platformDir);
    }

    // The latest stable version of a .NET Emscripten pack ("Sdk", "Node", "Cache"); returns its tools dir.
    AbsolutePath EmscriptenPack(string name)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root
            ? (AbsolutePath)root
            : (AbsolutePath)Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) / ".dotnet";
        var rid = (IsMacHost ? "osx-" : "linux-") + HostCpu;
        var dir = dotnetRoot / "packs" / $"Microsoft.NET.Runtime.Emscripten.{EmscriptenVersion}.{name}.{rid}";
        if (!Directory.Exists(dir))
        {
            throw new Exception($"Missing .NET pack {dir}. Install the workload: dotnet workload install wasm-tools-net10");
        }

        // Prefer stable versions.
        var versions = Directory.GetDirectories(dir).Select(Path.GetFileName).ToArray();
        var stable = versions.Where(v => !v.Contains('-')).ToArray();
        var version = (stable.Length > 0 ? stable : versions)
            .OrderBy(v => Version.TryParse(v.Split('-')[0], out var parsed) ? parsed : new Version())
            .Last();
        return dir / version / "tools";
    }

    // Fake emsdk dir over the .NET Emscripten packs, so the ABI matches dotnet publish.
    void SetupEmscriptenShim(out AbsolutePath shimDir)
    {
        var sdkPack = EmscriptenPack("Sdk");
        var nodePack = EmscriptenPack("Node");
        var cachePack = EmscriptenPack("Cache");
        Log.Information("Emscripten: {Path}", sdkPack);

        shimDir = RootDirectory / "external" / $"emsdk-dotnet-{EmscriptenVersion}";
        var upstream = shimDir / "upstream";
        upstream.CreateDirectory();
        foreach (var (link, target) in new[] { ("emscripten", sdkPack / "emscripten"), ("bin", sdkPack / "bin") })
        {
            var linkPath = upstream / link;
            if (new FileInfo(linkPath).LinkTarget != null || Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath);
            }
            Directory.CreateSymbolicLink(linkPath, target);
        }

        File.WriteAllText(shimDir / ".emscripten", $"""
            LLVM_ROOT = '{sdkPack / "bin"}'
            BINARYEN_ROOT = '{sdkPack}'
            NODE_JS = '{nodePack / "bin" / "node"}'
            CACHE = '{cachePack / "emscripten" / "cache"}'
            FROZEN_CACHE = True
            COMPILER_ENGINE = NODE_JS
            JS_ENGINES = [NODE_JS]

            """);
    }

    Dictionary<string, string> ToolEnvironment()
    {
        var env = new Dictionary<string, string>
        {
            ["PATH"] = DepotToolsDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };
        if (IsWindowsHost)
        {
            // Use local Visual Studio.
            env["DEPOT_TOOLS_WIN_TOOLCHAIN"] = "0";
        }
        return env;
    }

    static void Run(string fileName, IEnumerable<string> arguments, string workingDirectory, IDictionary<string, string> environment = null)
    {
        var args = arguments.ToArray();
        Log.Information("> {Command} {Arguments}", fileName, string.Join(' ', args));

        using var process = Process.Start(CreateStartInfo(fileName, args, workingDirectory, environment))!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}");
        }
    }

    static string Capture(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var startInfo = CreateStartInfo(fileName, arguments, workingDirectory, null);
        startInfo.RedirectStandardOutput = true;

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}");
        }
        return output;
    }

    static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string> arguments, string workingDirectory, IDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo(fileName) { WorkingDirectory = workingDirectory, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }
        return startInfo;
    }
}
