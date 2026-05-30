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
            sb.AppendLine($"**Token-Compact**: {(_options.TokenCompact ? "ON" : "OFF")}");
            sb.AppendLine($"**Architecture**: {_options.DetectedArchitecture}");
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
        /// for architecture discussions.
        /// </summary>
        private string GenerateLayerSummary(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Software Layers");

            var layerGroups = new Dictionary<string, List<string>>();

            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ex => f.Contains(ex)))
                .ToList();

            foreach (var file in csFiles)
            {
                var relative = Path.GetRelativePath(directory, file);
                var layer = ClassifyLayer(relative, file);

                if (!layerGroups.ContainsKey(layer))
                    layerGroups[layer] = new List<string>();

                var fileName = Path.GetFileName(file);
                if (layerGroups[layer].Count < 8)
                    layerGroups[layer].Add(fileName);
            }

            foreach (var (layer, files) in layerGroups.OrderBy(k => GetLayerOrder(k.Key)))
            {
                sb.AppendLine($"## {layer}");
                sb.AppendLine($"Files: {string.Join(", ", files)}" + (files.Count >= 8 ? " ..." : ""));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string ClassifyLayer(string relativePath, string fullPath)
        {
            var lower = relativePath.ToLowerInvariant();

            if (lower.Contains("/domain/") || lower.Contains("\\domain\\") || lower.Contains(".domain"))
                return "Domain";
            if (lower.Contains("/application/") || lower.Contains("\\application\\") || lower.Contains(".application"))
                return "Application";
            if (lower.Contains("/infrastructure/") || lower.Contains("\\infrastructure\\") || lower.Contains(".infrastructure"))
                return "Infrastructure";
            if (lower.Contains("/web/") || lower.Contains("\\web\\") || lower.Contains("/api/") || lower.Contains("/controllers/"))
                return "Presentation / API";
            if (lower.Contains("/features/") || lower.Contains("\\features\\") || lower.Contains("/slices/"))
                return "Vertical Slices / Features";
            if (lower.Contains("/cli/") || lower.Contains("\\cli\\") || lower.Contains("program.cs"))
                return "Entry Point / CLI";
            if (lower.Contains("/build/") || lower.Contains("/tests/") || lower.Contains("/specs/"))
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

    // Updated extractors to be async
    public class SolutionOverviewExtractor
    {
        private readonly ExtractionOptions _options;

        public SolutionOverviewExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Solution Overview");
            sb.AppendLine($"**Root**: {directory}");

            // Solution info
            var slnFiles = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).ToList();
            if (slnFiles.Any())
            {
                sb.AppendLine($"**Solution**: {Path.GetFileName(slnFiles.First())}");
                if (solution != null)
                    sb.AppendLine($"**Projects**: {solution.Projects.Count()}");
            }

            // Parallel project analysis
            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ex => f.Contains(ex)))
                .ToList();

            sb.AppendLine($"**Total .csproj**: {csprojFiles.Count}");

            var frameworks = new ConcurrentBag<string>();
            var runtimeIdentifiers = new ConcurrentBag<string>();
            var analyzers = new ConcurrentBag<string>();
            var isCliTool = false;
            var cliCommandName = string.Empty;

            var tasks = csprojFiles.Select(async csproj =>
            {
                try
                {
                    using var stream = File.OpenRead(csproj);
                    var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

                    // Target frameworks
                    var tf = doc.Descendants("TargetFramework").FirstOrDefault()?.Value ??
                             doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault();
                    if (!string.IsNullOrEmpty(tf))
                        frameworks.Add(tf);

                    // Runtime identifiers
                    var rid = doc.Descendants("RuntimeIdentifier").FirstOrDefault()?.Value;
                    if (!string.IsNullOrEmpty(rid))
                        runtimeIdentifiers.Add(rid);

                    var rids = doc.Descendants("RuntimeIdentifiers").FirstOrDefault()?.Value;
                    if (!string.IsNullOrEmpty(rids))
                    {
                        foreach (var r in rids.Split(';'))
                            runtimeIdentifiers.Add(r.Trim());
                    }

                    // Analyzers
                    foreach (var analyzer in doc.Descendants("PackageReference")
                        .Where(p => p.Attribute("Include")?.Value.Contains("Analyzer") == true))
                    {
                        analyzers.Add(analyzer.Attribute("Include")?.Value ?? "");
                    }

                    // CLI tool detection
                    var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
                    var toolCommand = doc.Descendants("ToolCommandName").FirstOrDefault()?.Value;
                    var packageType = doc.Descendants("PackAsTool").FirstOrDefault()?.Value;

                    if ((outputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true && toolCommand != null) ||
                        packageType?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        isCliTool = true;
                        cliCommandName = toolCommand ?? Path.GetFileNameWithoutExtension(csproj);
                    }
                }
                catch (Exception ex)
                {
                    if (_options.VerboseOutput)
                        Console.WriteLine($"Warning: Error reading {csproj}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            sb.AppendLine($"**Frameworks**: {string.Join(", ", frameworks.Distinct())}");

            if (runtimeIdentifiers.Any())
                sb.AppendLine($"**Runtime IDs**: {string.Join(", ", runtimeIdentifiers.Distinct())}");

            if (analyzers.Any())
                sb.AppendLine($"**Analyzers**: {string.Join(", ", analyzers.Distinct())}");

            sb.AppendLine($"**Type**: {(isCliTool ? $"CLI Tool (cmd: {cliCommandName})" : "Library/App")}");
            sb.AppendLine();

            return sb.ToString();
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

    public class CallGraphExtractor
    {
        private readonly ExtractionOptions _options;

        public CallGraphExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(Solution solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Call Graph");

            var calls = new ConcurrentBag<(string caller, string callee, string feature)>();

            // Process projects in parallel
            var tasks = solution.Projects.Select(async project =>
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    return;

                // Process documents in parallel within each project
                var docTasks = project.Documents.Select(async doc =>
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        return;

                    var root = await tree.GetRootAsync();
                    var model = compilation.GetSemanticModel(tree);

                    // Find all method declarations
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .Where(m => !IsTrivialMethod(m));

                    foreach (var method in methods)
                    {
                        var methodSymbol = model.GetDeclaredSymbol(method);
                        if (methodSymbol == null || ShouldExcludeMethod(methodSymbol))
                            continue;

                        var callerName = GetFullMethodName(methodSymbol);
                        var feature = ExtractFeatureFromNamespace(methodSymbol.ContainingNamespace?.ToString() ?? "");

                        // Find all invocations within this method
                        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

                        foreach (var invocation in invocations)
                        {
                            var invokedSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                            if (invokedSymbol == null || ShouldExcludeMethod(invokedSymbol))
                                continue;

                            // Filter to only track internal calls (within solution)
                            if (invokedSymbol.ContainingAssembly.Name == compilation.AssemblyName ||
                                solution.Projects.Any(p => p.AssemblyName == invokedSymbol.ContainingAssembly.Name))
                            {
                                var calleeName = GetFullMethodName(invokedSymbol);
                                calls.Add((callerName, calleeName, feature));
                            }
                        }
                    }
                });

                await Task.WhenAll(docTasks);
            });

            await Task.WhenAll(tasks);

            // Group by feature if enabled
            if (_options.EnableFeatureGrouping)
            {
                var callsByFeature = calls.GroupBy(c => c.feature)
                    .OrderBy(g => g.Key);

                foreach (var featureGroup in callsByFeature)
                {
                    sb.AppendLine($"## {featureGroup.Key}");

                    var featureCalls = featureGroup
                        .GroupBy(c => c.caller)
                        .OrderBy(g => g.Key);

                    foreach (var group in featureCalls)
                    {
                        var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                        sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                // Regular grouping
                var groupedCalls = calls.GroupBy(c => c.caller)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedCalls)
                {
                    var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                    sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private bool IsTrivialMethod(MethodDeclarationSyntax method)
        {
            var name = method.Identifier.Text;
            return _options.TrivialMethodPatterns.Any(pattern =>
                Regex.IsMatch(name, pattern.Replace("*", ".*")));
        }

        private bool ShouldExcludeMethod(IMethodSymbol method)
        {
            var ns = method.ContainingNamespace?.ToString() ?? "";
            return _options.ExcludeNamespaces.Any(pattern =>
                pattern.Contains("*")
                    ? Regex.IsMatch(ns, pattern.Replace("*", ".*"))
                    : ns.Contains(pattern));
        }

        private string ExtractFeatureFromNamespace(string ns)
        {
            // Try to extract feature from namespace pattern
            foreach (var pattern in _options.FeaturePatterns)
            {
                var regex = new Regex(pattern.Replace("*", @"(\w+)"));
                var match = regex.Match(ns);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            // Fallback to second-level namespace
            var parts = ns.Split('.');
            return parts.Length >= 2 ? parts[1] : "Core";
        }

        private string GetFullMethodName(IMethodSymbol method)
        {
            return $"{method.ContainingType?.ToDisplayString()}.{method.Name}";
        }

        private string GetShortName(string fullName)
        {
            var parts = fullName.Split('.');
            if (parts.Length >= 2)
                return $"{parts[^2]}.{parts[^1]}";
            return fullName;
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

    public class DomainModelExtractor
    {
        private readonly ExtractionOptions _options;

        public DomainModelExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Domain Model");

            var entities = new ConcurrentBag<string>();
            var enums = new ConcurrentBag<string>();
            var dtos = new ConcurrentBag<string>();

            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)));

            var tasks = csFiles.Select(async file =>
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();

                    // Find entities
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var cls in classes)
                    {
                        var name = cls.Identifier.Text;
                        if (name.EndsWith("Entity") || name.EndsWith("Model") || name.EndsWith("Aggregate"))
                            entities.Add(name);
                        else if (name.EndsWith("Dto") || name.EndsWith("DTO") ||
                                name.EndsWith("Request") || name.EndsWith("Response") ||
                                name.EndsWith("Command") || name.EndsWith("Query"))
                            dtos.Add(name);
                    }

                    // Find enums
                    var enumTypes = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
                    foreach (var en in enumTypes)
                    {
                        enums.Add(en.Identifier.Text);
                    }
                }
                catch { }
            });

            await Task.WhenAll(tasks);

            if (entities.Any())
            {
                sb.AppendLine("## Entities");
                foreach (var entity in entities.Distinct().OrderBy(e => e).Take(int.MaxValue))
                    sb.AppendLine($"- {entity}");
            }

            if (enums.Any())
            {
                sb.AppendLine("\n## Enums");
                foreach (var en in enums.Distinct().OrderBy(e => e).Take(int.MaxValue))
                    sb.AppendLine($"- {en}");
            }

            if (dtos.Any())
            {
                sb.AppendLine("\n## DTOs/Commands/Queries");
                foreach (var dto in dtos.Distinct().OrderBy(d => d).Take(int.MaxValue))
                    sb.AppendLine($"- {dto}");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
