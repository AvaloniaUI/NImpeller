using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotMemoryUnit.DotMemoryUnitTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Flutter commit SHA for Impeller SDK download")]
    readonly string ImpellerSha;

    [Parameter("Platform to download (linux-x64, darwin-arm64, etc.)")]
    readonly string Platform;

    [Parameter("Download all platforms")]
    readonly bool All;

    [Parameter("Extract downloaded files")]
    readonly bool Extract = true;

    [Parameter("Keep zip files after extraction")]
    readonly bool KeepZip;

    static readonly string[] SupportedPlatforms =
    {
        "linux-arm64",
        "linux-x64",
        "windows-arm64",
        "windows-x64",
        "darwin-arm64",
        "darwin-x64",
        "android-arm64",
        "android-arm",
        "android-x86",
        "android-x64"
    };

    const string BaseUrl = "https://storage.googleapis.com/flutter_infra_release/flutter";
    const string EngineRepo = "https://github.com/flutter/flutter.git";

    AbsolutePath OutputDirectory => RootDirectory / "external" / "impeller_sdk";

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

    Target GenerateBindings => _ => _
        .Description("Generate Impeller bindings from impeller.h using InteropGen")
        .Requires(() => Platform != null || All)
        .Executes(() =>
        {
            if (!string.IsNullOrEmpty(Platform) && All)
            {
                throw new Exception("Cannot specify both --platform and --all");
            }

            if (All)
            {
                foreach (var item in SupportedPlatforms)
                {
                    GenerateBindingsWithPlatform(Platform);
                }
            }
            else
            {
                if (!SupportedPlatforms.Contains(Platform))
                {
                    throw new Exception($"Platform {Platform} is unsupported");
                }

                GenerateBindingsWithPlatform(Platform);
            }
        });

    Target ListImpellerCommits => _ => _
        .Description("List available Flutter engine commits")
        .Executes(() =>
        {
            Log.Information("Fetching Flutter engine commits...");
            Log.Information("");
            Log.Information("Latest Flutter Engine Commits:");
            Log.Information("===============================");

            var headSha = GitTasks.Git("ls-remote " + EngineRepo + " HEAD", logOutput: false)
                .FirstOrDefault().Text.Split('\t')[0];

            if (!string.IsNullOrEmpty(headSha))
            {
                Log.Information("{Sha} (HEAD)", headSha);
            }

            Log.Information("");
            Log.Information("Recent tagged releases:");

            var tags = GitTasks.Git("ls-remote --tags " + EngineRepo, logOutput: false)
                .Where(x => !x.Text.Contains("^{}"))
                .TakeLast(10);

            foreach (var tag in tags)
            {
                var parts = tag.Text.Split('\t');
                var sha = parts[0];
                var tagName = parts[1].Replace("refs/tags/", "");
                Log.Information("{Sha}  ({Tag})", sha, tagName);
            }
        });

    Target DownloadImpeller => _ => _
        .Description("Download Impeller SDK for specified platform(s)")
        .Requires(() => ImpellerSha)
        .Requires(() => Platform != null || All)
        .Executes(async () =>
        {
            if (!string.IsNullOrEmpty(Platform) && All)
            {
                throw new Exception("Cannot specify both --platform and --all");
            }

            if (All)
            {
                await DownloadAllPlatformsAsync(ImpellerSha);
            }
            else
            {
                if (!SupportedPlatforms.Contains(Platform))
                {
                    throw new Exception($"Invalid platform: {Platform}. Valid platforms: {string.Join(", ", SupportedPlatforms)}");
                }
                await DownloadPlatformAsync(ImpellerSha, Platform);
            }
        });

    Target DownloadLatestImpeller => _ => _
        .Description("Download Impeller SDK for the latest Flutter engine commit")
        .Requires(() => Platform != null || All)
        .Executes(async () =>
        {
            if (!string.IsNullOrEmpty(Platform) && All)
            {
                throw new Exception("Cannot specify both --platform and --all");
            }

            Log.Information("Fetching latest engine commit SHA...");
            var sha = GitTasks.Git("ls-remote " + EngineRepo + " HEAD", logOutput: false)
                .FirstOrDefault().Text.Split('\t')[0];

            if (string.IsNullOrEmpty(sha))
            {
                throw new Exception("Failed to fetch latest commit SHA");
            }

            Log.Information("Latest SHA: {Sha}", sha);
            Log.Information("");

            if (All)
            {
                await DownloadAllPlatformsAsync(sha);
            }
            else
            {
                if (!SupportedPlatforms.Contains(Platform))
                {
                    throw new Exception($"Invalid platform: {Platform}. Valid platforms: {string.Join(", ", SupportedPlatforms)}");
                }
                await DownloadPlatformAsync(sha, Platform);
            }
        });

    async Task DownloadAllPlatformsAsync(string sha)
    {
        Log.Information("Downloading all platforms for SHA: {Sha}", sha);
        Log.Information("");

        var successCount = 0;
        var failCount = 0;

        foreach (var platform in SupportedPlatforms)
        {
            try
            {
                await DownloadPlatformAsync(sha, platform);
                successCount++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download {Platform}", platform);
                failCount++;
            }
            Log.Information("");
        }

        Log.Information("===============================");
        Log.Information("Successfully downloaded: {Count} platforms", successCount);
        if (failCount > 0)
        {
            Log.Warning("Failed downloads: {Count} platforms", failCount);
        }
    }

    void GenerateBindingsWithPlatform(string platform)
    {
        var impellerHeaderPath = OutputDirectory / platform / "include" / "impeller.h";

        if (!File.Exists(impellerHeaderPath))
        {
            throw new Exception(
                $"Impeller header file not found at: {impellerHeaderPath}\n" +
                $"Please download the {platform} Impeller SDK first by running the Nuke build task:\n" +
                $"  DownloadLatestImpeller --platform {platform}\n" +
                "or\n" +
                $"  DownloadImpeller --impeller-sha <commit-sha> --platform {platform}");
        }

        Log.Information("Found impeller.h at: {Path}", impellerHeaderPath);
        Log.Information("Building InteropGen...");

        var interopGenProject = RootDirectory / "src" / "InteropGen" / "InteropGen.csproj";
        DotNetBuild(s => s
            .SetProjectFile(interopGenProject)
            .SetConfiguration(Configuration));

        Log.Information("Running InteropGen to generate bindings...");

        DotNetRun(s => s
            .SetProjectFile(interopGenProject)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .SetApplicationArguments(impellerHeaderPath));

        var generatedFile = RootDirectory / "src" / "NImpeller" / "Generated" / "Bindings.g.cs";
        if (File.Exists(generatedFile))
        {
            var fileInfo = new FileInfo(generatedFile);
            var fileSize = fileInfo.Length / 1024.0;
            Log.Information("Successfully generated bindings at: {Path} ({Size:F2} KB)", generatedFile, fileSize);
        }
        else
        {
            throw new Exception("Failed to generate bindings - output file not found");
        }
    }

    async Task DownloadPlatformAsync(string sha, string platform)
    {
        var url = $"{BaseUrl}/{sha}/{platform}/impeller_sdk.zip";
        var platformDir = OutputDirectory / platform;
        var zipFile = platformDir / "impeller_sdk.zip";

        platformDir.CreateOrCleanDirectory();

        Log.Information("Checking availability: {Platform}", platform);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        // Check if URL exists
        var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
        if (!headResponse.IsSuccessStatusCode)
        {
            throw new Exception($"SDK not available for {platform} at SHA {sha}\nURL: {url}");
        }

        Log.Information("Downloading {Platform}...", platform);

        // Download the file
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var fileStream = File.Create(zipFile);
        await response.Content.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        fileStream.Close();

        var fileInfo = new FileInfo(zipFile);
        var fileSize = fileInfo.Length / 1024.0 / 1024.0;
        Log.Information("Downloaded {Platform} ({Size:F2} MB)", platform, fileSize);

        // Extract if requested
        if (Extract)
        {
            Log.Information("Extracting {Platform}...", platform);
            ZipFile.ExtractToDirectory(zipFile, platformDir);
            Log.Information("Extracted to {Directory}", platformDir);

            // Show extracted files
            var libExt = GetLibExtension(platform);
            var headerPath = platformDir / "include" / "impeller.h";
            var libPath1 = platformDir / "lib" / $"impeller.{libExt}";
            var libPath2 = platformDir / "lib" / $"libimpeller.{libExt}";

            if (File.Exists(headerPath))
            {
                Log.Information("  - include/impeller.h");
            }
            if (File.Exists(libPath1) || File.Exists(libPath2))
            {
                Log.Information("  - lib/impeller.{Extension}", libExt);
            }

            // Remove zip if not keeping
            if (!KeepZip)
            {
                File.Delete(zipFile);
                Log.Information("Removed zip file");
            }
        }
    }

    static string GetLibExtension(string platform)
    {
        if (platform.StartsWith("windows-"))
            return "dll";
        if (platform.StartsWith("darwin-"))
            return "dylib";
        return "so";
    }
}
