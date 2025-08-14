using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using DevContext.Core;
using DevContext.Core.Extractors;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevContext.Cli
{
    public static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandApp<DevContextCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("devcontext");
                config.AddExample(new[] { "." });
                config.AddExample(new[] { ".", "-s", "-d", "-c" });
                config.AddExample(new[] { ".", "-o", "context.md" });
                config.AddExample(new[] { ".", "--feature", "Advertisements" });
            });
            return await app.RunAsync(args);
        }
    }

    public class DevContextCommand : AsyncCommand<DevContextCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[directory]")]
            //[DefaultValue(@"c:\code\github\dntsite")]
            [DefaultValue(@"c:\code\DevContext")]
            public string? Directory { get; set; }

            [CommandOption("-s|--include-signatures")]
            [DefaultValue(true)]
            public bool IncludeSignatures { get; set; }

            [CommandOption("-d|--include-dependency-graph")]
            [DefaultValue(true)]
            public bool IncludeDependencyGraph { get; set; } = true;

            [CommandOption("-c|--include-call-graph")]
            [DefaultValue(false)]
            public bool IncludeCallGraph { get; set; } = true;

            [CommandOption("-m|--include-domain-model")]
            [DefaultValue(true)]

            public bool IncludeDomainModel { get; set; } = true;

            [CommandOption("-a|--include-architecture")]
            [DefaultValue(true)]
            public bool IncludeArchitecture { get; set; } = true;

            [CommandOption("--mermaid-graph")]
            [DefaultValue(false)]
            public bool MermaidGraph { get; set; }

            [CommandOption("--token-compact")]
            [DefaultValue(true)]
            public bool TokenCompact { get; set; } = true;

            [CommandOption("--no-token-compact")]
            [DefaultValue(true)]

            public bool NoTokenCompact { get; set; }

            [CommandOption("-o|--output")]
            public string? Output { get; set; }

            [CommandOption("-v|--verbose")]
            [DefaultValue(true)]

            public bool Verbose { get; set; }

            [CommandOption("--feature")]
            public string? FeatureFilter { get; set; }

            [CommandOption("--entry-point")]
            public string? EntryPointFilter { get; set; }

            [CommandOption("--max-depth")]
            public int MaxDepth { get; set; } = 3;

            [CommandOption("--include-trivial-methods")]
            public bool IncludeTrivialMethods { get; set; }

            [CommandOption("--include-ef-boilerplate")]
            public bool IncludeEFBoilerplate { get; set; }

            [CommandOption("--include-blazor-lifecycle")]
            public bool IncludeBlazorLifecycle { get; set; }

            [CommandOption("--no-feature-grouping")]
            public bool NoFeatureGrouping { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var targetDir = settings.Directory ?? Environment.CurrentDirectory;

            var options = new ExtractionOptions
            {
                TokenCompact = settings.TokenCompact && !settings.NoTokenCompact,
                IncludeMethodSignatures = settings.IncludeSignatures,
                IncludeDependencyGraph = settings.IncludeDependencyGraph,
                IncludeCallGraph = settings.IncludeCallGraph,
                IncludeDomainModel = settings.IncludeDomainModel,
                IncludeArchitectureView = settings.IncludeArchitecture,
                UseMermaidForGraphs = settings.MermaidGraph,
                VerboseOutput = settings.Verbose,
                OutputFilePath = settings.Output,
                FeatureFilter = settings.FeatureFilter,
                EntryPointFilter = settings.EntryPointFilter,
                MaxDepth = settings.MaxDepth,
                ExcludeTrivialMethods = !settings.IncludeTrivialMethods,
                ExcludeEFBoilerplate = !settings.IncludeEFBoilerplate,
                ExcludeBlazorLifecycle = !settings.IncludeBlazorLifecycle,
                GroupByFeature = !settings.NoFeatureGrouping
            };

            // Validate directory
            if (!Directory.Exists(targetDir))
            {
                AnsiConsole.MarkupLine($"[red]❌ Directory not found: {targetDir}[/]");
                return 1;
            }

            IProjectDetector detector = new GenericDotNetProjectDetector(options);

            AnsiConsole.MarkupLine($"[blue]🔍 Analyzing directory: {targetDir}[/]");

            if (!detector.Detect(targetDir))
            {
                AnsiConsole.MarkupLine("[red]❌ No .NET project detected.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]✅ .NET project detected. Extracting context...[/]");

            // Show extraction options
            var table = new Table();
            table.AddColumn("Option");
            table.AddColumn("Value");

            table.AddRow("Method signatures", options.IncludeMethodSignatures ? "Included" : "Excluded");
            table.AddRow("Dependency graph", options.IncludeDependencyGraph ? "Included" : "Excluded");
            table.AddRow("Call graph", options.IncludeCallGraph ? "Included" : "Excluded");
            table.AddRow("Domain model", options.IncludeDomainModel ? "Included" : "Excluded");
            table.AddRow("Architecture view", options.IncludeArchitectureView ? "Included" : "Excluded");
            table.AddRow("Token compact", options.TokenCompact ? "Enabled" : "Disabled");
            table.AddRow("Feature filter", options.FeatureFilter ?? "None");
            table.AddRow("Entry point filter", options.EntryPointFilter ?? "None");
            table.AddRow("Noise reduction",
                $"{(options.ExcludeTrivialMethods ? "Trivial methods" : "")} " +
                $"{(options.ExcludeEFBoilerplate ? "EF boilerplate" : "")} " +
                $"{(options.ExcludeBlazorLifecycle ? "Blazor lifecycle" : "")}");

            AnsiConsole.Write(table);

            var result = await detector.ExtractAsync(targetDir);

            // Output results
            if (string.IsNullOrEmpty(options.OutputFilePath))
            {
                AnsiConsole.MarkupLine("\n[bold]===== Extraction Result =====[/]");
                AnsiConsole.MarkupLine(result.Content);
            }
            else
            {
                var lines = result.Content.Split('\n').Length;
                var sizeKb = System.Text.Encoding.UTF8.GetByteCount(result.Content) / 1024.0;
                AnsiConsole.MarkupLine($"\n[green]📊 Generated {lines:N0} lines ({sizeKb:F1} KB)[/]");
            }

            return 0;
        }
    }
}
