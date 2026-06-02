using DevContext.Core.Models;
using DevContext.Core.Scoring;
using DevContext.Core.Rendering;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevContext.Core.Analysis;

public sealed class AnalysisPipeline
{
    private readonly ExtractionOptions _options;
    private readonly List<IAnalyzer> _analyzers;

    public AnalysisPipeline(ExtractionOptions options)
    {
        _options = options;

        _analyzers = new List<IAnalyzer>
        {
            new SolutionAnalyzer(options),
            new DependencyAnalyzer(),
            new LayerAnalyzer(options),
            new CallGraphAnalyzer(options),
        };
    }

    public async Task<PipelineResult> ExecuteAsync(string directory, IProgress<ExtractionProgress>? progress = null)
    {
        var model = new CodeModel
        {
            RootDirectory = directory
        };

        // Load Roslyn workspace (optional — analyzers fall back to csproj scanning)
        var slnPath = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (slnPath != null)
        {
            try
            {
                if (!MSBuildLocator.IsRegistered)
                    MSBuildLocator.RegisterDefaults();

                using var workspace = MSBuildWorkspace.Create();
                workspace.LoadMetadataForReferencedProjects = false;
                workspace.SkipUnrecognizedProjects = true;

                var openTask = workspace.OpenSolutionAsync(slnPath);
                if (await Task.WhenAny(openTask, Task.Delay(TimeSpan.FromSeconds(15))) == openTask)
                {
                    model.Solution = openTask.Result;
                }
            }
            catch
            {
            }
        }

        // Phase 1: Run all analyzers
        foreach (var analyzer in _analyzers)
        {
            progress?.Report(new ExtractionProgress
            {
                CurrentTask = $"Analyzing {analyzer.Name}...",
                PercentComplete = 0
            });

            await analyzer.AnalyzeAsync(model);
        }

        // Phase 2: Score by relevance
        progress?.Report(new ExtractionProgress
        {
            CurrentTask = "Scoring relevance...",
            PercentComplete = 70
        });

        var scorer = new RelevanceScorer(_options);
        scorer.Score(model);

        // Phase 3: Apply budget
        var filter = new RelevanceFilter(_options);
        filter.ApplyBudget(model);

        // Phase 4: Render
        progress?.Report(new ExtractionProgress
        {
            CurrentTask = "Rendering output...",
            PercentComplete = 85
        });

        var renderer = new MarkdownRenderer();
        var content = await renderer.RenderAsync(model, _options);

        // Save to file if specified
        if (!string.IsNullOrEmpty(_options.OutputFilePath))
        {
            await File.WriteAllTextAsync(_options.OutputFilePath, content);
        }

        progress?.Report(new ExtractionProgress
        {
            CurrentTask = "Complete!",
            PercentComplete = 100
        });

        return new PipelineResult
        {
            Model = model,
            ExtractionResult = new ExtractionResult("generic-dotnet", content)
        };
    }
}
