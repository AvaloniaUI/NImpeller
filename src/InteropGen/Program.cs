using System.Runtime.InteropServices;

namespace InteropGen;

class Program
{
    [DllImport("libc")]
    static extern void setenv(string name, string value, int overwrite);


    static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            setenv("LIBCLANG_DISABLE_CRASH_RECOVERY", "1", 1);
        
        if (args.Length is < 1 or > 2)
        {
            Console.WriteLine("Usage: InteropGen <path to impeller.h> [output .cs path]");
            return;
        }

        var impellerHeaderPath = Path.Combine(Directory.GetCurrentDirectory(), args[0]);
        if (!File.Exists(impellerHeaderPath))
        {
            Console.WriteLine($"File not found: {impellerHeaderPath}");
            return;
        }

        var model = NativeModel.Load(impellerHeaderPath, []);
        var outputPath = args.Length == 2 ? Path.GetFullPath(args[1]) : null;

        var dir = typeof(Program).Assembly.Location;
        Directory.SetCurrentDirectory(Path.Combine(dir, ".."));
        while (!File.Exists("NImpeller.slnx"))
        {
            Directory.SetCurrentDirectory("..");
            var curDir = Directory.GetCurrentDirectory();
            if (dir == curDir)
                throw new Exception();
            dir = curDir;
        }

        var cg = new CodeGen();
        Generator.Generate(model, cg);
        outputPath ??= Path.GetFullPath("src/NImpeller/Generated/Bindings.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, cg.ToString());
    }
}
