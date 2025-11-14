using System;
using System.Runtime.InteropServices;
using CommandLine;
using Sandbox;
using Sandbox.Scenes;

static class Program
{
    public enum GraphicsApi
    {
        OpenGL,
        Vulkan,
        Metal
    }

    public enum SceneType
    {
        MMark,
        Paragraph,
        CirclingSquares
    }

    public class Options
    {
        [Option('a', "api", Required = false, Default = GraphicsApi.OpenGL, HelpText = "Graphics API to use (OpenGL, Vulkan, Metal)")]
        public GraphicsApi Api { get; set; }

        [Option('s', "scene", Required = false, Default = SceneType.MMark, HelpText = "Scene to render (MMark, Paragraph, CirclingSquares)")]
        public SceneType Scene { get; set; }

        [Option('w', "width", Required = false, Default = 800, HelpText = "Window width")]
        public int Width { get; set; }

        [Option('h', "height", Required = false, Default = 600, HelpText = "Window height")]
        public int Height { get; set; }
    }

    static void Main(string[] args)
    {
        var parser = new Parser(with => with.CaseInsensitiveEnumValues = true);
        parser.ParseArguments<Options>(args)
            .WithParsed(RunApplication)
            .WithNotParsed(errors => Environment.Exit(1));
    }

    static void RunApplication(Options options)
    {
        // Create the scene based on the selected type
        IScene scene = options.Scene switch
        {
            SceneType.MMark => new MMarkScene(),
            SceneType.Paragraph => new ParagraphScene(),
            SceneType.CirclingSquares => new CirclingSquares(),
            _ => new MMarkScene()
        };

        // Check if Metal is requested and we're on macOS
        if (options.Api == GraphicsApi.Metal)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("Metal is only supported on macOS");
                return;
            }

            var metalApp = new MetalApplication();
            metalApp.SetScene(scene);
            metalApp.Run(options.Width, options.Height);
            return;
        }

        // For OpenGL and Vulkan, use SDL
        var sdlApi = options.Api == GraphicsApi.OpenGL ? SdlApplication.GraphicsApi.OpenGL : SdlApplication.GraphicsApi.Vulkan;
        var sdlApp = new SdlApplication(sdlApi);
        if (sdlApp.Initialize(options.Width, options.Height))
        {
            sdlApp.SetScene(scene);
            sdlApp.Run();
        }
    }
}

