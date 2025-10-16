using System.Runtime.InteropServices;

namespace InteropGen;

class Program
{
    [DllImport("libc")]
    static extern void setenv(string name, string value, int overwrite);


    static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            setenv("LIBCLANG_DISABLE_CRASH_RECOVERY", "1", 1);
        
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: InteropGen <path to impeller.h>");
            return;
        }

        var impellerHeaderPath = Path.Combine(Directory.GetCurrentDirectory(), args[0]);
        if (!File.Exists(impellerHeaderPath))
        {
            Console.WriteLine($"File not found: {impellerHeaderPath}");
            return;
        }

        var model = NativeModel.Load(impellerHeaderPath, []);

        var dir = typeof(Program).Assembly.Location;
        Directory.SetCurrentDirectory(Path.Combine(dir, ".."));
        while (!File.Exists("NImpeller.sln"))
        {
            Directory.SetCurrentDirectory("..");
            var curDir = Directory.GetCurrentDirectory();
            if (dir == curDir)
                throw new Exception();
            dir = curDir;
        }

        var cg = new CodeGen();
        Generator.Generate(model, cg);
        Directory.CreateDirectory("src/NImpeller/Generated");
        File.WriteAllText("src/NImpeller/Generated/Bindings.g.cs", cg.ToString());
    }
}