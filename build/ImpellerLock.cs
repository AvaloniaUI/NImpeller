using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

// impeller.lock.json: the pinned AvaloniaUI/impeller-builder release (a copy of its manifest.json) plus the binding
// revision. The archives are downloaded from the GitHub release, or read from a local impeller-builder release/
// folder (--builder-dir), checked against their SHA-256 and unpacked to <sdk root>/<rid>/.
public sealed record ImpellerAsset(string Name, long Size, string Sha256);

public sealed record ImpellerRidAssets(ImpellerAsset Main, ImpellerAsset Symbols, ImpellerAsset Tools);

public sealed record ImpellerLock(
    string Builder,
    string Tag,
    string Repository,
    string Ref,
    string Sha,
    string Version,
    string Kind,
    bool Patched,
    string ImpellerVersion,
    int Revision,
    IReadOnlyDictionary<string, ImpellerRidAssets> Assets)
{
    public const string FileName = "impeller.lock.json";
    public const string DefaultBuilder = "AvaloniaUI/impeller-builder";

    // The RIDs NImpeller ships, and the library file of each.
    public static readonly (string Rid, string Library)[] Rids =
    {
        ("linux-x64", "libimpeller.so"),
        ("linux-arm64", "libimpeller.so"),
        ("osx-x64", "libimpeller.dylib"),
        ("osx-arm64", "libimpeller.dylib"),
        ("win-x64", "impeller.dll"),
        ("win-arm64", "impeller.dll"),
        ("android-arm64", "libimpeller.so"),
        ("android-arm", "libimpeller.so"),
        ("android-x64", "libimpeller.so"),
    };

    // The Apple mobile platforms: one archive each, with impeller.framework per framework directory (a .NET RID root).
    // The Mac Catalyst framework is versioned (symbolic links), so NuGet gets impeller.framework.zip.
    public static readonly (string Platform, string[] FrameworkDirs, string Description)[] ApplePlatforms =
    {
        ("ios", new[] { "ios", "iossimulator" }, "iOS 15.0 or newer, devices (arm64) and simulators (arm64, x64)"),
        ("tvos", new[] { "tvos", "tvossimulator" }, "tvOS 16.0 or newer, devices (arm64) and simulators (arm64, x64)"),
        ("maccatalyst", new[] { "maccatalyst" }, "Mac Catalyst 15.0 or newer, arm64 and x64"),
    };

    /// <summary>Every target of a release: the RIDs, then the Apple mobile platforms.</summary>
    public static IEnumerable<string> Targets => Rids.Select(r => r.Rid).Concat(ApplePlatforms.Select(p => p.Platform));

    public static bool IsKnownTarget(string target) => Targets.Contains(target);

    /// <summary>The RIDs with impellerc (tools archives).</summary>
    public static bool IsDesktop(string rid) =>
        rid.StartsWith("linux-", StringComparison.Ordinal) || rid.StartsWith("osx-", StringComparison.Ordinal)
        || rid.StartsWith("win-", StringComparison.Ordinal);

    public static string LibraryOf(string rid) => Rids.First(r => r.Rid == rid).Library;

    static readonly Regex StableVersion = new(@"^(\d+)\.(\d+)\.(\d+)$");
    static readonly Regex BetaVersion = new(@"^(\d+)\.(\d+)\.0-(\d+)\.(\d+)\.pre$");

    /// <summary>
    /// The NuGet version: stable 3.47.9 (revision 0) or 3.47.9.N, beta 3.50.0-0.4.pre as 3.50.0-pre.0.4.N. A local
    /// build is a prerelease of the next revision, such as 3.50.0.4-local.mytag.
    /// </summary>
    public string PackageVersion => Builder.StartsWith("local:", StringComparison.Ordinal)
        ? LocalVersionOf(PackageVersionOf(Version, Kind, Revision + 1), Tag)
        : PackageVersionOf(Version, Kind, Revision);

    static string LocalVersionOf(string version, string tag)
    {
        var label = "local." + Regex.Replace(tag, "[^0-9A-Za-z-]", "-");
        return version + (version.Contains('-') ? "." : "-") + label;
    }

    public static string PackageVersionOf(string version, string kind, int revision)
    {
        if (kind == "stable" && StableVersion.IsMatch(version))
            return revision > 0 ? $"{version}.{revision}" : version;
        var beta = BetaVersion.Match(version);
        if (kind == "beta" && beta.Success)
            return $"{beta.Groups[1]}.{beta.Groups[2]}.0-pre.{beta.Groups[3]}.{beta.Groups[4]}.{revision}";
        throw new Exception($"Flutter version {version} is not a {kind} release");
    }

    public static string HostRid
    {
        get
        {
            var os = OperatingSystem.IsWindows() ? "win"
                : OperatingSystem.IsMacOS() ? "osx"
                : OperatingSystem.IsLinux() ? "linux"
                : throw new Exception($"No Impeller build for {RuntimeInformation.OSDescription}");
            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                var a => throw new Exception($"No Impeller build for {a}"),
            };
            return $"{os}-{arch}";
        }
    }

    public static string PathIn(string root) => Path.Combine(root, FileName);

    public static ImpellerLock Read(string root)
    {
        var path = PathIn(root);
        if (!File.Exists(path))
            throw new Exception($"{path} not found. Pin a release with ./build.sh BumpImpeller --tag <tag>");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        return FromManifest(node, node["builder"]!.GetValue<string>(), node["revision"]!.GetValue<int>());
    }

    /// <summary>A lock from an impeller-builder manifest.json (the lock is a copy of it).</summary>
    public static ImpellerLock FromManifest(JsonObject manifest, string builder, int revision)
    {
        var assets = new SortedDictionary<string, ImpellerRidAssets>(StringComparer.Ordinal);
        foreach (var (rid, value) in manifest["assets"]!.AsObject())
        {
            var o = value!.AsObject();
            assets[rid] = new ImpellerRidAssets(Asset(o), Asset(o["symbols"]?.AsObject()), Asset(o["tools"]?.AsObject()));
        }

        return new ImpellerLock(
            builder,
            manifest["tag"]!.GetValue<string>(),
            manifest["repository"]!.GetValue<string>(),
            manifest["ref"]!.GetValue<string>(),
            manifest["sha"]!.GetValue<string>(),
            manifest["version"]!.GetValue<string>(),
            manifest["kind"]!.GetValue<string>(),
            manifest["patched"]!.GetValue<bool>(),
            manifest["impellerVersion"]!.GetValue<string>(),
            revision,
            assets);

        static ImpellerAsset Asset(JsonObject o) => o == null
            ? null
            : new ImpellerAsset(o["name"]!.GetValue<string>(), o["size"]!.GetValue<long>(), o["sha256"]!.GetValue<string>());
    }

    public void Write(string root)
    {
        static JsonObject Asset(ImpellerAsset a) => new()
        {
            ["name"] = a.Name,
            ["size"] = a.Size,
            ["sha256"] = a.Sha256,
        };

        var assets = new JsonObject();
        foreach (var (rid, a) in Assets)
        {
            var o = Asset(a.Main);
            if (a.Symbols != null)
                o["symbols"] = Asset(a.Symbols);
            if (a.Tools != null)
                o["tools"] = Asset(a.Tools);
            assets[rid] = o;
        }

        var node = new JsonObject
        {
            ["builder"] = Builder,
            ["tag"] = Tag,
            ["repository"] = Repository,
            ["ref"] = Ref,
            ["sha"] = Sha,
            ["version"] = Version,
            ["kind"] = Kind,
            ["patched"] = Patched,
            ["impellerVersion"] = ImpellerVersion,
            ["revision"] = Revision,
            ["assets"] = assets,
        };
        File.WriteAllText(PathIn(root), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    /// <summary>
    /// Fails unless every shipped target (RID or Apple platform) has a library and symbols archive, and every
    /// desktop RID tools.
    /// </summary>
    public void RequireComplete()
    {
        var missing = Targets
            .Where(t => !Assets.TryGetValue(t, out var a) || a.Symbols == null || (IsDesktop(t) && a.Tools == null))
            .ToArray();
        if (missing.Length > 0)
            throw new Exception($"Release {Tag} lacks archives for {string.Join(", ", missing)}");
    }
}

[Flags]
public enum ImpellerParts
{
    Main = 1,
    Symbols = 2,
    Tools = 4,
}

/// <summary>
/// Gets the archives of a lock (from its GitHub release) or of a local impeller-builder release/ folder, and unpacks
/// them to &lt;sdk root&gt;/&lt;rid&gt;/ (main), &lt;rid&gt;/symbols/ and &lt;rid&gt;/tools/. &lt;rid&gt;/.tag
/// records the source, so a changed lock replaces the directory.
/// </summary>
public sealed class ImpellerFetcher
{
    public ImpellerLock Lock { get; }
    readonly string _builderDir;
    readonly string _cacheDir;

    ImpellerFetcher(ImpellerLock lockFile, string builderDir, string cacheDir)
    {
        Lock = lockFile;
        _builderDir = builderDir;
        _cacheDir = cacheDir;
    }

    /// <summary>The pinned release, or the archives in builderDir (checked against its manifest.json and SHA256SUMS).</summary>
    public static ImpellerFetcher Create(string root, string builderDir)
    {
        var cache = Path.Combine(root, "external", "impeller_sdk", ".downloads");
        if (string.IsNullOrEmpty(builderDir))
            return new ImpellerFetcher(ImpellerLock.Read(root), null, cache);

        var manifestPath = Path.Combine(builderDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new Exception($"{manifestPath} not found. Run scripts/make-release.py (with --partial for a subset of the RIDs)");
        var revision = File.Exists(ImpellerLock.PathIn(root)) ? ImpellerLock.Read(root).Revision : 0;
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var lockFile = ImpellerLock.FromManifest(manifest, "local:" + Path.GetFullPath(builderDir), revision);

        var sumsPath = Path.Combine(builderDir, "SHA256SUMS");
        var sums = File.ReadAllLines(sumsPath)
            .Where(l => l.Length > 66)
            .ToDictionary(l => l[66..].Trim(), l => l[..64], StringComparer.Ordinal);
        foreach (var asset in lockFile.Assets.Values.SelectMany(a => new[] { a.Main, a.Symbols, a.Tools }).Where(a => a != null))
        {
            if (!sums.TryGetValue(asset.Name, out var sum) || sum != asset.Sha256)
                throw new Exception($"{asset.Name}: manifest.json and SHA256SUMS disagree in {builderDir}");
        }

        return new ImpellerFetcher(lockFile, builderDir, cache);
    }

    string Stamp => _builderDir == null ? Lock.Tag : "local:" + Lock.Tag;

    public async Task<string> EnsureAsync(string sdkRoot, string rid, ImpellerParts parts)
    {
        if (!Lock.Assets.TryGetValue(rid, out var assets))
            throw new Exception($"{(_builderDir ?? Lock.Tag)} has no {rid} archive");

        var dir = Path.Combine(sdkRoot, rid);
        var stampFile = Path.Combine(dir, ".tag");
        if (Directory.Exists(dir) && (!File.Exists(stampFile) || File.ReadAllText(stampFile).Trim() != Stamp))
        {
            Log.Information("{Rid}: replacing {Old} with {New}", rid,
                File.Exists(stampFile) ? File.ReadAllText(stampFile).Trim() : "an unstamped SDK", Stamp);
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        if (!File.Exists(Path.Combine(dir, "include", "impeller.h")))
            await UnpackAsync(assets.Main, dir);
        if (parts.HasFlag(ImpellerParts.Symbols) && !Directory.Exists(Path.Combine(dir, "symbols")))
            await UnpackAsync(assets.Symbols ?? throw new Exception($"No {rid} symbols archive"), Path.Combine(dir, "symbols"));
        if (parts.HasFlag(ImpellerParts.Tools) && !Directory.Exists(Path.Combine(dir, "tools")))
            await UnpackAsync(assets.Tools ?? throw new Exception($"No {rid} tools archive"), Path.Combine(dir, "tools"));
        File.WriteAllText(stampFile, Stamp + "\n");
        return dir;
    }

    async Task UnpackAsync(ImpellerAsset asset, string target)
    {
        var archive = await GetAsync(asset);
        var temp = target + ".partial";
        if (Directory.Exists(temp))
            Directory.Delete(temp, recursive: true);
        Directory.CreateDirectory(temp);
        ExtractStrippingRoot(archive, temp);
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        Directory.Move(temp, target);
        Log.Information("Unpacked {Name} to {Target}", asset.Name, target);
    }

    async Task<string> GetAsync(ImpellerAsset asset)
    {
        if (_builderDir != null)
        {
            var local = Path.Combine(_builderDir, asset.Name);
            Verify(local, asset);
            return local;
        }

        var path = Path.Combine(_cacheDir, Lock.Tag, asset.Name);
        if (File.Exists(path) && Matches(path, asset))
            return path;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var url = $"https://github.com/{Lock.Builder}/releases/download/{Lock.Tag}/{asset.Name}";
        await DownloadAsync(url, path);
        Verify(path, asset);
        return path;
    }

    public static HttpClient NewHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NImpeller-build");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    static async Task DownloadAsync(string url, string path)
    {
        using var http = NewHttpClient();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Log.Information("Downloading {Url}", url);
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var temp = path + ".download";
                await using (var file = File.Create(temp))
                    await response.Content.CopyToAsync(file);
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception e) when (attempt < 4 && e is HttpRequestException or IOException or TaskCanceledException)
            {
                Log.Warning("Download failed ({Message}); retrying", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }
    }

    static bool Matches(string path, ImpellerAsset asset) =>
        new FileInfo(path).Length == asset.Size && Sha256(path) == asset.Sha256;

    static void Verify(string path, ImpellerAsset asset)
    {
        if (!File.Exists(path))
            throw new Exception($"{path} not found");
        var size = new FileInfo(path).Length;
        if (size != asset.Size)
            throw new Exception($"{asset.Name} is {size} bytes, expected {asset.Size}");
        var sum = Sha256(path);
        if (sum != asset.Sha256)
            throw new Exception($"{asset.Name} SHA-256 is {sum}, expected {asset.Sha256}");
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Extracts a .tar.gz whose entries are all under one root directory, without that directory.</summary>
    static void ExtractStrippingRoot(string archive, string target)
    {
        var full = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            var slash = entry.Name.IndexOf('/');
            if (slash < 0 || slash == entry.Name.Length - 1)
                continue;
            var relative = entry.Name[(slash + 1)..].TrimEnd('/');
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(full, StringComparison.Ordinal))
                throw new Exception($"{archive}: entry {entry.Name} is outside the archive root");
            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(destination);
                    break;
                case TarEntryType.RegularFile or TarEntryType.V7RegularFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, overwrite: true);
                    break;
                case TarEntryType.SymbolicLink:
                    // The versioned Mac Catalyst framework. Windows hosts skip it: they use its zip.
                    if (OperatingSystem.IsWindows())
                        break;
                    var linkTarget = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(destination)!, entry.LinkName));
                    if (Path.IsPathRooted(entry.LinkName) || !linkTarget.StartsWith(full, StringComparison.Ordinal))
                        throw new Exception($"{archive}: link {entry.Name} -> {entry.LinkName} leaves the archive root");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.CreateSymbolicLink(destination, entry.LinkName);
                    break;
                default:
                    throw new Exception($"{archive}: unsupported entry {entry.Name} ({entry.EntryType})");
            }
        }
    }
}
