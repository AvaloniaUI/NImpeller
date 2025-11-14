using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Sandbox;
using Sandbox.Scenes;

static class Program
{
    private static ILoggerFactory? _loggerFactory;

    private static readonly string[] AllApis = ["OpenGL", "Vulkan", "Metal"];
    private static readonly string[] NonMetalApis = ["OpenGL", "Vulkan"];

    private static readonly IScene[] AvailableScenes =
    [
        new MMarkScene(),
        new ParagraphScene(),
        new CirclingSquares()
    ];

    public enum GraphicsApi
    {
        OpenGL,
        Vulkan,
        Metal
    }

    public class Options
    {
        [Option('a', "api", Required = false, Default = GraphicsApi.OpenGL, HelpText = "Graphics API to use (OpenGL, Vulkan, Metal)")]
        public GraphicsApi Api { get; set; }

        [Option('s', "scene", Required = false, Default = "MMark", HelpText = "Scene to render. Available scenes: MMark, Paragraph, CirclingSquares")]
        public string Scene { get; set; } = "MMark";

        [Option('w', "width", Required = false, Default = 800, HelpText = "Window width")]
        public int Width { get; set; }

        [Option('h', "height", Required = false, Default = 600, HelpText = "Window height")]
        public int Height { get; set; }
    }

    static void Main(string[] args)
    {
        // Configure logging
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddDebug()
                .SetMinimumLevel(LogLevel.Debug);
        });

        // If no arguments provided, show interactive menu
        if (args.Length == 0)
        {
            var options = ShowInteractiveMenu();
            if (options != null)
            {
                RunApplication(options);
            }
            return;
        }

        var parser = new Parser(with => with.CaseInsensitiveEnumValues = true);
        parser.ParseArguments<Options>(args)
            .WithParsed(RunApplication)
            .WithNotParsed(errors => Environment.Exit(1));
    }

    static Options? ShowInteractiveMenu()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("NImpeller Sandbox")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]A .NET binding for Flutter's Impeller graphics engine[/]\n");

        // Select Graphics API
        var availableApis = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? AllApis : NonMetalApis;

        var selectedApi = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select Graphics API:[/]")
                .PageSize(10)
                .AddChoices(availableApis));

        var api = selectedApi switch
        {
            "OpenGL" => GraphicsApi.OpenGL,
            "Vulkan" => GraphicsApi.Vulkan,
            "Metal" => GraphicsApi.Metal,
            _ => GraphicsApi.OpenGL
        };

        // Select Scene
        var selectedScene = AnsiConsole.Prompt(
            new SelectionPrompt<IScene>()
                .Title("[green]Select Scene:[/]")
                .PageSize(10)
                .UseConverter(scene => $"{scene.Name} - [dim]{scene.Description}[/]")
                .AddChoices(AvailableScenes));

        // Get window dimensions
        var width = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]Window width:[/]")
                .DefaultValue(800)
                .ValidationErrorMessage("[red]Please enter a valid number[/]")
                .Validate(w => w > 0 && w <= 4096
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Width must be between 1 and 4096[/]")));

        var height = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]Window height:[/]")
                .DefaultValue(600)
                .ValidationErrorMessage("[red]Please enter a valid number[/]")
                .Validate(h => h > 0 && h <= 4096
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Height must be between 1 and 4096[/]")));

        AnsiConsole.MarkupLine($"\n[cyan]Starting {selectedApi} with {selectedScene.Name} scene ({width}x{height})...[/]\n");

        return new Options
        {
            Api = api,
            Scene = selectedScene.Name,
            Width = width,
            Height = height
        };
    }

    static IScene? GetSceneByName(string sceneName)
    {
        return AvailableScenes.FirstOrDefault(s =>
            s.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
    }

    static void RunApplication(Options options)
    {
        var logger = _loggerFactory!.CreateLogger("Sandbox");

        // Get the scene based on the selected name
        var scene = GetSceneByName(options.Scene);
        if (scene == null)
        {
            var availableScenes = string.Join(", ", AvailableScenes.Select(s => s.Name));
            logger.LogError("Unknown scene: {Scene}. Available scenes: {AvailableScenes}", options.Scene, availableScenes);
            return;
        }

        // Check if Metal is requested and we're on macOS
        if (options.Api == GraphicsApi.Metal)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogError("Metal is only supported on macOS");
                return;
            }

            var metalLogger = _loggerFactory!.CreateLogger<MetalApplication>();
            var metalApp = new MetalApplication(options.Width, options.Height, "NImpeller on Metal", metalLogger);
            metalApp.SetScene(scene);
            RunWithConsoleDisplay(metalApp);
            return;
        }

        // For OpenGL and Vulkan, use SDL
        var sdlApi = options.Api == GraphicsApi.OpenGL ? SdlApplication.GraphicsApi.OpenGL : SdlApplication.GraphicsApi.Vulkan;
        var sdlLogger = _loggerFactory!.CreateLogger<SdlApplication>();
        var sdlApp = new SdlApplication(sdlApi, sdlLogger);
        if (sdlApp.Initialize(options.Width, options.Height))
        {
            sdlApp.SetScene(scene);
            RunWithConsoleDisplay(sdlApp);
        }
    }

    static void RunWithConsoleDisplay(IApplication app)
    {
        var currentStatus = app.GetStatus();
        var isRunning = true;

        // Subscribe to status updates
        app.StatusUpdated += (sender, e) =>
        {
            currentStatus = e.Status;
        };

        // Start console display in background thread
        var consoleTask = Task.Run(() =>
        {
            AnsiConsole.Live(CreateStatusTable(currentStatus))
                .Start(ctx =>
                {
                    while (isRunning)
                    {
                        ctx.UpdateTarget(CreateStatusTable(currentStatus));
                        Thread.Sleep(100); // Update display every 100ms
                    }
                });
        });

        app.Run();

        isRunning = false;

        consoleTask.Wait(TimeSpan.FromSeconds(1));
    }

    static Table CreateStatusTable(ApplicationStatus status)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Metric[/]").Centered())
            .AddColumn(new TableColumn("[bold]Value[/]").Centered());

        table.AddRow("[cyan]FPS[/]", $"[green]{status.CurrentFps}[/]");
        table.AddRow("[cyan]Total Frames[/]", $"[yellow]{status.TotalFrames:N0}[/]");
        table.AddRow("[cyan]Runtime[/]", $"[magenta]{status.RunTime:hh\\:mm\\:ss}[/]");

        return table;
    }
}

