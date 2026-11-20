using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Licenses;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// NuGet packaging of NImpeller for the impeller-builder release pinned in impeller.lock.json.
//
//   ./build.sh Pack
//   ./build.sh Pack --builder-dir ../impeller-builder/release    # unpublished local builds
//
// Everything for a pack is staged under artifacts/staging/<version>/, so external/impeller_sdk and
// Generated/Bindings.g.cs are left alone. Packages land in artifacts/packages/.
partial class Build
{
    const string RepositoryUrl = "https://github.com/AvaloniaUI/NImpeller";
    const string PackageAuthors = "AvaloniaUI OÜ";
    const string PackageCopyright = "Copyright (c) AvaloniaUI OÜ All Rights Reserved";
    const string ManagedPackageId = "NImpeller";
    const string NativeMetaPackageId = "NImpeller.Native";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";

    Target Pack => _ => _
        .Description("Build the NImpeller NuGet packages from the pinned Impeller SDK (or --builder-dir)")
        .Executes(async () =>
        {
            var fetcher = ImpellerFetcher.Create(RootDirectory, BuilderDir);
            var pin = fetcher.Lock;
            if (BuilderDir == null)
                pin.RequireComplete();
            // A local release may hold only some targets; its packages cover those.
            var targets = ImpellerLock.Targets.Where(pin.Assets.ContainsKey).ToArray();
            if (targets.Length < ImpellerLock.Targets.Count())
                Log.Warning("Packing only {Targets}", string.Join(", ", targets));
            LogPin(pin);
            WarnIfWorkingTreeDirty();

            var staging = ArtifactsDirectory / "staging" / pin.PackageVersion;
            staging.CreateOrCleanDirectory();
            var sdk = staging / "sdk";

            foreach (var target in targets)
                await fetcher.EnsureAsync(sdk, target, ImpellerParts.Main | ImpellerParts.Symbols);

            var header = VerifyHeadersIdentical(sdk, targets);

            var bindings = staging / "Bindings.g.cs";
            RunInteropGen(header, bindings);

            VerifyHostNativeVersion(sdk, bindings);

            var build = staging / "build";
            var managedDll = BuildManaged(pin, bindings, build);
            RunUnitTests(sdk, bindings, build);

            PackagesDirectory.CreateDirectory();
            var packages = CreatePackages(pin, staging, sdk, managedDll, targets);

            Log.Information("");
            Log.Information("Packages written to {Directory}:", PackagesDirectory);
            foreach (var package in packages)
                Log.Information("  {Package}", Path.GetFileName(package));
        });

    static void LogPin(ImpellerLock pin)
    {
        Log.Information("impeller-builder: {Builder} {Tag}", pin.Builder, pin.Tag);
        Log.Information("Flutter:          {Version} ({Kind}{Patched})", pin.Version, pin.Kind, pin.Patched ? ", patched" : "");
        Log.Information("Source:           {Repository} {Ref} @ {Sha}", pin.Repository, pin.Ref, pin.Sha);
        Log.Information("Package version:  {Version}", pin.PackageVersion);
    }

    // Returns stdout, or null on a non-zero exit when throwOnError is false.
    static string RunGit(AbsolutePath root, string arguments, bool throwOnError = true)
    {
        var psi = new ProcessStartInfo("git", $"-C \"{root}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)!;
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
            return stdout;
        if (!throwOnError)
            return null;
        throw new Exception($"git {arguments} failed ({process.ExitCode}): {stderrTask.Result.Trim()}");
    }

    void WarnIfWorkingTreeDirty()
    {
        var status = RunGit(RootDirectory, "status --porcelain --untracked-files=no", throwOnError: false);
        if (!string.IsNullOrWhiteSpace(status))
            Log.Warning("NImpeller has uncommitted changes; the packages' repository commit will not describe them");
    }

    // ---------------------------------------------------------------------------------------
    // Verification
    // ---------------------------------------------------------------------------------------

    static AbsolutePath VerifyHeadersIdentical(AbsolutePath sdk, string[] targets)
    {
        var headers = targets
            .Select(t => (Rid: t, Path: sdk / t / "include" / "impeller.h"))
            .ToArray();

        // Compare content, not bytes, in case an archive carries CRLF line endings.
        static string Normalized(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
        var reference = Normalized(headers[0].Path);
        var different = headers.Skip(1)
            .Where(h => Normalized(h.Path) != reference)
            .Select(h => h.Rid)
            .ToArray();

        if (different.Length > 0)
            throw new Exception($"impeller.h differs between {headers[0].Rid} and {string.Join(", ", different)}; bindings cannot be shared");

        Log.Information("impeller.h is identical across all {Count} RIDs and Apple platforms", headers.Length);
        return headers[0].Path;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate uint ImpellerGetVersionFn();

    static void VerifyHostNativeVersion(AbsolutePath sdk, AbsolutePath bindings)
    {
        var match = Regex.Match(File.ReadAllText(bindings), @"ImpellerVersion\s*=\s*(\d+)");
        if (!match.Success)
            throw new Exception($"No ImpellerVersion constant in {bindings}");
        var expected = uint.Parse(match.Groups[1].Value);

        var host = ImpellerLock.HostRid;
        if (ImpellerLock.Rids.All(r => r.Rid != host))
        {
            Log.Warning("Host {Host} has no packaged native library; skipping the native version check", host);
            return;
        }

        var handle = NativeLibrary.Load(sdk / host / "lib" / ImpellerLock.LibraryOf(host));
        try
        {
            var getVersion = Marshal.GetDelegateForFunctionPointer<ImpellerGetVersionFn>(
                NativeLibrary.GetExport(handle, "ImpellerGetVersion"));
            var actual = getVersion();
            if (actual != expected)
                throw new Exception($"{host} native reports Impeller version {FormatImpellerVersion(actual)}, generated bindings expect {FormatImpellerVersion(expected)}");
            Log.Information("{Rid} native matches generated IMPELLER_VERSION {Version}", host, FormatImpellerVersion(actual));
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    // IMPELLER_MAKE_VERSION(variant, major, minor, patch)
    static string FormatImpellerVersion(uint v) =>
        $"{(v >> 22) & 0x7F}.{(v >> 12) & 0x3FF}.{v & 0xFFF} (variant {v >> 29}, raw {v})";

    // ---------------------------------------------------------------------------------------
    // Build and test against the staged SDK + bindings
    // ---------------------------------------------------------------------------------------

    AbsolutePath BuildManaged(ImpellerLock pin, AbsolutePath bindings, AbsolutePath build)
    {
        DotNetBuild(s => s
            .SetProjectFile(RootDirectory / "src" / "NImpeller" / "NImpeller.csproj")
            .SetConfiguration(Configuration.Release)
            .SetProperty("UseArtifactsOutput", true)
            .SetProperty("ArtifactsPath", build)
            .SetProperty("NImpellerBindingsFile", bindings)
            .SetProperty("Version", pin.PackageVersion)
            .SetProperty("DebugType", "embedded")
            .SetProperty("ContinuousIntegrationBuild", true));

        var dll = Directory.GetFiles(build / "bin" / "NImpeller", "NImpeller.dll", SearchOption.AllDirectories)
            .SingleOrDefault() ?? throw new Exception($"NImpeller.dll not found under {build}");
        return (AbsolutePath)dll;
    }

    void RunUnitTests(AbsolutePath sdk, AbsolutePath bindings, AbsolutePath build)
    {
        // Global properties flow to the NImpeller ProjectReference and override Impeller.targets'
        // ImpellerSdkDirectory, so the tests load the staged native library.
        var project = RootDirectory / "tests" / "NImpeller.Tests" / "NImpeller.Tests.csproj";
        DotNet($"test --project {project} -c Release --artifacts-path {build} -p:NImpellerBindingsFile={bindings} -p:ImpellerSdkDirectory={sdk} --filter-namespace NImpeller.Tests.Unit",
            workingDirectory: RootDirectory);
    }

    // ---------------------------------------------------------------------------------------
    // Packages
    // ---------------------------------------------------------------------------------------

    List<string> CreatePackages(ImpellerLock pin, AbsolutePath staging, AbsolutePath sdk, AbsolutePath managedDll,
        string[] targets)
    {
        var rids = ImpellerLock.Rids.Where(r => targets.Contains(r.Rid)).ToArray();
        var version = NuGetVersion.Parse(pin.PackageVersion);
        var exact = new VersionRange(version, true, version, true);
        var nimpellerCommit = RunGit(RootDirectory, "rev-parse HEAD").Trim();
        var origin = DescribeOrigin(pin);
        var packages = new List<string>();

        var notices = staging / "THIRD-PARTY-NOTICES.txt";
        File.WriteAllText(notices, BuildNotices(pin, sdk / rids[0].Rid));

        foreach (var (rid, library) in rids)
        {
            var id = $"{NativeMetaPackageId}.{rid}";
            var toolchain = DescribeToolchain(sdk / rid / "build-info.json");

            // The native library, stripped by impeller-builder.
            {
                var builder = NewBuilder(id, version, nimpellerCommit, "BSD-3-Clause",
                    $"Impeller native library (libimpeller) for {rid}, used by NImpeller. {origin}");
                builder.Tags.AddRange(new[] { "native", rid });
                AddFile(builder, sdk / rid / "lib" / library, $"runtimes/{rid}/native/{library}");
                AddFile(builder, notices, "THIRD-PARTY-NOTICES.txt");
                AddReadme(builder, staging, id, $"""
                    # {id}

                    The Impeller native library (`{library}`) for `{rid}`, built by
                    {BuilderLink(pin)} without debug information. The symbols are in
                    `{id}.Symbols`. You normally reference `{ManagedPackageId}` (all RIDs) or `{ManagedPackageId}`
                    plus only the `{NativeMetaPackageId}.<rid>` packages you ship.

                    {OriginTable(pin)}

                    Toolchain: {toolchain}

                    The binary is licensed under Flutter's BSD-3-Clause license and the licenses of its
                    bundled dependencies; see `THIRD-PARTY-NOTICES.txt`.
                    """);
                packages.Add(Save(builder));
            }

            // Its debug symbols, next to the library when referenced.
            {
                var symbolsId = id + ".Symbols";
                var builder = NewBuilder(symbolsId, version, nimpellerCommit, "BSD-3-Clause",
                    $"Debug symbols of the Impeller native library for {rid}. {origin}");
                builder.Tags.AddRange(new[] { "native", "symbols", rid });
                var symbolsDir = sdk / rid / "symbols" / "lib";
                foreach (var file in Directory.GetFiles(symbolsDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                    AddFile(builder, file, $"runtimes/{rid}/native/{Path.GetRelativePath(symbolsDir, file).Replace('\\', '/')}");
                AddFile(builder, notices, "THIRD-PARTY-NOTICES.txt");
                builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework,
                    new[] { new PackageDependency(id, exact) }));
                AddReadme(builder, staging, symbolsId, $"""
                    # {symbolsId}

                    Debug symbols of `{library}` in `{id}` ({SymbolsKind(rid)}). Reference this package next to
                    `{id}` to place the symbols beside the library, for debuggers and crash symbolization.

                    {OriginTable(pin)}
                    """);
                packages.Add(Save(builder));
            }
        }

        foreach (var (platform, frameworkDirs, platformDescription) in ImpellerLock.ApplePlatforms.Where(p => targets.Contains(p.Platform)))
            packages.AddRange(CreateApplePackages(pin, staging, sdk, notices, version, exact, nimpellerCommit, origin,
                platform, frameworkDirs, platformDescription));

        // Meta package: depends on every RID and Apple platform package.
        {
            var builder = NewBuilder(NativeMetaPackageId, version, nimpellerCommit, "MIT",
                $"Pulls in the Impeller native libraries for every supported RID and Apple platform. {origin}");
            builder.Tags.Add("native");
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework,
                targets.Select(t => new PackageDependency($"{NativeMetaPackageId}.{t}", exact))));
            AddReadme(builder, staging, NativeMetaPackageId, $"""
                # {NativeMetaPackageId}

                Meta package that references the Impeller native library for every supported RID and Apple platform:
                {string.Join(", ", targets.Select(t => $"`{t}`"))}.

                {OriginTable(pin)}
                """);
            packages.Add(Save(builder));
        }

        // Managed bindings.
        {
            var builder = NewBuilder(ManagedPackageId, version, nimpellerCommit, "MIT",
                $".NET bindings for Flutter's Impeller 2D renderer (impeller.h). {origin}");
            AddFile(builder, managedDll, "lib/net10.0/NImpeller.dll");
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.Parse("net10.0"),
                new[] { new PackageDependency(NativeMetaPackageId, exact) }));
            AddReadme(builder, staging, ManagedPackageId, $"""
                # NImpeller

                {OriginTable(pin)}
                """);
            packages.Add(Save(builder));
        }

        return packages;
    }

    /// <summary>
    /// NImpeller.Native.&lt;platform&gt; with impeller.framework per framework directory, as the .NET Apple SDK takes
    /// them from runtimes/&lt;RID root&gt;/native (the Mac Catalyst framework zipped, since NuGet cannot hold its
    /// symbolic links), and its .Symbols package with the dSYMs under symbols/, which the app build does not copy.
    /// </summary>
    List<string> CreateApplePackages(ImpellerLock pin, AbsolutePath staging, AbsolutePath sdk, AbsolutePath notices,
        NuGetVersion version, VersionRange exact, string nimpellerCommit, string origin, string platform,
        string[] frameworkDirs, string platformDescription)
    {
        var packages = new List<string>();
        var id = $"{NativeMetaPackageId}.{platform}";
        var toolchain = DescribeToolchain(sdk / platform / "build-info.json");
        var frameworks = sdk / platform / "frameworks";
        var x64Note = platform == "maccatalyst"
            ? " The x64 slice is built and checked, and has run under Rosetta on Apple silicon, not on an Intel Mac."
            : "";

        {
            var builder = NewBuilder(id, version, nimpellerCommit, "BSD-3-Clause",
                $"Impeller native library (impeller.framework) for {platformDescription}, used by NImpeller. {origin}");
            builder.Tags.AddRange(new[] { "native", platform });
            foreach (var dir in frameworkDirs)
            {
                if (platform == "maccatalyst")
                {
                    AddFile(builder, frameworks / dir / "impeller.framework.zip", $"runtimes/{dir}/native/impeller.framework.zip");
                    continue;
                }
                var framework = frameworks / dir / "impeller.framework";
                if (!framework.DirectoryExists())
                    throw new Exception($"{framework} not found");
                foreach (var file in Directory.GetFiles(framework, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                    AddFile(builder, file, $"runtimes/{dir}/native/impeller.framework/{Path.GetRelativePath(framework, file).Replace('\\', '/')}");
            }
            if (platform == "ios")
            {
                // NuGet's RID graph falls back from maccatalyst-* to ios-*: without an asset of its own, a Mac
                // Catalyst app would take the iOS device framework from this package.
                var placeholder = staging / "placeholder" / "_._";
                placeholder.Parent.CreateDirectory();
                File.WriteAllText(placeholder, "");
                AddFile(builder, placeholder, "runtimes/maccatalyst/native/_._");
            }
            AddFile(builder, notices, "THIRD-PARTY-NOTICES.txt");
            AddReadme(builder, staging, id, $"""
                # {id}

                The Impeller native library as `impeller.framework` for {platformDescription}, built by
                {BuilderLink(pin)} without debug information. The .NET Apple SDK embeds the framework that matches
                the app's RuntimeIdentifier ({string.Join(", ", frameworkDirs.Select(d => $"`{d}`"))}), and NImpeller
                loads it from `@rpath/impeller.framework/impeller`. The symbols are in `{id}.Symbols`.{x64Note}

                {OriginTable(pin)}

                Toolchain: {toolchain}

                The binary is licensed under Flutter's BSD-3-Clause license and the licenses of its
                bundled dependencies; see `THIRD-PARTY-NOTICES.txt`.
                """);
            packages.Add(Save(builder));
        }

        {
            var symbolsId = id + ".Symbols";
            var builder = NewBuilder(symbolsId, version, nimpellerCommit, "BSD-3-Clause",
                $"Debug symbols (dSYM) of the Impeller native library for {platformDescription}. {origin}");
            builder.Tags.AddRange(new[] { "native", "symbols", platform });
            var symbols = sdk / platform / "symbols" / "frameworks";
            foreach (var file in Directory.GetFiles(symbols, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                AddFile(builder, file, $"symbols/{Path.GetRelativePath(symbols, file).Replace('\\', '/')}");
            AddFile(builder, notices, "THIRD-PARTY-NOTICES.txt");
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework,
                new[] { new PackageDependency(id, exact) }));
            AddReadme(builder, staging, symbolsId, $"""
                # {symbolsId}

                Debug symbols of `impeller.framework` in `{id}`: `symbols/<RID root>/impeller.framework.dSYM`
                for {string.Join(", ", frameworkDirs.Select(d => $"`{d}`"))}. They are not copied into apps; crash
                reporting tools and `atos` find a dSYM by the UUID of the framework binary.

                {OriginTable(pin)}
                """);
            packages.Add(Save(builder));
        }

        return packages;
    }

    static string SymbolsKind(string rid) =>
        rid.StartsWith("win-") ? "`impeller.dll.pdb`"
        : rid.StartsWith("osx-") ? "`libimpeller.dylib.dSYM`"
        : "`libimpeller.so.debug`, zlib-compressed DWARF linked with `.gnu_debuglink`";

    static string DescribeOrigin(ImpellerLock pin) =>
        $"Built from Flutter {pin.Version}{(pin.Patched ? " with Avalonia patches" : "")} ({pin.Repository}@{pin.Sha[..10]}) by impeller-builder {pin.Tag}.";

    static string OriginTable(ImpellerLock pin) => $"""
        | | |
        |---|---|
        | Flutter version | {pin.Version} ({pin.Kind}) |
        | Source | `{pin.Repository}` `{pin.Ref}`{(pin.Patched ? " (Avalonia patches)" : "")} |
        | Commit | [`{pin.Sha}`](https://github.com/{pin.Repository}/commit/{pin.Sha}) |
        | impeller-builder | {ReleaseLink(pin)} |
        | `IMPELLER_VERSION` | {pin.ImpellerVersion} |
        """;

    // Local --builder-dir packs name a folder, not a GitHub repository.
    static bool IsLocal(ImpellerLock pin) => pin.Builder.StartsWith("local:", StringComparison.Ordinal);

    static string BuilderLink(ImpellerLock pin) =>
        IsLocal(pin) ? "impeller-builder (local build)" : $"[{pin.Builder}](https://github.com/{pin.Builder})";

    static string ReleaseLink(ImpellerLock pin) =>
        IsLocal(pin) ? $"{pin.Tag} (local build)" : $"[{pin.Tag}](https://github.com/{pin.Builder}/releases/tag/{pin.Tag})";

    static string DescribeToolchain(AbsolutePath buildInfo)
    {
        if (!File.Exists(buildInfo))
            return "unknown";
        var toolchain = JsonNode.Parse(File.ReadAllText(buildInfo))!["toolchain"]!.AsObject();
        return string.Join(", ", toolchain
            .Where(p => p.Key != "visualStudioPath" && p.Value != null)
            .Select(p => $"{p.Key} {(p.Value is JsonArray a ? string.Join(" ", a) : p.Value.ToString())}"));
    }

    static string BuildNotices(ImpellerLock pin, AbsolutePath ridDirectory)
    {
        var licenses = ridDirectory / "licenses";
        string Read(string relative)
        {
            var path = licenses / relative;
            return File.Exists(path) ? File.ReadAllText(path).Trim() : throw new Exception($"{path} not found");
        }

        var sb = new StringBuilder();
        sb.AppendLine("The native library in this package is Flutter's Impeller SDK, built by impeller-builder.");
        sb.AppendLine(DescribeOrigin(pin));
        sb.AppendLine();
        if (File.Exists(licenses / "LICENSE.sdk.md"))
        {
            sb.AppendLine("===== Impeller SDK LICENSE.sdk.md =====");
            sb.AppendLine(Read("LICENSE.sdk.md"));
            sb.AppendLine();
        }
        sb.AppendLine("===== Flutter LICENSE =====");
        sb.AppendLine(Read("flutter/LICENSE"));
        sb.AppendLine();
        sb.AppendLine("===== Flutter engine and bundled third-party licenses (sky_engine/LICENSE) =====");
        sb.AppendLine(Read("sky_engine/LICENSE"));
        return sb.ToString();
    }

    static PackageBuilder NewBuilder(string id, NuGetVersion version, string nimpellerCommit, string license, string description)
    {
        var builder = new PackageBuilder
        {
            Id = id,
            Version = version,
            Description = description,
            Copyright = PackageCopyright,
            ProjectUrl = new Uri(RepositoryUrl),
            Repository = new RepositoryMetadata("git", RepositoryUrl + ".git", null, nimpellerCommit),
            LicenseMetadata = new LicenseMetadata(LicenseType.Expression, license,
                NuGetLicenseExpression.Parse(license), null, LicenseMetadata.EmptyVersion),
        };
        builder.Authors.Add(PackageAuthors);
        builder.Tags.AddRange(new[] { "impeller", "flutter", "graphics", "2d", "rendering" });
        return builder;
    }

    static void AddFile(PackageBuilder builder, string source, string target) =>
        builder.Files.Add(new PhysicalPackageFile { SourcePath = source, TargetPath = target });

    static void AddReadme(PackageBuilder builder, AbsolutePath staging, string id, string content)
    {
        var path = staging / "readme" / id / "README.md";
        path.Parent.CreateDirectory();
        File.WriteAllText(path, content);
        AddFile(builder, path, "README.md");
        builder.Readme = "README.md";
    }

    string Save(PackageBuilder builder)
    {
        var path = PackagesDirectory / $"{builder.Id}.{builder.Version.ToNormalizedString()}.nupkg";
        using (var stream = File.Create(path))
            builder.Save(stream);
        return path;
    }
}
