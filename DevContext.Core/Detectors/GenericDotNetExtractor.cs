using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DevContext.Core;
using DevContext.Core.Extractors;
using Microsoft.Build.Locator;

// Note: Some extractor classes are still defined in this file during incremental refactoring.
// They will be moved to DevContext.Core.Extractors in follow-up passes.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevContext.Core
{
    public class GenericDotNetProjectDetector : IProjectDetector
    {
        private readonly ExtractionOptions _options;
        private readonly Stopwatch _stopwatch = new();

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
            _stopwatch.Restart();
            var sb = new StringBuilder();

            // Initialize progress tracking
            var extractionProgress = new ExtractionProgress
            {
                TotalTasks = CountEnabledTasks(),
                CompletedTasks = 0
            };

            // Initialize MSBuild
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            // Header
            sb.AppendLine("# DevContext - .NET Project Analysis");
            sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Profile**: Depth={_options.Depth}, Focus={_options.Focus}" +
                          (_options.FocusedFeatures.Any() ? $" (features: {string.Join(", ", _options.FocusedFeatures)})" : ""));
            sb.AppendLine($"**Token-Compact**: {(_options.TokenCompact ? "ON" : "OFF")}");
            sb.AppendLine();

            // Load workspace and solution
            var slnPath = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            MSBuildWorkspace? workspace = null;
            Solution? solution = null;

            UpdateProgress(progress, extractionProgress, "Loading solution...", 5);

            if (slnPath != null)
            {
                try
                {
                    workspace = MSBuildWorkspace.Create();
                    solution = await workspace.OpenSolutionAsync(slnPath);

                    // Auto-detect architecture if not set
                    if (_options.DetectedArchitecture == ArchitectureStyle.Unknown)
                    {
                        var featureDetector = new FeatureDetector(_options);
                        var featureGrouping = await featureDetector.DetectFeaturesAsync(solution);
                        _options.DetectedArchitecture = featureGrouping.Architecture;
                    }
                }
                catch (Exception ex)
                {
                    if (_options.VerboseOutput)
                        Console.WriteLine($"Warning: Failed to load solution: {ex.Message}");
                }
            }

            // Apply depth + focus rules (new v1 behavior for better LLM prompt output)
            ApplyDepthAndFocusRules();

            // Run extractors in parallel if enabled
            var extractorTasks = new List<Task<(string name, string content)>>();

            // 1. Solution Overview
            extractorTasks.Add(RunExtractorAsync("Solution Overview", async () =>
            {
                var overviewExtractor = new SolutionOverviewExtractor(_options);
                return await overviewExtractor.ExtractAsync(directory, solution);
            }, progress, extractionProgress));

            // 2. Feature Grouping (if enabled)
            if (_options.EnableFeatureGrouping && solution != null)
            {
                extractorTasks.Add(RunExtractorAsync("Feature Analysis", async () =>
                {
                    var featureDetector = new FeatureDetector(_options);
                    var features = await featureDetector.DetectFeaturesAsync(solution);
                    return FormatFeatureGrouping(features);
                }, progress, extractionProgress));
            }

            // New: Software Layer Summary for architecture focus (high value for LLM prompts)
            if (_options.Focus == ExtractionFocus.Architecture || _options.Depth == ExtractionDepth.Shallow)
            {
                extractorTasks.Add(RunExtractorAsync("Software Layers", async () =>
                {
                    return GenerateLayerSummary(directory, solution);
                }, progress, extractionProgress));
            }

            // 3. Dependency Graph
            if (_options.IncludeDependencyGraph)
            {
                extractorTasks.Add(RunExtractorAsync("Dependency Graph", async () =>
                {
                    var depExtractor = new DependencyGraphExtractor(_options);
                    return await depExtractor.ExtractAsync(directory, solution);
                }, progress, extractionProgress));
            }

            // 4. Call Graph
            if (_options.IncludeCallGraph && solution != null)
            {
                extractorTasks.Add(RunExtractorAsync("Call Graph", async () =>
                {
                    var callExtractor = new CallGraphExtractor(_options);
                    return await callExtractor.ExtractAsync(solution);
                }, progress, extractionProgress));
            }

            // 5. Code Structure
            extractorTasks.Add(RunExtractorAsync("Code Structure", async () =>
            {
                var codeExtractor = new CodeStructureExtractor(_options);
                return await codeExtractor.ExtractAsync(directory, solution);
            }, progress, extractionProgress));

            // 6. Domain Model
            if (_options.IncludeDomainModel)
            {
                extractorTasks.Add(RunExtractorAsync("Domain Model", async () =>
                {
                    var domainExtractor = new DomainModelExtractor(_options);
                    return await domainExtractor.ExtractAsync(directory, solution);
                }, progress, extractionProgress));
            }

            // Wait for all extractors to complete
            var results = _options.EnableParallelProcessing
                ? await Task.WhenAll(extractorTasks)
                : await RunSequentialAsync(extractorTasks);

            // Combine results
            foreach (var (name, content) in results.OrderBy(r => GetExtractorOrder(r.name)))
            {
                sb.Append(content);
            }

            // Apply token compression if enabled
            string finalContent = sb.ToString();
            if (_options.TokenCompact)
            {
                UpdateProgress(progress, extractionProgress, "Compressing output...", 95);
                var compressor = new TokenCompressor();
                finalContent = await Task.Run(() => compressor.Compress(finalContent));
            }

            // Add timing information if enabled
            if (_options.ShowElapsedTime)
            {
                _stopwatch.Stop();
                finalContent += $"\n\n---\n**Total Time**: {_stopwatch.Elapsed.TotalSeconds:F2}s";

                if (_options.ShowMemoryUsage)
                {
                    var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    finalContent += $"\n**Memory Used**: {memoryMB}MB";
                }
            }

            // Save to file if specified
            var result = new ExtractionResult("generic-dotnet", finalContent);

            if (!string.IsNullOrEmpty(_options.OutputFilePath))
            {
                try
                {
                    await File.WriteAllTextAsync(_options.OutputFilePath, result.Content);
                    Console.WriteLine($"✅ Context saved to: {_options.OutputFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to save: {ex.Message}");
                }
            }

            UpdateProgress(progress, extractionProgress, "Complete!", 100);
            workspace?.Dispose();
            return result;
        }

        private async Task<(string name, string content)> RunExtractorAsync(
            string name,
            Func<Task<string>> extractor,
            IProgress<ExtractionProgress>? progress,
            ExtractionProgress extractionProgress)
        {
            UpdateProgress(progress, extractionProgress, $"Extracting {name}...",
                (extractionProgress.CompletedTasks * 100) / extractionProgress.TotalTasks);

            var content = await extractor();


            Interlocked.Increment(ref extractionProgress.CompletedTasks);
            extractionProgress.ElapsedTime = _stopwatch.Elapsed;

            return (name, content);
        }

        private async Task<(string name, string content)[]> RunSequentialAsync(
            List<Task<(string name, string content)>> tasks)
        {
            var results = new List<(string name, string content)>();
            foreach (var task in tasks)
            {
                results.Add(await task);
            }
            return results.ToArray();
        }

        private int CountEnabledTasks()
        {
            int count = 2; // Overview and Code Structure are always enabled
            if (_options.EnableFeatureGrouping)
                count++;
            if (_options.IncludeDependencyGraph)
                count++;
            if (_options.IncludeCallGraph)
                count++;
            if (_options.IncludeDomainModel)
                count++;
            return count;
        }

        private int GetExtractorOrder(string name)
        {
            return name switch
            {
                "Solution Overview" => 1,
                "Feature Analysis" => 2,
                "Dependency Graph" => 3,
                "Call Graph" => 4,
                "Code Structure" => 5,
                "Domain Model" => 6,
                _ => 99
            };
        }

        /// <summary>
        /// Applies the new Depth + Focus model to automatically adjust what gets extracted.
        /// This is the core mechanism for producing output that is actually good to attach to LLM prompts.
        /// </summary>
        private void ApplyDepthAndFocusRules()
        {
            // === Depth rules (stronger for v1) ===
            switch (_options.Depth)
            {
                case ExtractionDepth.Shallow:
                    _options.IncludeCallGraph = false;
                    _options.IncludeDomainModel = false;
                    _options.IncludeMethodSignatures = false;
                    _options.MaxCallGraphDepth = 0;
                    // Keep high-value architecture signals
                    _options.EnableFeatureGrouping = true;
                    _options.IncludeDependencyGraph = true;
                    break;

                case ExtractionDepth.Deep:
                    _options.IncludeCallGraph = true;
                    _options.IncludeDomainModel = true;
                    _options.IncludeMethodSignatures = true;
                    _options.MaxCallGraphDepth = Math.Max(_options.MaxCallGraphDepth, 6);
                    break;

                case ExtractionDepth.Balanced:
                default:
                    if (_options.MaxCallGraphDepth == int.MaxValue)
                        _options.MaxCallGraphDepth = 3;
                    break;
            }

            // === Focus rules ===
            switch (_options.Focus)
            {
                case ExtractionFocus.Architecture:
                    _options.EnableFeatureGrouping = true;
                    _options.IncludeDependencyGraph = true;
                    if (_options.Depth == ExtractionDepth.Shallow)
                    {
                        _options.IncludeCallGraph = false;
                        _options.IncludeMethodSignatures = false;
                    }
                    break;

                case ExtractionFocus.Feature:
                    _options.EnableFeatureGrouping = true;
                    if (_options.Depth != ExtractionDepth.Shallow)
                        _options.IncludeCallGraph = true;
                    break;

                case ExtractionFocus.Debug:
                case ExtractionFocus.Implementation:
                    _options.IncludeCallGraph = true;
                    _options.IncludeDomainModel = true;
                    _options.IncludeMethodSignatures = true;
                    break;
            }

            // When user explicitly focuses on features, reduce global noise
            if (_options.Focus == ExtractionFocus.Feature && _options.FocusedFeatures.Any())
            {
                // In future we will filter extractors more aggressively to selected features
            }
        }

        private void UpdateProgress(
            IProgress<ExtractionProgress>? progress,
            ExtractionProgress state,
            string task,
            double percent)
        {
            if (progress == null)
                return;

            state.CurrentTask = task;
            state.PercentComplete = percent;
            state.ElapsedTime = _stopwatch.Elapsed;
            progress.Report(state);
        }

        private string FormatFeatureGrouping(FeatureGrouping grouping)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Feature Analysis");
            sb.AppendLine($"**Architecture Style**: {grouping.Architecture}");
            sb.AppendLine($"**Features Found**: {grouping.Features.Count}");
            sb.AppendLine();

            foreach (var feature in grouping.Features.OrderBy(f => f.Key))
            {
                sb.AppendLine($"## {feature.Key}");

                if (feature.Value.Endpoints.Any())
                {
                    sb.AppendLine("### Endpoints");
                    foreach (var endpoint in feature.Value.Endpoints)
                    {
                        var route = endpoint.Route ?? "N/A";
                        var method = endpoint.HttpMethod ?? "N/A";
                        sb.AppendLine($"- `{method} {route}` - {endpoint.Name}");
                    }
                }

                if (feature.Value.UseCases.Any())
                {
                    sb.AppendLine("### Use Cases");
                    foreach (var useCase in feature.Value.UseCases)
                    {
                        sb.AppendLine($"- {useCase.Name} ({useCase.Type})");
                    }
                }

                if (feature.Value.Files.Any() && feature.Value.Files.Count <= 10)
                {
                    sb.AppendLine("### Files");
                    foreach (var file in feature.Value.Files)
                    {
                        sb.AppendLine($"- {file}");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a high-level software layer summary. Very useful when attaching context to LLM prompts
        /// for architecture discussions. Now project-aware for much better signal on real solutions.
        /// </summary>
        private string GenerateLayerSummary(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Software Layers");

            // Project-aware layer classification (much higher value)
            var projectLayers = new Dictionary<string, Dictionary<string, int>>();

            if (solution != null)
            {
                foreach (var project in solution.Projects)
                {
                    var projName = project.Name;
                    projectLayers[projName] = new Dictionary<string, int>();

                    foreach (var doc in project.Documents)
                    {
                        if (string.IsNullOrEmpty(doc.FilePath)) continue;
                        if (_options.ExcludeDirectories.Any(ex => doc.FilePath.Contains(ex))) continue;

                        var relative = Path.GetRelativePath(directory, doc.FilePath);
                        var layer = ClassifyLayer(relative, doc.FilePath);

                        if (!projectLayers[projName].ContainsKey(layer))
                            projectLayers[projName][layer] = 0;
                        projectLayers[projName][layer]++;
                    }
                }
            }
            else
            {
                // Fallback to file walk
                var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !_options.ExcludeDirectories.Any(ex => f.Contains(ex)))
                    .ToList();

                projectLayers["Solution"] = new Dictionary<string, int>();
                foreach (var file in csFiles)
                {
                    var relative = Path.GetRelativePath(directory, file);
                    var layer = ClassifyLayer(relative, file);
                    if (!projectLayers["Solution"].ContainsKey(layer))
                        projectLayers["Solution"][layer] = 0;
                    projectLayers["Solution"][layer]++;
                }
            }

            foreach (var (project, layers) in projectLayers.OrderBy(p => p.Key))
            {
                if (layers.Count == 0) continue;

                sb.AppendLine($"## {project}");
                var total = layers.Values.Sum();
                sb.AppendLine($"**Total files analyzed**: {total}");
                foreach (var (layer, count) in layers.OrderBy(l => GetLayerOrder(l.Key)))
                {
                    var pct = total > 0 ? (count * 100.0 / total) : 0;
                    sb.AppendLine($"- **{layer}** — {count} files ({pct:F0}%)");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string ClassifyLayer(string relativePath, string fullPath)
        {
            var lower = relativePath.ToLowerInvariant();
            var fileName = Path.GetFileName(fullPath).ToLowerInvariant();

            // Strong project/folder signals first (best for real solutions)
            if (lower.Contains("/domain/") || lower.Contains("\\domain\\") || lower.Contains(".domain"))
                return "Domain";
            if (lower.Contains("/application/") || lower.Contains("\\application\\") || lower.Contains(".application"))
                return "Application";
            if (lower.Contains("/infrastructure/") || lower.Contains("\\infrastructure\\") || lower.Contains(".infrastructure"))
                return "Infrastructure";
            if (lower.Contains("/web/") || lower.Contains("\\web\\") || lower.Contains("/api/") || lower.Contains("/controllers/") || lower.Contains("/endpoints/"))
                return "Presentation / API";
            if (lower.Contains("/features/") || lower.Contains("\\features\\") || lower.Contains("/slices/") || lower.Contains("/modules/"))
                return "Vertical Slices / Features";

            // File-level heuristics
            if (fileName.Contains("controller") || fileName.Contains("endpoint") || fileName.Contains("minimalapi"))
                return "Presentation / API";
            if (fileName.EndsWith("entity.cs") || fileName.EndsWith("aggregate.cs") || fileName.EndsWith("valueobject.cs"))
                return "Domain";
            if (fileName.Contains("handler") || fileName.Contains("command") || fileName.Contains("query") || fileName.Contains("usecase"))
                return "Application";

            if (lower.Contains("/cli/") || lower.Contains("\\cli\\") || fileName == "program.cs")
                return "Entry Point / CLI";
            if (lower.Contains("/build/") || lower.Contains("/tests/") || lower.Contains("/specs/") || lower.Contains(".tests"))
                return "Build / Tests";

            return "Core / Shared";
        }

        private int GetLayerOrder(string layer)
        {
            return layer switch
            {
                "Domain" => 1,
                "Application" => 2,
                "Infrastructure" => 3,
                "Presentation / API" => 4,
                "Vertical Slices / Features" => 5,
                "Entry Point / CLI" => 10,
                "Build / Tests" => 20,
                _ => 99
            };
        }
    }

    public class DependencyGraphExtractor
    {
        private readonly ExtractionOptions _options;

        public DependencyGraphExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Dependency Graph");

            var projectDeps = new ConcurrentDictionary<string, List<string>>();
            var projectPackages = new ConcurrentDictionary<string, List<string>>();

            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ex => f.Contains(ex)))
                .ToList();

            var tasks = csprojFiles.Select(async csproj =>
            {
                try
                {
                    using var stream = File.OpenRead(csproj);
                    var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
                    var projectName = Path.GetFileNameWithoutExtension(csproj);

                    var deps = new List<string>();
                    var packages = new List<string>();

                    // Project references
                    foreach (var projRef in doc.Descendants("ProjectReference"))
                    {
                        var refPath = projRef.Attribute("Include")?.Value;
                        if (!string.IsNullOrEmpty(refPath))
                        {
                            var refName = Path.GetFileNameWithoutExtension(refPath);
                            deps.Add(refName);
                        }
                    }

                    // NuGet packages - filter out noise
                    foreach (var pkg in doc.Descendants("PackageReference"))
                    {
                        var name = pkg.Attribute("Include")?.Value;
                        var ver = pkg.Attribute("Version")?.Value;
                        if (!string.IsNullOrEmpty(name) && !IsNoisePackage(name))
                        {
                            packages.Add($"{name}@{ver ?? "latest"}");
                        }
                    }

                    projectDeps[projectName] = deps;
                    projectPackages[projectName] = packages;
                }
                catch (Exception ex)
                {
                    if (_options.VerboseOutput)
                        Console.WriteLine($"Warning: Error reading {csproj}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            if (_options.UseMermaidForGraphs)
            {
                // Mermaid format
                sb.AppendLine("```mermaid");
                sb.AppendLine("graph LR");
                foreach (var kvp in projectDeps.Where(p => p.Value.Any()))
                {
                    foreach (var dep in kvp.Value)
                    {
                        sb.AppendLine($"  {SanitizeForMermaid(kvp.Key)} --> {SanitizeForMermaid(dep)}");
                    }
                }
                sb.AppendLine("```");
            }
            else
            {
                // Plain text format
                sb.AppendLine("## Project Dependencies");
                foreach (var kvp in projectDeps.OrderBy(p => p.Key))
                {
                    if (kvp.Value.Any())
                    {
                        sb.AppendLine($"{kvp.Key} -> {string.Join(", ", kvp.Value)}");
                    }
                }

                // Top NuGet packages (filtered)
                var allPackages = projectPackages.SelectMany(p => p.Value)
                    .GroupBy(p => p.Split('@')[0])
                    .Where(g => g.Count() > 1) // Only show packages used by multiple projects
                    .OrderByDescending(g => g.Count())
                    .Take(int.MaxValue); // Limit to top 10

                if (allPackages.Any())
                {
                    sb.AppendLine("\n## Top Shared NuGet Packages");
                    foreach (var pkg in allPackages)
                    {
                        sb.AppendLine($"- {pkg.Key} (used by {pkg.Count()} projects)");
                    }
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private bool IsNoisePackage(string packageName)
        {
            var noisePatterns = new[]
            {
                "Microsoft.NET.Test.Sdk",
                "xunit",
                "coverlet",
                "Microsoft.SourceLink",
                "Microsoft.CodeAnalysis.Analyzers",
                "StyleCop",
                "SonarAnalyzer"
            };

            return noisePatterns.Any(pattern => packageName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string SanitizeForMermaid(string name)
        {
            return name.Replace(".", "_").Replace("-", "_");
        }
    }

    public class CodeStructureExtractor
    {
        private readonly ExtractionOptions _options;

        public CodeStructureExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Code Structure");

            // File structure
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)))
                .Select(f => Path.GetRelativePath(directory, f))
                .OrderBy(f => f)
                .ToList();

            sb.AppendLine($"**Total Files**: {csFiles.Count} .cs");

            // Group by directory with limit
            var filesByDirectory = csFiles.GroupBy(f => Path.GetDirectoryName(f) ?? "")
                .OrderBy(g => g.Key)
                .Take(int.MaxValue); // Limit directories shown

            foreach (var group in filesByDirectory)
            {
                var dirName = string.IsNullOrEmpty(group.Key) ? "Root" : group.Key;
                var files = group.Take(5).Select(Path.GetFileName).ToList();
                var fileList = string.Join(", ", files);

                if (group.Count() > 5)
                    fileList += $" ... (+{group.Count() - 5} more)";

                sb.AppendLine($"**{dirName}**: {fileList}");
            }

            // Method signatures if requested
            if (_options.IncludeMethodSignatures)
            {
                sb.AppendLine("\n## Key Method Signatures");
                await ExtractMethodSignaturesAsync(sb, directory);
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private async Task ExtractMethodSignaturesAsync(StringBuilder sb, string directory)
        {
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)))
                .Take(int.MaxValue); // Limit files analyzed

            var tasks = csFiles.Select(async file =>
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();
                    var relativePath = Path.GetRelativePath(directory, file);

                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .Where(m => m.Modifiers.Any(mod => mod.Text == "public") &&
                                   !IsTrivialMethod(m.Identifier.Text))
                        .Take(int.MaxValue); // Limit methods per file

                    if (!methods.Any())
                        return (string.Empty, new List<string>());


                    var signatures = new List<string>();
                    foreach (var method in methods)
                    {
                        var modifiers = string.Join(" ", method.Modifiers.Select(m => m.ValueText));
                        var parameters = _options.TokenCompact
                            ? string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var"))
                            : string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));

                        signatures.Add($"- {modifiers} {method.ReturnType} {method.Identifier}({parameters})");
                    }

                    return (relativePath, signatures);
                }
                catch
                {
                    return (string.Empty, new List<string>());

                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results.Where(r => r.Item2.Any()))
            {
                sb.AppendLine($"\n### {result.Item1}");
                foreach (var sig in result.Item2)
                {
                    sb.AppendLine(sig);
                }
            }
        }



        private bool IsTrivialMethod(string methodName)
        {
            return _options.TrivialMethodPatterns.Any(pattern =>
                Regex.IsMatch(methodName, pattern.Replace("*", ".*")));
        }
    }

}
