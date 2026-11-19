using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// The natives come from the AvaloniaUI/impeller-builder release pinned in impeller.lock.json:
//
//   ./build.sh DownloadImpeller [--rid <rid>|ios|tvos|maccatalyst|--all] [--symbols] [--tools] [--builder-dir <path>]
//   ./build.sh GenerateBindings [--rid <rid>]
//   ./build.sh BumpImpeller --tag <version>-<sha10>
//   ./build.sh CompileShaders
partial class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("RID of the Impeller SDK (linux-x64, osx-arm64, win-x64, android-arm64, ...) or Apple platform (ios, tvos, maccatalyst) - Default is the host")]
    readonly string Rid;

    [Parameter("All RIDs and Apple platforms of the lock file")]
    readonly bool All;

    [Parameter("Also unpack the debug symbols archive")]
    readonly bool Symbols;

    [Parameter("Also unpack the tools archive (impellerc), desktop RIDs only")]
    readonly bool Tools;

    [Parameter("Read the archives from a local impeller-builder release/ folder instead of the pinned release")]
    readonly AbsolutePath BuilderDir;

    [Parameter("impeller-builder release tag to pin, <flutter version>-<sha10>")]
    readonly string Tag;

    [Parameter("GitHub repository of the impeller-builder releases - Default is the lock's, or AvaloniaUI/impeller-builder")]
    readonly string Builder;

    AbsolutePath SdkDirectory => RootDirectory / "external" / "impeller_sdk";
    AbsolutePath BindingsFile => RootDirectory / "src" / "NImpeller" / "Generated" / "Bindings.g.cs";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
        });

    Target Restore => _ => _
        .Executes(() =>
        {
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
        });

    Target DownloadImpeller => _ => _
        .Description("Download, verify and unpack the pinned Impeller SDK into external/impeller_sdk/<rid or platform>/")
        .Executes(async () =>
        {
            var fetcher = ImpellerFetcher.Create(RootDirectory, BuilderDir);
            var parts = ImpellerParts.Main | (Symbols ? ImpellerParts.Symbols : 0);
            foreach (var rid in RequestedRids(fetcher.Lock))
            {
                var ridParts = parts | (Tools && ImpellerLock.IsDesktop(rid) ? ImpellerParts.Tools : 0);
                var dir = await fetcher.EnsureAsync(SdkDirectory, rid, ridParts);
                Log.Information("{Rid}: {Tag} in {Dir}", rid, fetcher.Lock.Tag, dir);
            }
        });

    Target GenerateBindings => _ => _
        .Description("Generate the bindings from the pinned impeller.h with InteropGen")
        .Executes(async () =>
        {
            var fetcher = ImpellerFetcher.Create(RootDirectory, BuilderDir);
            var dir = await fetcher.EnsureAsync(SdkDirectory, Rid ?? ImpellerLock.HostRid, ImpellerParts.Main);
            RunInteropGen((AbsolutePath)dir / "include" / "impeller.h", BindingsFile);
        });

    Target BumpImpeller => _ => _
        .Description("Pin an impeller-builder release (--tag) in impeller.lock.json, regenerate and print the API change")
        .Requires(() => Tag)
        .Executes(async () =>
        {
            var old = File.Exists(ImpellerLock.PathIn(RootDirectory)) ? ImpellerLock.Read(RootDirectory) : null;
            var builder = Builder ?? old?.Builder ?? ImpellerLock.DefaultBuilder;

            using var http = ImpellerFetcher.NewHttpClient();
            var url = $"https://github.com/{builder}/releases/download/{Tag}/manifest.json";
            Log.Information("Reading {Url}", url);
            var manifest = JsonNode.Parse(await http.GetStringAsync(url))!.AsObject();
            if (manifest["tag"]!.GetValue<string>() != Tag)
                throw new Exception($"{url} is the manifest of {manifest["tag"]}, not {Tag}");

            // The revision restarts for a new Flutter version and goes up for another build of the same version.
            var version = manifest["version"]!.GetValue<string>();
            var revision = old == null || old.Version != version ? 0
                : old.Tag == Tag ? old.Revision
                : old.Revision + 1;
            var pinned = ImpellerLock.FromManifest(manifest, builder, revision);
            pinned.RequireComplete();
            pinned = pinned with
            {
                Assets = pinned.Assets
                    .Where(a => ImpellerLock.IsKnownTarget(a.Key))
                    .ToDictionary(a => a.Key, a => a.Value),
            };
            pinned.Write(RootDirectory);

            Log.Information("Pinned {Tag}: Flutter {Version} ({Kind}{Patched}) from {Repository}@{Sha}", pinned.Tag,
                pinned.Version, pinned.Kind, pinned.Patched ? ", patched" : "", pinned.Repository, pinned.Sha[..10]);
            Log.Information("Package version {Old} -> {New}", old?.PackageVersion ?? "(none)", pinned.PackageVersion);
            Log.Information("IMPELLER_VERSION {Old} -> {New}", old?.ImpellerVersion ?? "(none)", pinned.ImpellerVersion);

            var before = File.Exists(BindingsFile) ? PublicMembers(File.ReadAllText(BindingsFile)) : new SortedSet<string>();
            var dir = await ImpellerFetcher.Create(RootDirectory, null).EnsureAsync(SdkDirectory, ImpellerLock.HostRid, ImpellerParts.Main);
            RunInteropGen((AbsolutePath)dir / "include" / "impeller.h", BindingsFile);
            var after = PublicMembers(File.ReadAllText(BindingsFile));

            var removed = before.Except(after).ToArray();
            var added = after.Except(before).ToArray();
            Log.Information("Generated API: {Added} added, {Removed} removed", added.Length, removed.Length);
            foreach (var member in removed)
                Log.Information("  - {Member}", member);
            foreach (var member in added)
                Log.Information("  + {Member}", member);
        });

    Target CompileShaders => _ => _
        .Description("Rebuild samples/Sandbox.Core/Shaders/*.frag.iplr with impellerc from the pinned tools archive")
        .Executes(async () =>
        {
            var rid = ImpellerLock.HostRid;
            var dir = (AbsolutePath)await ImpellerFetcher.Create(RootDirectory, BuilderDir)
                .EnsureAsync(SdkDirectory, rid, ImpellerParts.Main | ImpellerParts.Tools);
            var impellerc = dir / "tools" / "bin" / (OperatingSystem.IsWindows() ? "impellerc.exe" : "impellerc");
            var shaderLib = dir / "tools" / "shader_lib";
            var shaders = RootDirectory / "samples" / "Sandbox.Core" / "Shaders";
            var spirv = RootDirectory / "artifacts" / "shaders";
            spirv.CreateOrCleanDirectory();

            foreach (var source in Directory.GetFiles(shaders, "*.frag").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(source);
                ProcessTasks.StartProcess(impellerc,
                        $"--runtime-stage-gles --runtime-stage-gles3 --runtime-stage-metal --runtime-stage-vulkan " +
                        $"--iplr --input={name} --sl={name}.iplr --spirv={spirv / (name + ".spv")} --include={shaderLib}",
                        workingDirectory: shaders)
                    .AssertZeroExitCode();
                Log.Information("Compiled {Shader}.iplr", name);
            }
        });

    IEnumerable<string> RequestedRids(ImpellerLock lockFile)
    {
        if (All && Rid != null)
            throw new Exception("--all cannot be combined with --rid");
        if (All)
            return lockFile.Assets.Keys;
        return new[] { Rid ?? ImpellerLock.HostRid };
    }

    // Public members of the generated bindings, one line each, for the API change of a bump.
    static SortedSet<string> PublicMembers(string source)
    {
        var members = new SortedSet<string>(StringComparer.Ordinal);
        string type = null;
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.Trim();
            var typeMatch = Regex.Match(line, @"^public\s+(?:(?:sealed|static|partial|unsafe|readonly|ref)\s+)*(?:class|struct|enum|record)\s+(\w+)");
            if (typeMatch.Success)
            {
                type = typeMatch.Groups[1].Value;
                members.Add(line.TrimEnd('{').Trim());
                continue;
            }
            if (line.StartsWith("public ", StringComparison.Ordinal))
                members.Add($"{type}: {Regex.Replace(line, @"\s*(=>.*|\{.*|=[^>].*)$", "").TrimEnd(';')}");
        }
        return members;
    }

    void RunInteropGen(AbsolutePath header, AbsolutePath output)
    {
        Log.Information("Building InteropGen...");

        var interopGenProject = RootDirectory / "src" / "InteropGen" / "InteropGen.csproj";
        DotNetBuild(s => s
            .SetProjectFile(interopGenProject)
            .SetConfiguration(Configuration));

        Log.Information("Running InteropGen on {Header}", header);

        DotNetRun(s => s
            .SetProjectFile(interopGenProject)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .SetApplicationArguments(header, output));

        if (!File.Exists(output))
            throw new Exception("Failed to generate bindings - output file not found");
        Log.Information("Generated {Path} ({Size:F2} KB)", output, new FileInfo(output).Length / 1024.0);
    }
}
