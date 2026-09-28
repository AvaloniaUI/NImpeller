using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

// Turns the SDK's bitcode libimpeller.a into one object whose only global definitions are the Impeller C API.
// A throwaway LTO link with the platform's lld internalizes everything else; the LTO object it saves is the result.
partial class Build
{
    Target InternalizeImpeller => _ => _
        .Description("Internalize artifacts/impeller/<platform>/lib/libimpeller.a in place (BuildImpeller runs this itself)")
        .Requires(() => Platform != null || All)
        .Executes(() =>
        {
            var platforms = All
                ? SupportedPlatforms.Concat(LocallyBuiltPlatforms).Where(p => Directory.Exists(ArtifactsDirectory / p)).ToArray()
                : new[] { Platform };
            foreach (var platform in platforms)
            {
                var workDir = EngineSrc / "out" / $"nimpeller_{platform.Replace('-', '_')}_{ImpellerRuntimeMode}" / "nimpeller_internalize";
                InternalizeStaticLibrary(platform, ArtifactsDirectory / platform, workDir);
            }
        });

    AbsolutePath HostLlvmBin => EngineSrc / "flutter" / "buildtools" / HostPlatform / "clang" / "bin";

    // The Impeller C API, parsed from impeller.h.
    static string[] ImpellerExports(AbsolutePath header)
    {
        var text = File.ReadAllText(header);
        var names = Regex.Matches(text, @"^IMPELLER_EXPORT\b(.*?)\(", RegexOptions.Multiline | RegexOptions.Singleline)
            .Select(m => Regex.Matches(m.Groups[1].Value, @"[A-Za-z_]\w*").Last().Value)
            .ToArray();
        if (names.Length == 0)
        {
            throw new Exception($"No IMPELLER_EXPORT declarations found in {header}");
        }
        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
        {
            throw new Exception($"Duplicate exports in {header}: {string.Join(", ", duplicates)}");
        }
        return names;
    }

    static void WriteExportLists(string[] exports, AbsolutePath dir)
    {
        static string Lines(IEnumerable<string> lines) => string.Concat(lines.Select(l => l + "\n"));
        File.WriteAllText(dir / "exports.txt", Lines(exports));
        File.WriteAllText(dir / "exports.map", "{\n  global:\n" + Lines(exports.Select(e => $"    {e};")) + "  local: *;\n};\n");
        File.WriteAllText(dir / "exports.macho.txt", Lines(exports.Select(e => "_" + e)));
        File.WriteAllText(dir / "exports.def", "EXPORTS\n" + Lines(exports.Select(e => "  " + e)));
        File.WriteAllText(dir / "exports.wasm.rsp", Lines(exports.Select(e => "--export-if-defined=" + e)));
    }

    record ArchiveMember(int Index, string Name, long Offset, long Size, byte[] Magic);

    // GNU/BSD ar reader: data members only, in order.
    static List<ArchiveMember> ReadArchive(AbsolutePath archive)
    {
        using var stream = File.OpenRead(archive);
        var magic = new byte[8];
        stream.ReadExactly(magic);
        if (Encoding.ASCII.GetString(magic) != "!<arch>\n")
        {
            throw new Exception($"{archive} is not an ar archive");
        }

        var members = new List<ArchiveMember>();
        string longNames = null;
        var header = new byte[60];
        while (stream.Position < stream.Length)
        {
            if (stream.Position % 2 == 1)
            {
                stream.Position++;
                if (stream.Position >= stream.Length)
                    break;
            }
            stream.ReadExactly(header);
            var name = Encoding.ASCII.GetString(header, 0, 16).TrimEnd(' ');
            var size = long.Parse(Encoding.ASCII.GetString(header, 48, 10).Trim());
            var offset = stream.Position;

            if (name.StartsWith("#1/"))
            {
                // BSD: the name precedes the data.
                var nameLength = int.Parse(name[3..]);
                var nameBytes = new byte[nameLength];
                stream.ReadExactly(nameBytes);
                name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                offset += nameLength;
                size -= nameLength;
            }

            if (name == "//")
            {
                var table = new byte[size];
                stream.ReadExactly(table);
                longNames = Encoding.UTF8.GetString(table);
            }
            else if (name is "/" or "/SYM64/" or "__.SYMDEF" or "__.SYMDEF SORTED" or "__.SYMDEF_64")
            {
                // Symbol table.
            }
            else
            {
                if (name.Length > 1 && name[0] == '/' && char.IsDigit(name[1]))
                {
                    var start = int.Parse(name[1..]);
                    var end = longNames!.IndexOf('\n', start);
                    name = longNames[start..end];
                }
                name = name.TrimEnd('/');

                stream.Position = offset;
                var memberMagic = new byte[Math.Min(8, size)];
                stream.ReadExactly(memberMagic);
                members.Add(new ArchiveMember(members.Count, name, offset, size, memberMagic));
            }
            stream.Position = offset + size;
        }
        return members;
    }

    static bool IsBitcode(byte[] magic) =>
        magic.Length >= 4 && ((magic[0] == 'B' && magic[1] == 'C' && magic[2] == 0xC0 && magic[3] == 0xDE)
            // Bitcode wrapper (Darwin).
            || (magic[0] == 0xDE && magic[1] == 0xC0 && magic[2] == 0x17 && magic[3] == 0x0B));

    static string ObjectFormat(byte[] magic)
    {
        if (IsBitcode(magic))
            return "bitcode";
        if (magic.Length >= 4 && magic[0] == 0x7F && magic[1] == 'E' && magic[2] == 'L' && magic[3] == 'F')
            return "elf";
        if (magic.Length >= 4 && magic[0] == 0 && magic[1] == 'a' && magic[2] == 's' && magic[3] == 'm')
            return "wasm";
        if (magic.Length >= 4 && BitConverter.ToUInt32(magic, 0) is 0xFEEDFACF or 0xFEEDFACE)
            return "macho";
        if (magic.Length >= 2 && BitConverter.ToUInt16(magic, 0) is 0x8664 or 0xAA64 or 0x14C)
            return "coff";
        return "unknown";
    }

    static string PlatformObjectFormat(string platform) => platform.Split('-')[0] switch
    {
        "wasm" => "wasm",
        "darwin" or "ios" => "macho",
        "windows" => "coff",
        _ => "elf",
    };

    void InternalizeStaticLibrary(string platform, AbsolutePath platformDir, AbsolutePath workDir)
    {
        var format = PlatformObjectFormat(platform);
        var libDir = platformDir / "lib";
        var archive = libDir / (format == "coff" ? "impeller_static.lib" : "libimpeller.a");
        var objectName = format == "coff" ? "impeller.obj" : "impeller.o";
        if (!File.Exists(archive))
        {
            Log.Information("{Platform}: no static library at {Archive}, nothing to internalize", platform, archive);
            return;
        }

        var members = ReadArchive(archive);
        var archiveBytes = new FileInfo(archive).Length;
        if (members.Count == 1 && members[0].Name == objectName && !IsBitcode(members[0].Magic))
        {
            Log.Information("{Platform}: {Archive} is already internalized", platform, archive);
            return;
        }
        if (!members.Any(m => IsBitcode(m.Magic)))
        {
            // Debug builds are compiled without LTO. Don't ship an archive that exports Skia/libc++.
            Log.Warning("{Platform}: {Archive} has no bitcode (debug build?); deleting it instead of shipping an un-internalized archive", platform, archive);
            archive.DeleteFile();
            return;
        }
        if (format is "macho" or "coff")
        {
            // Recipes in the spec are unverified; see "Landing" steps 4 and 5.
            Log.Warning("{Platform}: {Format} internalization is not implemented yet; deleting {Archive}", platform, format, archive);
            archive.DeleteFile();
            return;
        }

        workDir.CreateOrCleanDirectory();
        var exports = ImpellerExports(platformDir / "include" / "impeller.h");
        WriteExportLists(exports, workDir);

        var nativeDir = workDir / "native";
        nativeDir.CreateDirectory();
        var natives = new List<string>();
        using (var stream = File.OpenRead(archive))
        {
            foreach (var member in members.Where(m => !IsBitcode(m.Magic)))
            {
                var memberFormat = ObjectFormat(member.Magic);
                if (memberFormat != format)
                {
                    throw new Exception($"{archive}: member #{member.Index} {member.Name} is {memberFormat}, expected {format} or bitcode");
                }
                var path = nativeDir / $"{member.Index:D5}_{member.Name}";
                stream.Position = member.Offset;
                using (var output = File.Create(path))
                {
                    CopyBytes(stream, output, member.Size);
                }
                natives.Add(path);
                Log.Information("{Platform}: native member #{Index} {Name} ({Size} bytes)", platform, member.Index, member.Name, member.Size);
            }
        }

        var finalObject = workDir / objectName;
        if (format == "wasm")
            InternalizeWasm(archive, natives, workDir, finalObject);
        else
            InternalizeElf(platform, archive, natives, workDir, finalObject);

        var nm = format == "wasm" ? WasmLlvmBin / "llvm-nm" : HostLlvmBin / "llvm-nm";
        var globals = CaptureLines(nm, "--extern-only", "--defined-only", "--just-symbol-name", finalObject);
        var undefined = CaptureLines(nm, "--undefined-only", "--just-symbol-name", finalObject);

        // Post-checks.
        var missing = exports.Except(globals).ToArray();
        var extra = globals.Except(exports).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
        {
            throw new Exception($"{platform}: global definitions differ from the export list. " +
                $"Missing ({missing.Length}): {string.Join(", ", missing.Take(20))}. Extra ({extra.Length}): {string.Join(", ", extra.Take(20))}");
        }
        if (format == "elf")
        {
            var groups = CaptureLines(HostLlvmBin / "llvm-readelf", "--section-headers", "--wide", finalObject)
                .Count(l => Regex.IsMatch(l, @"\]\s+\S+\s+GROUP\s"));
            if (groups > 0)
            {
                throw new Exception($"{platform}: {finalObject} still has {groups} section groups");
            }
            // Android before API 29 has no ELF TLS; flutter targets older APIs with emulated TLS.
            if (platform.StartsWith("android-") && CaptureLines(HostLlvmBin / "llvm-readelf", "--section-headers", "--wide", finalObject).Any(l => Regex.IsMatch(l, @"\.t(data|bss)\b")))
            {
                throw new Exception($"{platform}: {finalObject} uses ELF TLS; the LTO link is missing -emulated-tls");
            }
        }
        if (format == "wasm")
        {
            var sjlj = undefined.Where(u => u is "emscripten_longjmp" or "saveSetjmp" or "testSetjmp" || u.StartsWith("invoke_")).ToArray();
            if (sjlj.Length > 0)
            {
                throw new Exception($"{platform}: {finalObject} uses Emscripten JS exceptions/setjmp: {string.Join(", ", sjlj)}");
            }
        }

        File.WriteAllText(libDir / "impeller.undefined.txt", string.Concat(undefined.Order(StringComparer.Ordinal).Select(u => u + "\n")));

        var tempArchive = workDir / archive.Name;
        tempArchive.DeleteFile();
        var ar = format == "wasm" ? WasmLlvmBin / "llvm-ar" : HostLlvmBin / "llvm-ar";
        Run(ar, new[] { "rcs", tempArchive.ToString(), objectName }, workDir);
        File.Move(tempArchive, archive, overwrite: true);

        Log.Information("{Platform}: {Archive}: {OldMembers} members / {OldBytes:N0} bytes -> 1 member / {NewBytes:N0} bytes; {Globals} globals, {Undefined} undefined",
            platform, archive, members.Count, archiveBytes, new FileInfo(archive).Length, globals.Length, undefined.Length);
    }

    void InternalizeElf(string platform, AbsolutePath archive, List<string> natives, AbsolutePath workDir, AbsolutePath finalObject)
    {
        var emulation = platform.Split('-').Last() switch
        {
            "x64" => "elf_x86_64",
            "arm64" => "aarch64linux",
            "arm" => "armelf_linux_eabi",
            var cpu => throw new Exception($"No ELF emulation for {cpu}"),
        };

        var ltoObject = workDir / "impeller.lto.o";
        Run(HostLlvmBin / "ld.lld", new[]
        {
            "-shared", "-m", emulation, "-O2", "--version-script=exports.map",
        }.Concat(SharedLibraryLtoOptions(workDir.Parent)).Concat(new[]
        {
            "--whole-archive", archive.ToString(), "--no-whole-archive",
            $"--lto-obj-path={ltoObject}", "-o", "throwaway.so",
        }), workDir);

        var merged = ltoObject;
        if (natives.Count > 0)
        {
            merged = workDir / "merged.o";
            Run(HostLlvmBin / "ld.lld", new[] { "-r", "-m", emulation, ltoObject.ToString() }.Concat(natives).Concat(new[] { "-o", merged.ToString() }), workDir);
        }

        Run(HostLlvmBin / "llvm-objcopy", new[]
        {
            "--keep-global-symbols=exports.txt", "--remove-section=.group", "--strip-debug", merged.ToString(), finalObject.ToString(),
        }, workDir);
        (workDir / "throwaway.so").DeleteFile();
    }

    // The LTO codegen options (-plugin-opt=, -mllvm, --lto-*) the clang driver passes to lld for flutter's own
    // libimpeller.so link, e.g. -emulated-tls on Android < 29. Keeps codegen the same as the shipped shared library.
    string[] SharedLibraryLtoOptions(AbsolutePath outDir)
    {
        var commands = Capture(EngineNinja, new[] { "-C", outDir.ToString(), "-t", "commands", "flutter/impeller/toolkit/interop:shared_library" }, EngineSrc);
        var link = commands.Split('\n').Last();
        // Ninja deletes the .rsp (the inputs) after linking; the options don't depend on it.
        link = Regex.Replace(link[..link.IndexOf(" && ", StringComparison.Ordinal)], @"\s@\S+\.rsp", "");
        var driver = Capture("sh", new[] { "-c", link.Replace(" -shared ", " -### -shared ") + " 2>&1" }, outDir);
        var ld = driver.Split('\n').Last(l => l.Contains("ld.lld\""));
        var args = Regex.Matches(ld, @"""((?:[^""\\]|\\.)*)""").Select(m => m.Groups[1].Value).ToArray();

        var options = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-mllvm")
                options.AddRange(new[] { args[i], args[++i] });
            else if (args[i].StartsWith("-plugin-opt=") || args[i].StartsWith("--lto-"))
                options.Add(args[i]);
        }
        Log.Information("LTO options from {OutDir} shared_library link: {Options}", outDir.Name, string.Join(' ', options));
        return options.ToArray();
    }

    void InternalizeWasm(AbsolutePath archive, List<string> natives, AbsolutePath workDir, AbsolutePath finalObject)
    {
        if (natives.Count > 0)
        {
            throw new Exception($"{archive}: native wasm members aren't supported: {string.Join(", ", natives.Select(Path.GetFileName))}");
        }

        // -mllvm flags mirror emcc's -fwasm-exceptions + SUPPORT_LONGJMP=wasm link.
        Run(WasmLlvmBin / "wasm-ld", new[]
        {
            "--no-entry", "--allow-undefined", "@exports.wasm.rsp",
            "--whole-archive", archive.ToString(), "--no-whole-archive", "--lto-O2",
            "-mllvm", "-wasm-enable-eh", "-mllvm", "-wasm-enable-sjlj", "-mllvm", "-exception-model=wasm", "-mllvm", "-disable-lsr",
            "--save-temps", "-o", "throwaway.wasm",
        }, workDir);

        var ltoObject = workDir / "throwaway.wasm.lto.o";
        if (!File.Exists(ltoObject))
        {
            throw new Exception($"wasm-ld --save-temps didn't write {ltoObject}");
        }
        Run("python3", new[] { (RootDirectory / "wasm-finalize-object.py").ToString(), ltoObject.ToString(), finalObject.ToString(), "exports.txt" }, workDir);

        foreach (var temp in workDir.GlobFiles("throwaway.wasm*").Where(f => f != ltoObject))
        {
            temp.DeleteFile();
        }
    }

    AbsolutePath WasmLlvmBin => EmscriptenPack("Sdk") / "bin";

    static void CopyBytes(Stream input, Stream output, long count)
    {
        var buffer = new byte[1 << 20];
        while (count > 0)
        {
            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
                throw new EndOfStreamException();
            output.Write(buffer, 0, read);
            count -= read;
        }
    }

    static string[] CaptureLines(string fileName, params string[] arguments) =>
        Capture(fileName, arguments, Directory.GetCurrentDirectory())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
