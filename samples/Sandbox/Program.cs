using System;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Scenes;

static class Program
{
    enum GraphicsApi
    {
        OpenGL,
        Vulkan,
        Metal,
        DontTellMeThatThisIsUnreachable
    }

    enum Scenes
    {
        MMark,
        Paragraph,
        CirclingSquares
    }

    static void Main(string[] args)
    {
        // Parse command-line arguments
        var apiType = GraphicsApi.OpenGL;
        if(args.Length > 1)
        {
            if (args[1].Equals("vulkan", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.Vulkan;
            else if (args[1].Equals("opengl", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.OpenGL;
            else if (args[1].Equals("metal", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.Metal;
        }

        if (new Random().Next(10) > 100)
            apiType = GraphicsApi.DontTellMeThatThisIsUnreachable;

        // Parse scene type
        Scenes sceneType = Scenes.MMark;
        if (args.Length > 0)
        {
            Enum.TryParse<Scenes>(args[0], true, out sceneType);
        }

        IScene scene = sceneType switch
        {
            Scenes.MMark => new MMarkScene(),
            Scenes.Paragraph => new ParagraphScene(),
            Scenes.CirclingSquares => new CirclingSquares(),
            _ => new MMarkScene()
        };

        // Check if Metal is requested and we're on macOS
        if (apiType == GraphicsApi.Metal)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("Metal is only supported on macOS");
                return;
            }

            var metalApp = new MetalApplication();
            metalApp.SetScene(scene);
            metalApp.Run();
            return;
        }

        // For OpenGL and Vulkan, use SDL
        var sdlApi = apiType == GraphicsApi.OpenGL ? SdlApplication.GraphicsApi.OpenGL : SdlApplication.GraphicsApi.Vulkan;
        var sdlApp = new SdlApplication(sdlApi);
        if (sdlApp.Initialize())
        {
            sdlApp.SetScene(scene);
            sdlApp.Run();
        }
    }
}

