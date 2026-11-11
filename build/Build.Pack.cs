using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

// NuGet packaging of NImpeller for a given Flutter release tag or framework commit.
//
//   ./build.sh Pack --flutter-ref 3.47.9
//   ./build.sh Pack --flutter-ref 3.50.0-0.4.pre
//   ./build.sh Pack --flutter-ref a55d35a8b73          # master commit -> X.Y.0-dev.<date>.<sha>
//
// The Flutter checkout is only read (plus `git fetch <remote> --tags`). Everything for a pack is
// staged under artifacts/staging/<version>/, so external/impeller_sdk and Generated/Bindings.g.cs
// are left alone. Packages land in artifacts/packages/.
partial class Build
{
    [Parameter("Flutter tag or commit to package (default: latest stable tag)")]
    readonly string FlutterRef;

    [Parameter("Path to a flutter/flutter checkout (default: ../flutter)")]
    readonly AbsolutePath FlutterRoot;

    [Parameter("Git remote of the Flutter checkout to fetch tags from (default: upstream)")]
    readonly string FlutterRemote = "upstream";

    [Parameter("Binding revision on top of the same Flutter release (stable: 4th version part, beta: last prerelease part)")]
    readonly int Revision;

    [Parameter("Do not fetch tags from the Flutter remote")]
    readonly bool NoFetch;

    const string RepositoryUrl = "https://github.com/AvaloniaUI/NImpeller";
    const string PackageAuthors = "AvaloniaUI OÜ";
    const string PackageCopyright = "Copyright (c) AvaloniaUI OÜ All Rights Reserved";
    const string ManagedPackageId = "NImpeller";
    const string NativeMetaPackageId = "NImpeller.Native";

    static readonly (string Platform, string Rid, string Library)[] PackagePlatforms =
    {
        ("linux-x64", "linux-x64", "libimpeller.so"),
        ("linux-arm64", "linux-arm64", "libimpeller.so"),
        ("darwin-x64", "osx-x64", "libimpeller.dylib"),
        ("darwin-arm64", "osx-arm64", "libimpeller.dylib"),
        ("windows-x64", "win-x64", "impeller.dll"),
        ("windows-arm64", "win-arm64", "impeller.dll"),
    };

    static readonly Regex StableTag = new(@"^(\d+)\.(\d+)\.(\d+)$");
    static readonly Regex BetaTag = new(@"^(\d+)\.(\d+)\.0-(\d+)\.(\d+)\.pre$");
    static readonly Regex CandidateBranch = new(@"^flutter-(\d+)\.(\d+)-candidate\.(\d+)$");

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";

    record FlutterRelease(
        string Ref,
        string Commit,
        string Kind,            // stable | beta | dev
        string FlutterVersion,  // tag name, or null for an untagged commit
        string EngineHash,
        string PackageVersion);

    Target ResolveFlutterVersion => _ => _
        .Description("Show the Flutter commit, engine hash and package version that Pack would use")
        .Executes(() => LogRelease(ResolveFlutterRelease()));

    Target Pack => _ => _
        .Description("Build NImpeller NuGet packages for a Flutter tag or commit (--flutter-ref)")
        .Executes(async () =>
        {
            var release = ResolveFlutterRelease();
            LogRelease(release);
            WarnIfWorkingTreeDirty();

            var staging = ArtifactsDirectory / "staging" / release.PackageVersion;
            staging.CreateOrCleanDirectory();
            var sdk = staging / "sdk";

            await Task.WhenAll(PackagePlatforms.Select(p => DownloadPlatformAsync(release.EngineHash, p.Platform, sdk)));
            foreach (var p in PackagePlatforms)
            {
                var lib = sdk / p.Platform / "lib" / p.Library;
                if (!File.Exists(lib))
                    throw new Exception($"Expected native library missing from the {p.Platform} SDK: {lib}");
            }

            var header = VerifyHeadersIdentical(sdk);

            var bindings = staging / "Bindings.g.cs";
            RunInteropGen(header, bindings);

            VerifyHostNativeVersion(sdk, bindings);

            var build = staging / "build";
            var managedDll = BuildManaged(release, bindings, build);
            RunUnitTests(sdk, bindings, build);

            PackagesDirectory.CreateDirectory();
            var packages = CreatePackages(release, staging, sdk, managedDll);

            Log.Information("");
            Log.Information("Packages written to {Directory}:", PackagesDirectory);
            foreach (var package in packages)
                Log.Information("  {Package}", Path.GetFileName(package));
        });

    // ---------------------------------------------------------------------------------------
    // Flutter ref -> commit, engine hash, package version
    // ---------------------------------------------------------------------------------------

    AbsolutePath FlutterCheckout
    {
        get
        {
            var root = FlutterRoot ?? RootDirectory.Parent / "flutter";
            if (!Directory.Exists(root / ".git") && !File.Exists(root / ".git"))
                throw new Exception($"No Flutter git checkout at {root}; pass --flutter-root");
            return root;
        }
    }

    FlutterRelease ResolveFlutterRelease()
    {
        var root = FlutterCheckout;

        if (!NoFetch)
        {
            Log.Information("Fetching tags from {Remote} in {Root}...", FlutterRemote, root);
            if (RunGit(root, $"fetch {FlutterRemote} --tags --quiet", throwOnError: false) == null)
                Log.Warning("git fetch {Remote} failed; continuing with the tags already in the clone", FlutterRemote);
        }

        var refName = FlutterRef ?? LatestStableTag(root);
        var commit = RunGit(root, $"rev-parse --verify --quiet {refName}^{{commit}}", throwOnError: false)?.Trim();
        if (string.IsNullOrEmpty(commit))
            throw new Exception($"Unknown Flutter ref '{refName}' in {root}");

        // A ref that is (or points at) a release tag is packaged as that release.
        var tag = IsReleaseTag(refName)
            ? refName
            : RunGit(root, $"tag --points-at {commit}")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsReleaseTag)
                .OrderBy(t => StableTag.IsMatch(t) ? 0 : 1)
                .FirstOrDefault();

        var engineVersionFile = GitShow(root, commit, "bin/internal/engine.version")?.Trim();
        var engineHash = string.IsNullOrEmpty(engineVersionFile) ? ContentAwareHash(root, commit) : engineVersionFile;

        if (tag != null)
        {
            if (string.IsNullOrEmpty(engineVersionFile))
                Log.Warning("Tag {Tag} has no bin/internal/engine.version; using the content-aware hash", tag);

            var stable = StableTag.Match(tag);
            if (stable.Success)
            {
                var version = Revision > 0 ? $"{tag}.{Revision}" : tag;
                return new FlutterRelease(refName, commit, "stable", tag, engineHash, version);
            }

            var beta = BetaTag.Match(tag);
            var betaVersion = $"{beta.Groups[1]}.{beta.Groups[2]}.0-pre.{beta.Groups[3]}.{beta.Groups[4]}.{Revision}";
            return new FlutterRelease(refName, commit, "beta", tag, engineHash, betaVersion);
        }

        if (Revision > 0)
            Log.Warning("--revision is ignored for untagged commits; dev versions are unique per commit");

        var (major, minor) = DevBaseVersion(root, commit);
        var date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(RunGit(root, $"show -s --format=%ct {commit}").Trim()))
            .UtcDateTime.ToString("yyyyMMdd");
        var shortSha = commit[..7];
        // SemVer forbids leading zeros in numeric identifiers, so never let the sha be all digits.
        if (shortSha.All(char.IsAsciiDigit))
            shortSha = "g" + shortSha;

        return new FlutterRelease(refName, commit, "dev", null, engineHash,
            $"{major}.{minor}.0-dev.{date}.{shortSha}");
    }

    static bool IsReleaseTag(string name) => StableTag.IsMatch(name) || BetaTag.IsMatch(name);

    static string LatestStableTag(AbsolutePath root)
    {
        var tag = RunGit(root, "tag --list")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => StableTag.IsMatch(t))
            .OrderByDescending(Version.Parse)
            .FirstOrDefault();
        return tag ?? throw new Exception($"No stable release tags in {root}; fetch them or pass --flutter-ref");
    }

    // Base X.Y for an untagged commit. On a release branch it is the branch's version; on master it
    // is the minor after the newest release branch cut that the commit already contains.
    static (int Major, int Minor) DevBaseVersion(AbsolutePath root, string commit)
    {
        var candidate = GitShow(root, commit, "bin/internal/release-candidate-branch.version")?.Trim();
        var candidateMatch = candidate != null ? CandidateBranch.Match(candidate) : Match.Empty;
        if (candidateMatch.Success)
            return (int.Parse(candidateMatch.Groups[1].Value), int.Parse(candidateMatch.Groups[2].Value));

        var tagsByMinor = RunGit(root, "tag --list")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => (Tag: t, Match: StableTag.IsMatch(t) ? StableTag.Match(t) : BetaTag.Match(t)))
            .Where(x => x.Match.Success)
            .GroupBy(x => (Major: int.Parse(x.Match.Groups[1].Value), Minor: int.Parse(x.Match.Groups[2].Value)))
            .OrderByDescending(g => g.Key.Major).ThenByDescending(g => g.Key.Minor)
            .Take(12);

        foreach (var group in tagsByMinor)
        {
            // The commit that created release-candidate-branch.version initialises the branch; its
            // parent is the master commit the branch was cut from.
            var init = RunGit(root,
                $"log -1 --diff-filter=A --format=%H {group.First().Tag} -- bin/internal/release-candidate-branch.version").Trim();
            if (string.IsNullOrEmpty(init))
                break;

            if (RunGit(root, $"merge-base --is-ancestor {init}^ {commit}", throwOnError: false) != null)
                return (group.Key.Major, group.Key.Minor + 1);
        }

        throw new Exception($"Could not determine a Flutter version for {commit}: it predates the release branches known to this clone. Pass a release tag instead.");
    }

    // Mirrors bin/internal/content_aware_hash.sh: git ls-tree <ref> -- <tracked> | git hash-object --stdin
    static string ContentAwareHash(AbsolutePath root, string commit)
    {
        var tree = RunGit(root, $"ls-tree {commit} -- DEPS engine bin/internal/release-candidate-branch.version");
        return RunGit(root, "hash-object --stdin", stdin: tree).Trim();
    }

    static string GitShow(AbsolutePath root, string commit, string path) =>
        RunGit(root, $"show {commit}:{path}", throwOnError: false);

    // Returns stdout, or null on a non-zero exit when throwOnError is false.
    static string RunGit(AbsolutePath root, string arguments, bool throwOnError = true, string stdin = null)
    {
        // Same as content_aware_hash.sh: Apple Git can misbehave with multi-pack indexes.
        var psi = new ProcessStartInfo("git", $"-c core.multiPackIndex=false -C \"{root}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin != null,
            UseShellExecute = false,
        };
        psi.Environment.Remove("GIT_DIR");
        psi.Environment.Remove("GIT_INDEX_FILE");
        psi.Environment.Remove("GIT_WORK_TREE");

        using var process = Process.Start(psi)!;
        if (stdin != null)
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
            return stdout;
        if (!throwOnError)
            return null;
        throw new Exception($"git {arguments} failed ({process.ExitCode}): {stderrTask.Result.Trim()}");
    }

    static void LogRelease(FlutterRelease release)
    {
        Log.Information("Flutter ref:      {Ref}", release.Ref);
        Log.Information("Flutter commit:   {Commit}", release.Commit);
        Log.Information("Flutter version:  {Version} ({Kind})", release.FlutterVersion ?? "untagged", release.Kind);
        Log.Information("Engine hash:      {Engine}", release.EngineHash);
        Log.Information("Package version:  {Version}", release.PackageVersion);
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

    static AbsolutePath VerifyHeadersIdentical(AbsolutePath sdk)
    {
        var headers = PackagePlatforms
            .Select(p => (p.Platform, Path: sdk / p.Platform / "include" / "impeller.h"))
            .ToArray();

        // The Windows SDKs ship the header with CRLF line endings; compare content, not bytes.
        static string Normalized(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
        var reference = Normalized(headers[0].Path);
        var different = headers.Skip(1)
            .Where(h => Normalized(h.Path) != reference)
            .Select(h => h.Platform)
            .ToArray();

        if (different.Length > 0)
            throw new Exception($"impeller.h differs between {headers[0].Platform} and {string.Join(", ", different)}; bindings cannot be shared");

        Log.Information("impeller.h is identical across all {Count} platforms", headers.Length);
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

        var host = PackagePlatforms.FirstOrDefault(p => p.Platform == HostPlatform);
        if (host.Platform == null)
        {
            Log.Warning("Host {Host} has no packaged native library; skipping the native version check", HostPlatform);
            return;
        }

        var handle = NativeLibrary.Load(sdk / host.Platform / "lib" / host.Library);
        try
        {
            var getVersion = Marshal.GetDelegateForFunctionPointer<ImpellerGetVersionFn>(
                NativeLibrary.GetExport(handle, "ImpellerGetVersion"));
            var actual = getVersion();
            if (actual != expected)
                throw new Exception($"{host.Platform} native reports Impeller version {FormatImpellerVersion(actual)}, generated bindings expect {FormatImpellerVersion(expected)}");
            Log.Information("{Platform} native matches generated IMPELLER_VERSION {Version}", host.Platform, FormatImpellerVersion(actual));
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

    AbsolutePath BuildManaged(FlutterRelease release, AbsolutePath bindings, AbsolutePath build)
    {
        DotNetBuild(s => s
            .SetProjectFile(RootDirectory / "src" / "NImpeller" / "NImpeller.csproj")
            .SetConfiguration(Configuration.Release)
            .SetProperty("UseArtifactsOutput", true)
            .SetProperty("ArtifactsPath", build)
            .SetProperty("NImpellerBindingsFile", bindings)
            .SetProperty("Version", release.PackageVersion)
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

    List<string> CreatePackages(FlutterRelease release, AbsolutePath staging, AbsolutePath sdk, AbsolutePath managedDll)
    {
        var version = NuGetVersion.Parse(release.PackageVersion);
        var exact = new VersionRange(version, true, version, true);
        var nimpellerCommit = RunGit(RootDirectory, "rev-parse HEAD").Trim();
        var origin = DescribeOrigin(release);
        var packages = new List<string>();

        var notices = staging / "THIRD-PARTY-NOTICES.txt";
        File.WriteAllText(notices, BuildNotices(release, sdk));

        // Per-RID native packages.
        foreach (var p in PackagePlatforms)
        {
            var id = $"{NativeMetaPackageId}.{p.Rid}";
            var builder = NewBuilder(id, version, nimpellerCommit, "BSD-3-Clause",
                $"Impeller native library (libimpeller) for {p.Rid}, used by NImpeller. {origin}");
            builder.Tags.AddRange(new[] { "native", p.Rid });
            AddFile(builder, sdk / p.Platform / "lib" / p.Library, $"runtimes/{p.Rid}/native/{p.Library}");
            AddFile(builder, notices, "THIRD-PARTY-NOTICES.txt");
            AddReadme(builder, staging, id, $"""
                # {id}

                The Impeller native library (`{p.Library}`) for `{p.Rid}`, from Flutter's prebuilt
                `impeller_sdk.zip`. You normally reference `{ManagedPackageId}` (all RIDs) or
                `{ManagedPackageId}` plus only the `{NativeMetaPackageId}.<rid>` packages you ship.

                {OriginTable(release)}

                The binary is licensed under Flutter's BSD-3-Clause license and the licenses of its
                bundled dependencies; see `THIRD-PARTY-NOTICES.txt`.
                """);
            packages.Add(Save(builder));
        }

        // Meta package: depends on every RID package.
        {
            var builder = NewBuilder(NativeMetaPackageId, version, nimpellerCommit, "MIT",
                $"Pulls in the Impeller native libraries for every supported RID. {origin}");
            builder.Tags.Add("native");
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework,
                PackagePlatforms.Select(p => new PackageDependency($"{NativeMetaPackageId}.{p.Rid}", exact))));
            AddReadme(builder, staging, NativeMetaPackageId, $"""
                # {NativeMetaPackageId}

                Meta package that references the Impeller native library for every supported RID:
                {string.Join(", ", PackagePlatforms.Select(p => $"`{p.Rid}`"))}.

                {OriginTable(release)}
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

                {OriginTable(release)}
                """);
            packages.Add(Save(builder));
        }

        return packages;
    }

    static string DescribeOrigin(FlutterRelease release) => release.FlutterVersion != null
        ? $"Built from Flutter {release.FlutterVersion} (engine {release.EngineHash})."
        : $"Built from Flutter commit {release.Commit} (engine {release.EngineHash}).";

    static string OriginTable(FlutterRelease release) => $"""
        | | |
        |---|---|
        | Flutter version | {release.FlutterVersion ?? "untagged (" + release.Kind + ")"} |
        | Flutter commit | `{release.Commit}` |
        | Engine hash | `{release.EngineHash}` |
        """;

    string BuildNotices(FlutterRelease release, AbsolutePath sdk)
    {
        var root = FlutterCheckout;
        var flutterLicense = GitShow(root, release.Commit, "LICENSE")
            ?? throw new Exception($"No LICENSE in Flutter at {release.Commit}");
        var engineLicenses = GitShow(root, release.Commit, "engine/src/flutter/sky/packages/sky_engine/LICENSE")
            ?? throw new Exception($"No engine/src/flutter/sky/packages/sky_engine/LICENSE in Flutter at {release.Commit}");
        var sdkNote = File.ReadAllText(sdk / PackagePlatforms[0].Platform / "LICENSE.sdk.md");

        var sb = new StringBuilder();
        sb.AppendLine("The native library in this package is Flutter's Impeller SDK (impeller_sdk.zip).");
        sb.AppendLine(DescribeOrigin(release));
        sb.AppendLine();
        sb.AppendLine("===== impeller_sdk LICENSE.sdk.md =====");
        sb.AppendLine(sdkNote.Trim());
        sb.AppendLine();
        sb.AppendLine("===== Flutter LICENSE =====");
        sb.AppendLine(flutterLicense.Trim());
        sb.AppendLine();
        sb.AppendLine("===== Flutter engine and bundled third-party licenses (sky_engine/LICENSE) =====");
        sb.AppendLine(engineLicenses.Trim());
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
