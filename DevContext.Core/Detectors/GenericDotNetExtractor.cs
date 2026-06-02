using System.Diagnostics;
using DevContext.Core.Analysis;
using DevContext.Core.Extractors;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevContext.Core
{
    /// <summary>
    /// Main orchestrator for .NET solution context extraction.
    /// Now delegates to the structured AnalysisPipeline (typed IR → scoring → rendering).
    /// </summary>
    public class GenericDotNetProjectDetector : IProjectDetector
    {
        private readonly ExtractionOptions _options;

        public GenericDotNetProjectDetector(ExtractionOptions? options = null)
        {
            _options = options ?? new ExtractionOptions();
        }

        public string Id => "generic-dotnet";

        public bool Detect(string directory)
        {
            return Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).Any() ||
                   Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any();
        }

        public async Task<ExtractionResult> ExtractAsync(string directory, IProgress<ExtractionProgress>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();

            if (_options.DetectedArchitecture == ArchitectureStyle.Unknown)
                await TryDetectArchitectureAsync(directory);

            _options.ApplyDepthAndFocusRules();

            var pipeline = new AnalysisPipeline(_options);
            var result = await pipeline.ExecuteAsync(directory, progress);

            stopwatch.Stop();

            if (_options.ShowElapsedTime)
            {
                var content = result.Content;
                content += $"\n\n---\n**Total Time**: {stopwatch.Elapsed.TotalSeconds:F2}s";
                if (_options.ShowMemoryUsage)
                {
                    var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    content += $"\n**Memory Used**: {memoryMB}MB";
                }
                return new ExtractionResult(result.Id, content);
            }

            return result;
        }

        private async Task TryDetectArchitectureAsync(string directory)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            try
            {
                using var workspace = MSBuildWorkspace.Create();
                var slnPath = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (slnPath == null) return;

                var solution = await workspace.OpenSolutionAsync(slnPath);
                var detector = new FeatureDetector(_options);
                var features = await detector.DetectFeaturesAsync(solution);
                _options.DetectedArchitecture = features.Architecture;
            }
            catch
            {
            }
        }
    }
}
