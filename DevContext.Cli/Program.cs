using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DevContext.Core;
using DevContext.Core.Extractors;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevContext.Cli
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandApp<ExtractCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("devcontext");
                config.SetApplicationVersion("1.0.0");

                config.AddCommand<ExtractCommand>("extract")
                    .WithAlias("e")
                    .WithDescription("Extract context from a .NET project or solution")
                    .WithExample(new[] { "extract", ".", "-o", "context.md" })
                    .WithExample(new[] { "extract", "C:\\MyProject", "--config", "devcontext.json" });

                config.AddCommand<InitCommand>("init")
                    .WithDescription("Initialize a configuration file")
                    .WithExample(new[] { "init" });

                config.AddCommand<DetectCommand>("detect")
                    .WithDescription("Detect project architecture and features")
                    .WithExample(new[] { "detect", "." });
            });

            return await app.RunAsync(args);
        }
    }

    public class ExtractCommand : AsyncCommand<ExtractCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[PATH]")]
            public string Path { get; set; } = @"c:\code\github\dntsite";

            [CommandOption("-o|--output")]
            public string? OutputFile { get; set; } = "new.md";

            [CommandOption("-c|--config")]
            public string? ConfigFile { get; set; }

            [CommandOption("--no-progress")]
            public bool NoProgress { get; set; }

            [CommandOption("--token-compact")]
            public bool? TokenCompact { get; set; }

            [CommandOption("--parallel")]
            public bool? Parallel { get; set; }

            [CommandOption("--features")]
            public bool? EnableFeatures { get; set; }

            [CommandOption("--mermaid")]
            public bool? UseMermaid { get; set; }

            [CommandOption("--exclude")]
            public string[]? ExcludePatterns { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var targetDir = Path.GetFullPath(settings.Path);

            if (!Directory.Exists(targetDir))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Directory '{targetDir}' does not exist.");
                return 1;
            }

            // Load configuration
            var options = await LoadConfigurationAsync(settings);

            // Display configuration table
            DisplayConfiguration(options, targetDir);

            // Detect project type
            var detector = new GenericDotNetProjectDetector(options);

            if (!detector.Detect(targetDir))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No .NET project found in the specified directory.");
                return 1;
            }

            // Extract with progress
            ExtractionResult result;

            if (settings.NoProgress)
            {
                AnsiConsole.MarkupLine("[yellow]Extracting context...[/]");
                result = await detector.ExtractAsync(targetDir);
            }
            else
            {
                result = await AnsiConsole.Progress()
                    .AutoClear(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new ElapsedTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[green]Extracting context[/]", maxValue: 100);

                        var progress = new Progress<ExtractionProgress>(p =>
                        {
                            task.Description = $"[green]{p.CurrentTask}[/]";
                            task.Value = p.PercentComplete;

                            if (!string.IsNullOrEmpty(p.CurrentDetail))
                            {
                                AnsiConsole.MarkupLine($"  [grey]{p.CurrentDetail}[/]");
                            }
                        });

                        return await detector.ExtractAsync(targetDir, progress);
                    });
            }

            // Display results summary
            DisplayResultsSummary(result);

            // Save if output file specified
            if (!string.IsNullOrEmpty(settings.OutputFile ?? options.OutputFilePath))
            {
                var outputPath = settings.OutputFile ?? options.OutputFilePath;
                await File.WriteAllTextAsync(outputPath, result.Content);
                AnsiConsole.MarkupLine($"[green]✓[/] Saved to: [blue]{outputPath}[/]");
            }

            return 0;
        }

        private async Task<ExtractionOptions> LoadConfigurationAsync(Settings settings)
        {
            var options = new ExtractionOptions();

            // Try to load config file
            var configFile = settings.ConfigFile ?? "devcontext.json";
            if (File.Exists(configFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(configFile);
                    options = JsonSerializer.Deserialize<ExtractionOptions>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }) ?? options;

                    AnsiConsole.MarkupLine($"[green]✓[/] Loaded config from: [blue]{configFile}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to load config: {ex.Message}");
                }
            }

            // Override with command line options
            if (settings.OutputFile != null)
                options.OutputFilePath = settings.OutputFile;

            if (settings.TokenCompact.HasValue)
                options.TokenCompact = settings.TokenCompact.Value;

            if (settings.Parallel.HasValue)
                options.EnableParallelProcessing = settings.Parallel.Value;

            if (settings.EnableFeatures.HasValue)
                options.EnableFeatureGrouping = settings.EnableFeatures.Value;

            if (settings.UseMermaid.HasValue)
                options.UseMermaidForGraphs = settings.UseMermaid.Value;

            if (settings.ExcludePatterns != null)
                options.ExcludeDirectories.AddRange(settings.ExcludePatterns);

            return options;
        }

        private void DisplayConfiguration(ExtractionOptions options, string targetDir)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold yellow]DevContext Configuration[/]")
                .AddColumn("[blue]Setting[/]")
                .AddColumn("[green]Value[/]");

            table.AddRow("Target Directory", targetDir);
            table.AddRow("Token Compact", options.TokenCompact ? "✓" : "✗");
            table.AddRow("Parallel Processing", options.EnableParallelProcessing ? "✓" : "✗");
            table.AddRow("Feature Grouping", options.EnableFeatureGrouping ? "✓" : "✗");
            table.AddRow("Include Call Graph", options.IncludeCallGraph ? "✓" : "✗");
            table.AddRow("Include Dependencies", options.IncludeDependencyGraph ? "✓" : "✗");
            table.AddRow("Include Domain Model", options.IncludeDomainModel ? "✓" : "✗");
            table.AddRow("Mermaid Graphs", options.UseMermaidForGraphs ? "✓" : "✗");
            table.AddRow("Max Call Depth", options.MaxCallGraphDepth.ToString());
            table.AddRow("Excluded Dirs", string.Join(", ", options.ExcludeDirectories.Take(3)) +
                                          (options.ExcludeDirectories.Count > 3 ? "..." : ""));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        private void DisplayResultsSummary(ExtractionResult result)
        {
            AnsiConsole.WriteLine();

            var panel = new Panel(new Markup(
                $"[bold green]Extraction Complete![/]\n\n" +
                $"[blue]Content Size:[/] {result.Content.Length:N0} characters\n" +
                $"[blue]Lines:[/] {result.Content.Split('\n').Length:N0}\n" +
                $"[blue]Sections:[/] {CountSections(result.Content)}"))
            {
                Header = new PanelHeader("[bold]Results Summary[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2)
            };

            AnsiConsole.Write(panel);
        }

        private int CountSections(string content)
        {
            return content.Split('\n').Count(line => line.StartsWith("#"));
        }
    }

    public class InitCommand : Command<InitCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("-f|--force")]
            public bool Force { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            const string configFile = "devcontext.json";

            if (File.Exists(configFile) && !settings.Force)
            {
                if (!AnsiConsole.Confirm($"[yellow]{configFile}[/] already exists. Overwrite?"))
                {
                    AnsiConsole.MarkupLine("[red]Aborted.[/]");
                    return 1;
                }
            }

            // Interactive configuration
            var options = new ExtractionOptions();

            AnsiConsole.MarkupLine("[bold]DevContext Configuration Wizard[/]");
            AnsiConsole.WriteLine();

            options.TokenCompact = AnsiConsole.Confirm("Enable token compression?", true);
            options.EnableParallelProcessing = AnsiConsole.Confirm("Enable parallel processing?", true);
            options.EnableFeatureGrouping = AnsiConsole.Confirm("Enable feature grouping?", true);

            var extractors = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Select extractors to enable:")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                    .AddChoices(new[]
                    {
                        "Method Signatures",
                        "Dependency Graph",
                        "Call Graph",
                        "Domain Model"
                    })
                    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]"));

            options.IncludeMethodSignatures = extractors.Contains("Method Signatures");
            options.IncludeDependencyGraph = extractors.Contains("Dependency Graph");
            options.IncludeCallGraph = extractors.Contains("Call Graph");
            options.IncludeDomainModel = extractors.Contains("Domain Model");

            options.UseMermaidForGraphs = AnsiConsole.Confirm("Use Mermaid for graphs?", false);

            options.MaxCallGraphDepth = AnsiConsole.Prompt(
                new TextPrompt<int>("Maximum call graph depth:")
                    .DefaultValue(3)
                    .ValidationErrorMessage("[red]Please enter a valid number[/]"));

            // Add exclude patterns
            var excludePatterns = AnsiConsole.Prompt(
                new TextPrompt<string>("Exclude patterns (comma-separated):")
                    .DefaultValue(".git,.vs,bin,obj,node_modules")
                    .AllowEmpty());

            if (!string.IsNullOrWhiteSpace(excludePatterns))
            {
                options.ExcludeDirectories = excludePatterns.Split(',').Select(p => p.Trim()).ToList();
            }

            // Architecture detection
            var architecture = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Preferred architecture style (for auto-detection):")
                    .AddChoices(new[]
                    {
                        "Auto-detect",
                        "MVC",
                        "Minimal API",
                        "Blazor",
                        "Fast Endpoints",
                        "Clean Architecture",
                        "Vertical Slice",
                        "Modular"
                    }));

            if (architecture != "Auto-detect")
            {
                options.DetectedArchitecture = Enum.Parse<ArchitectureStyle>(
                    architecture.Replace(" ", ""), true);
            }

            // Save configuration
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(options, jsonOptions);
            File.WriteAllText(configFile, json);

            AnsiConsole.MarkupLine($"[green]✓[/] Configuration saved to: [blue]{configFile}[/]");

            // Display example
            var panel = new Panel(
                new Markup(
                    "[yellow]Example usage:[/]\n\n" +
                    "[blue]devcontext extract[/] - Extract using config\n" +
                    "[blue]devcontext extract . -o context.md[/] - Extract to file\n" +
                    "[blue]devcontext extract --no-progress[/] - Without progress bar"))
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(2)
            };

            AnsiConsole.Write(panel);

            return 0;
        }
    }

    public class DetectCommand : AsyncCommand<DetectCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[PATH]")]
            public string Path { get; set; } = ".";
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var targetDir = Path.GetFullPath(settings.Path);

            if (!Directory.Exists(targetDir))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Directory '{targetDir}' does not exist.");
                return 1;
            }

            AnsiConsole.Status()
                .Start("[yellow]Analyzing project structure...[/]", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                });

            // Load solution
            var slnPath = Directory.EnumerateFiles(targetDir, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (slnPath == null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No solution file found.");
                return 1;
            }

            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(slnPath);

            // Detect features
            var detector = new FeatureDetector(new ExtractionOptions());
            var features = await detector.DetectFeaturesAsync(solution);

            // Display results
            AnsiConsole.Clear();

            var tree = new Tree($"[bold yellow]{Path.GetFileName(slnPath)}[/]")
                .Style(Style.Parse("blue"));

            tree.AddNode($"[green]Architecture:[/] [bold]{features.Architecture}[/]");
            tree.AddNode($"[green]Projects:[/] {solution.Projects.Count()}");

            var featuresNode = tree.AddNode($"[green]Features:[/] {features.Features.Count}");

            foreach (var feature in features.Features.Take(10))
            {
                var featureNode = featuresNode.AddNode($"[yellow]{feature.Key}[/]");

                if (feature.Value.Endpoints.Any())
                {
                    var endpointsNode = featureNode.AddNode($"Endpoints: {feature.Value.Endpoints.Count}");
                    foreach (var endpoint in feature.Value.Endpoints.Take(3))
                    {
                        endpointsNode.AddNode($"[grey]{endpoint.HttpMethod} {endpoint.Route}[/]");
                    }
                }

                if (feature.Value.UseCases.Any())
                {
                    var useCasesNode = featureNode.AddNode($"Use Cases: {feature.Value.UseCases.Count}");
                    foreach (var useCase in feature.Value.UseCases.Take(3))
                    {
                        useCasesNode.AddNode($"[grey]{useCase.Name}[/]");
                    }
                }
            }

            if (features.Features.Count > 10)
            {
                featuresNode.AddNode($"[grey]... and {features.Features.Count - 10} more[/]");
            }

            AnsiConsole.Write(tree);

            // Recommendations
            var recommendations = GenerateRecommendations(features);
            if (recommendations.Any())
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(string.Join("\n", recommendations))
                {
                    Header = new PanelHeader("[bold]Recommendations[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("yellow")
                };
                AnsiConsole.Write(panel);
            }

            return 0;
        }

        private List<string> GenerateRecommendations(FeatureGrouping features)
        {
            var recommendations = new List<string>();

            switch (features.Architecture)
            {
                case ArchitectureStyle.MinimalApi:
                    recommendations.Add("• Consider grouping endpoints by feature modules");
                    recommendations.Add("• Use endpoint filters for cross-cutting concerns");
                    break;

                case ArchitectureStyle.MVC:
                    recommendations.Add("• Consider using areas for feature organization");
                    recommendations.Add("• Group related controllers in feature folders");
                    break;

                case ArchitectureStyle.CleanArchitecture:
                    recommendations.Add("• Ensure clear separation between layers");
                    recommendations.Add("• Consider using MediatR for command/query handling");
                    break;

                case ArchitectureStyle.VerticalSlice:
                    recommendations.Add("• Keep features self-contained");
                    recommendations.Add("• Minimize cross-feature dependencies");
                    break;

                case ArchitectureStyle.FastEndpoints:
                    recommendations.Add("• Use endpoint groups for related features");
                    recommendations.Add("• Consider using pre/post processors");
                    break;
            }

            if (features.Features.Count > 20)
            {
                recommendations.Add($"• With {features.Features.Count} features, consider module boundaries");
            }

            return recommendations;
        }
    }
}

