using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DevContext.Core.Extractors;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;

namespace DevContext.Core
{
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
        public async Task<ExtractionResult> ExtractAsync(string directory)
        {
            var sb = new StringBuilder();
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            // Header
            sb.AppendLine("# DevContext - .NET Project Analysis");
            sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Token-Compact**: {(_options.TokenCompact ? "ON" : "OFF")}");
            sb.AppendLine();

            // Load solution with progress
            Solution? solution = null;
            var slnPath = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (slnPath != null)
            {
                await AnsiConsole.Status()
                    .StartAsync("Loading solution...", async ctx =>
                    {
                        try
                        {
                            using var workspace = MSBuildWorkspace.Create();
                            solution = await workspace.OpenSolutionAsync(slnPath);
                        }
                        catch (Exception ex)
                        {
                            if (_options.VerboseOutput)
                                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to load solution: {ex.Message}[/]");
                        }
                    });
            }

            // Extract components with progress
            await AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var tasks = new List<string> {
                        "Solution Overview",
                        "Dependency Graph",
                        "Call Graph",
                        "Code Structure",
                        "Domain Model",
                        "Architecture View"
                    };

                    var progressTasks = tasks.Select(t => ctx.AddTask($"[green]{t}[/]")).ToList();

                    // 1. Solution Overview
                    var overviewExtractor = new SolutionOverviewExtractor(_options);
                    sb.Append(await overviewExtractor.ExtractAsync(directory, solution));
                    progressTasks[0].Value = 100;

                    // 2. Dependency Graph
                    if (_options.IncludeDependencyGraph)
                    {
                        var depExtractor = new DependencyGraphExtractor(_options);
                        sb.Append(await depExtractor.ExtractAsync(directory, solution));
                    }
                    progressTasks[1].Value = 100;

                    // 3. Call Graph
                    if (_options.IncludeCallGraph && solution != null)
                    {
                        var callExtractor = new CallGraphExtractor(_options, progressTasks[2]);
                        await callExtractor.ExtractAsync(solution);

                    }
                    progressTasks[2].Value = 100;

                    // 4. Code Structure
                    var codeExtractor = new CodeStructureExtractor(_options);
                    sb.Append(await codeExtractor.ExtractAsync(directory, solution));
                    progressTasks[3].Value = 100;

                    // 5. Domain Model
                    if (_options.IncludeDomainModel)
                    {
                        var domainExtractor = new DomainModelExtractor(_options);
                        sb.Append(await domainExtractor.ExtractAsync(directory, solution));
                    }
                    progressTasks[4].Value = 100;

                    // 6. Architecture View
                    if (_options.IncludeArchitectureView)
                    {
                        var archExtractor = new ArchitectureViewExtractor(_options);
                        sb.Append(await archExtractor.ExtractAsync(directory, solution));
                    }
                    progressTasks[5].Value = 100;
                });

            // Apply token compression
            string finalContent = sb.ToString();
            if (_options.TokenCompact)
            {
                var compressor = new TokenCompressor();
                finalContent = compressor.Compress(finalContent);
            }

            // Save output
            var result = new ExtractionResult("generic-dotnet", finalContent);
            if (!string.IsNullOrEmpty(_options.OutputFilePath))
            {
                try
                {
                    await File.WriteAllTextAsync(_options.OutputFilePath, result.Content);
                    AnsiConsole.MarkupLine($"[green]✅ Context saved to: {_options.OutputFilePath}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Failed to save: {ex.Message}[/]");
                }
            }

            return result;
        }
    }
}

namespace DevContext.Core.Extractors
{
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
            // Project analysis
            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).ToList();
            sb.AppendLine($"**Total .csproj**: {csprojFiles.Count}");
            var frameworks = new HashSet<string>();
            var runtimeIdentifiers = new HashSet<string>();
            var analyzers = new HashSet<string>();
            var isCliTool = false;
            var cliCommandName = string.Empty;

            var tasks = csprojFiles.Select(async csproj =>
            {
                try
                {
                    var doc = await Task.Run(() => XDocument.Load(csproj));

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

            sb.AppendLine($"**Frameworks**: {string.Join(", ", frameworks)}");
            if (runtimeIdentifiers.Any())
                sb.AppendLine($"**Runtime IDs**: {string.Join(", ", runtimeIdentifiers)}");
            if (analyzers.Any())
                sb.AppendLine($"**Analyzers**: {string.Join(", ", analyzers)}");
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
            var projectDeps = new Dictionary<string, List<string>>();
            var projectPackages = new Dictionary<string, List<string>>();
            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).ToList();

            var tasks = csprojFiles.Select(async csproj =>
            {
                try
                {
                    var doc = await Task.Run(() => XDocument.Load(csproj));
                    var projectName = Path.GetFileNameWithoutExtension(csproj);
                    projectDeps[projectName] = new List<string>();
                    projectPackages[projectName] = new List<string>();

                    // Project references
                    foreach (var projRef in doc.Descendants("ProjectReference"))
                    {
                        var refPath = projRef.Attribute("Include")?.Value;
                        if (!string.IsNullOrEmpty(refPath))
                        {
                            var refName = Path.GetFileNameWithoutExtension(refPath);
                            projectDeps[projectName].Add(refName);
                        }
                    }

                    // NuGet packages
                    foreach (var pkg in doc.Descendants("PackageReference"))
                    {
                        var name = pkg.Attribute("Include")?.Value;
                        var ver = pkg.Attribute("Version")?.Value;
                        if (!string.IsNullOrEmpty(name))
                        {
                            projectPackages[projectName].Add($"{name}@{ver ?? "latest"}");
                        }
                    }
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
                var d = projectDeps.ToList();
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
                    else
                    {
                        sb.AppendLine($"{kvp.Key} (standalone)");
                    }
                }
                // Top NuGet packages across projects
                var allPackages = projectPackages.SelectMany(p => p.Value)
                    .GroupBy(p => p.Split('@')[0])
                    .OrderByDescending(g => g.Count());

                sb.AppendLine("\n## Top NuGet Packages");
                foreach (var pkg in allPackages)
                {
                    sb.AppendLine($"- {pkg.Key} (used by {pkg.Count()} projects)");
                }
            }
            sb.AppendLine();
            return sb.ToString();
        }
        private string SanitizeForMermaid(string name)
        {
            return name.Replace(".", "_").Replace("-", "_");
        }
    }

    public class CallGraphExtractor
    {
        private readonly ExtractionOptions _options;
        private ProgressTask? _progressTask;

        public CallGraphExtractor(ExtractionOptions options, ProgressTask? progressTask = null)
        {
            _options = options;
            _progressTask = progressTask;
        }
        public async Task<string> ExtractAsync(Solution solution)
        {
            try
            {


                var sb = new StringBuilder();
                sb.AppendLine("# Call Graph");

                var calls = await ExtractCallGraphAsync(solution);

                // Apply filters
                if (!string.IsNullOrEmpty(_options.EntryPointFilter))
                {
                    calls = FilterByEntryPoint(calls, _options.EntryPointFilter);
                }

                if (!string.IsNullOrEmpty(_options.FeatureFilter))
                {
                    calls = FilterByFeature(calls, _options.FeatureFilter);
                }

                // Group by feature
                if (_options.GroupByFeature)
                {
                    var featureGroups = GroupCallsByFeature(calls);
                    foreach (var group in featureGroups.OrderBy(g => g.Key))
                    {
                        sb.AppendLine($"## {group.Key}");
                        foreach (var call in group.Value)
                        {
                            sb.AppendLine($"{GetShortName(call.caller)} -> {GetShortName(call.callee)}");
                        }
                        sb.AppendLine();
                    }
                }
                else
                {
                    // Standard grouping
                    var groupedCalls = calls.GroupBy(c => c.caller)
                        .OrderBy(g => g.Key);

                    foreach (var group in groupedCalls)
                    {
                        var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                        sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception e)
            {

                throw;
            }
        }

        private async Task<List<(string caller, string callee)>> ExtractCallGraphAsync(
            Solution solution,
            ProgressTask? progressTask = null)
        {
            var calls = new List<(string caller, string callee)>();

            // Count total projects for progress calculation
            var projectList = solution.Projects.ToList();
            int totalProjects = projectList.Count;
            int completedProjects = 0;

            // Set description if we have progress
            progressTask?.Description = "Analyzing call graph...";

            var projectTasks = projectList.Select(async project =>
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                {
                    Interlocked.Increment(ref completedProjects);
                    progressTask?.Value = (completedProjects / (double)totalProjects) * 100;
                    return null;
                }

                var projectCalls = new List<(string caller, string callee)>();
                var documentList = project.Documents.ToList();

                foreach (var document in documentList)
                {
                    var tree = await document.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();
                    var model = compilation.GetSemanticModel(tree);
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                    foreach (var method in methods)
                    {
                        // Apply skip filters
                        if (_options.ExcludeTrivialMethods && IsTrivialMethod(method))
                            continue;

                        if (_options.ExcludeEFBoilerplate && IsEFBoilerplate(method))
                            continue;

                        if (_options.ExcludeBlazorLifecycle && IsBlazorLifecycleHook(method))
                            continue;

                        var methodSymbol = model.GetDeclaredSymbol(method);
                        if (methodSymbol == null)
                            continue;

                        var callerName = GetFullMethodName(methodSymbol);

                        // Find invocations
                        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
                        foreach (var invocation in invocations)
                        {
                            var invokedSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                            if (invokedSymbol == null)
                                continue;

                            if (IsInternalCall(invokedSymbol, compilation, solution))
                            {
                                var calleeName = GetFullMethodName(invokedSymbol);
                                lock (projectCalls)
                                {
                                    projectCalls.Add((callerName, calleeName));
                                }
                            }
                        }
                    }
                }

                // Update progress for completed project
                Interlocked.Increment(ref completedProjects);
                progressTask?.Value = (completedProjects / (double)totalProjects) * 100;

                return projectCalls;
            });

            var allCalls = await Task.WhenAll(projectTasks);
            calls = allCalls.Where(c => c != null).SelectMany(c => c!).ToList();

            return calls;
        }

        private bool IsTrivialMethod(MethodDeclarationSyntax method)
        {
            // Simple getter/setter
            //if (method.ExpressionBody != null &&
            //    method.ExpressionBody.Expression is MemberAccessExpression)
            //    return true;

            // Empty method
            if (method.Body?.Statements.Count == 0)
                return true;

            // Simple override that just calls base
            if (method.Modifiers.Any(m => m.ValueText == "override") &&
                method.Body?.Statements.Count == 1 &&
                method.Body.Statements[0] is ExpressionStatementSyntax expr &&
                expr.Expression is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is BaseExpressionSyntax)
                return true;

            return false;
        }

        private bool IsEFBoilerplate(MethodDeclarationSyntax method)
        {
            var classDeclaration = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration == null)
                return false;

            var className = classDeclaration.Identifier.Text;
            if (!className.EndsWith("Context") && !className.EndsWith("Configuration"))
                return false;

            var methodName = method.Identifier.Text;
            var efMethodNames = new[] { "OnModelCreating", "Configure", "HasIndex", "HasKey", "HasOne", "HasMany" };

            return efMethodNames.Contains(methodName);
        }

        private bool IsBlazorLifecycleHook(MethodDeclarationSyntax method)
        {
            var classDeclaration = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration == null)
                return false;

            var baseType = classDeclaration.BaseList?.Types
                .Select(t => t.Type.ToString())
                .FirstOrDefault(t => t.Contains("ComponentBase"));

            if (baseType == null)
                return false;

            var methodName = method.Identifier.Text;
            var lifecycleMethods = new[] { "OnInitialized", "OnParametersSet", "OnAfterRender", "ShouldRender" };

            return lifecycleMethods.Contains(methodName);
        }

        private bool IsInternalCall(IMethodSymbol invokedSymbol, Compilation compilation, Solution solution)
        {
            return invokedSymbol.ContainingAssembly.Name == compilation.AssemblyName ||
                   solution.Projects.Any(p => p.AssemblyName == invokedSymbol.ContainingAssembly.Name);
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

        private Dictionary<string, List<(string caller, string callee)>> GroupCallsByFeature(List<(string caller, string callee)> calls)
        {
            var featureGroups = new Dictionary<string, List<(string caller, string callee)>>();

            foreach (var call in calls)
            {
                var callerFeature = ExtractFeatureFromMethodName(call.caller);
                var calleeFeature = ExtractFeatureFromMethodName(call.callee);

                var feature = !string.IsNullOrEmpty(callerFeature) ? callerFeature : calleeFeature;
                feature = !string.IsNullOrEmpty(feature) ? feature : "General";

                if (!featureGroups.ContainsKey(feature))
                {
                    featureGroups[feature] = new List<(string caller, string callee)>();
                }

                featureGroups[feature].Add(call);
            }

            return featureGroups;
        }

        private string ExtractFeatureFromMethodName(string fullMethodName)
        {
            var lastDotIndex = fullMethodName.LastIndexOf('.');
            if (lastDotIndex <= 0)
                return string.Empty;

            var typeName = fullMethodName.Substring(0, lastDotIndex);
            var namespaceEndIndex = typeName.LastIndexOf('.');
            if (namespaceEndIndex <= 0)
                return string.Empty;

            var namespaceName = typeName.Substring(0, namespaceEndIndex);
            var namespaceParts = namespaceName.Split('.');

            var featureIndicators = new[] { "Advertisements", "Backlogs", "Courses", "News", "Projects" };

            foreach (var part in namespaceParts)
            {
                if (featureIndicators.Contains(part))
                {
                    return part;
                }
            }

            return string.Empty;
        }

        private List<(string caller, string callee)> FilterByEntryPoint(List<(string caller, string callee)> calls, string entryPoint)
        {
            var entryMethods = new HashSet<string>();
            var queue = new Queue<string>(new[] { entryPoint });
            var visited = new HashSet<string>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current))
                    continue;

                visited.Add(current);
                entryMethods.Add(current);

                foreach (var call in calls.Where(c => c.caller == current))
                {
                    if (!visited.Contains(call.callee))
                    {
                        queue.Enqueue(call.callee);
                    }
                }
            }

            return calls.Where(c => entryMethods.Contains(c.caller)).ToList();
        }

        private List<(string caller, string callee)> FilterByFeature(List<(string caller, string callee)> calls, string feature)
        {
            return calls.Where(c =>
                ExtractFeatureFromMethodName(c.caller).Equals(feature, StringComparison.OrdinalIgnoreCase) ||
                ExtractFeatureFromMethodName(c.callee).Equals(feature, StringComparison.OrdinalIgnoreCase)
            ).ToList();
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
            var excludePatterns = new[] { ".git", ".vs", ".nuke", ".github", "bin", "obj", ".idea", "nupkg", ".packageguard" };

            // File structure
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludePatterns.Any(ep => f.Contains(ep)))
                .Select(f => Path.GetRelativePath(directory, f))
                .OrderBy(f => f)
                .ToList();

            sb.AppendLine($"**Total Files**: {csFiles.Count} .cs");

            // Group by directory
            var filesByDirectory = csFiles.GroupBy(f => Path.GetDirectoryName(f) ?? "")
                .OrderBy(g => g.Key);

            foreach (var group in filesByDirectory)
            {
                var dirName = string.IsNullOrEmpty(group.Key) ? "Root" : group.Key;
                var fileList = string.Join(", ", group.Select(Path.GetFileName));
                if (group.Count() > 10)
                    fileList += $" +{group.Count() - 10} more";
                sb.AppendLine($"**{dirName}**: {fileList}");
            }

            // Method signatures if requested
            if (_options.IncludeMethodSignatures)
            {
                sb.AppendLine("\n## Method Signatures");
                await ExtractMethodSignaturesAsync(sb, directory, excludePatterns);
            }
            else
            {
                // Just summary
                var totalLoc = 0;
                foreach (var file in csFiles)
                {
                    try
                    {
                        var fullPath = Path.Combine(directory, file);
                        totalLoc += (await File.ReadAllLinesAsync(fullPath)).Length;
                    }
                    catch { }
                }
                sb.AppendLine($"\n**Lines of Code**: ~{totalLoc:N0}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private async Task ExtractMethodSignaturesAsync(StringBuilder sb, string directory, string[] excludePatterns)
        {
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludePatterns.Any(ep => f.Contains(ep)));

            foreach (var file in csFiles)
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();
                    var relativePath = Path.GetRelativePath(directory, file);
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                    if (!methods.Any())
                        continue;

                    sb.AppendLine($"\n### {relativePath}");

                    foreach (var method in methods)
                    {
                        var modifiers = string.Join(" ", method.Modifiers.Select(m => m.ValueText));
                        var parameters = _options.TokenCompact
                            ? string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var"))
                            : string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                        sb.AppendLine($"- {modifiers} {method.ReturnType} {method.Identifier}({parameters})");
                    }
                }
                catch { }
            }
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
            var excludePatterns = new[] { ".git", ".vs", "bin", "obj", "Test" };
            var entities = new List<EntityInfo>();
            var enums = new List<EnumInfo>();
            var dtos = new List<DtoInfo>();

            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludePatterns.Any(ep => f.Contains(ep)));

            foreach (var file in csFiles)
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();

                    // Find entities (classes ending with Entity, Model, or in Domain namespace)
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var cls in classes)
                    {
                        var name = cls.Identifier.Text;

                        if (name.EndsWith("Entity") || name.EndsWith("Model") || name.EndsWith("Domain"))
                        {
                            var entity = new EntityInfo
                            {
                                Name = name,
                                Properties = ExtractProperties(cls),
                                Methods = ExtractMethods(cls),
                                Namespace = GetNamespace(cls)
                            };
                            entities.Add(entity);
                        }
                        else if (name.EndsWith("Dto") || name.EndsWith("DTO") || name.EndsWith("Request") || name.EndsWith("Response"))
                        {
                            var dto = new DtoInfo
                            {
                                Name = name,
                                Properties = ExtractProperties(cls),
                                Namespace = GetNamespace(cls)
                            };
                            dtos.Add(dto);
                        }
                    }

                    // Find enums
                    var enumTypes = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
                    foreach (var en in enumTypes)
                    {
                        var enumInfo = new EnumInfo
                        {
                            Name = en.Identifier.Text,
                            Values = en.Members.Select(m => m.Identifier.Text).ToList(),
                            Namespace = GetNamespace(en)
                        };
                        enums.Add(enumInfo);
                    }
                }
                catch { }
            }

            // Group by feature if requested
            if (_options.GroupByFeature)
            {
                var featureEntities = GroupByFeature(entities, e => e.Namespace);
                var featureEnums = GroupByFeature(enums, e => e.Namespace);
                var featureDtos = GroupByFeature(dtos, d => d.Namespace);

                var allFeatures = featureEntities.Keys
                    .Union(featureEnums.Keys)
                    .Union(featureDtos.Keys)
                    .OrderBy(f => f);

                foreach (var feature in allFeatures)
                {
                    sb.AppendLine($"## {feature}");

                    if (featureEntities.ContainsKey(feature) && featureEntities[feature].Any())
                    {
                        sb.AppendLine("### Entities");
                        foreach (var entity in featureEntities[feature])
                        {
                            sb.AppendLine($"#### {entity.Name}");
                            sb.AppendLine($"**Namespace**: {entity.Namespace}");

                            if (entity.Properties.Any())
                            {
                                sb.AppendLine("**Properties**:");
                                foreach (var prop in entity.Properties)
                                {
                                    sb.AppendLine($"- {prop.Type} {prop.Name}");
                                }
                            }

                            if (entity.Methods.Any())
                            {
                                sb.AppendLine("**Methods**:");
                                foreach (var method in entity.Methods)
                                {
                                    sb.AppendLine($"- {method}");
                                }
                            }

                            sb.AppendLine();
                        }
                    }

                    if (featureEnums.ContainsKey(feature) && featureEnums[feature].Any())
                    {
                        sb.AppendLine("### Enums");
                        foreach (var enumInfo in featureEnums[feature])
                        {
                            sb.AppendLine($"#### {enumInfo.Name}");
                            sb.AppendLine($"**Namespace**: {enumInfo.Namespace}");
                            sb.AppendLine($"**Values**: {string.Join(", ", enumInfo.Values)}");
                            sb.AppendLine();
                        }
                    }

                    if (featureDtos.ContainsKey(feature) && featureDtos[feature].Any())
                    {
                        sb.AppendLine("### DTOs");
                        foreach (var dto in featureDtos[feature])
                        {
                            sb.AppendLine($"#### {dto.Name}");
                            sb.AppendLine($"**Namespace**: {dto.Namespace}");

                            if (dto.Properties.Any())
                            {
                                sb.AppendLine("**Properties**:");
                                foreach (var prop in dto.Properties)
                                {
                                    sb.AppendLine($"- {prop.Type} {prop.Name}");
                                }
                            }

                            sb.AppendLine();
                        }
                    }
                }
            }
            else
            {
                // Output without feature grouping
                if (entities.Any())
                {
                    sb.AppendLine("## Entities");
                    foreach (var entity in entities.Distinct().OrderBy(e => e.Name))
                    {
                        sb.AppendLine($"### {entity.Name}");
                        sb.AppendLine($"**Namespace**: {entity.Namespace}");

                        if (entity.Properties.Any())
                        {
                            sb.AppendLine("**Properties**:");
                            foreach (var prop in entity.Properties)
                            {
                                sb.AppendLine($"- {prop.Type} {prop.Name}");
                            }
                        }

                        if (entity.Methods.Any())
                        {
                            sb.AppendLine("**Methods**:");
                            foreach (var method in entity.Methods)
                            {
                                sb.AppendLine($"- {method}");
                            }
                        }

                        sb.AppendLine();
                    }
                }

                if (enums.Any())
                {
                    sb.AppendLine("## Enums");
                    foreach (var enumInfo in enums.Distinct().OrderBy(e => e.Name))
                    {
                        sb.AppendLine($"### {enumInfo.Name}");
                        sb.AppendLine($"**Namespace**: {enumInfo.Namespace}");
                        sb.AppendLine($"**Values**: {string.Join(", ", enumInfo.Values)}");
                        sb.AppendLine();
                    }
                }

                if (dtos.Any())
                {
                    sb.AppendLine("## DTOs");
                    foreach (var dto in dtos.Distinct().OrderBy(d => d.Name))
                    {
                        sb.AppendLine($"### {dto.Name}");
                        sb.AppendLine($"**Namespace**: {dto.Namespace}");

                        if (dto.Properties.Any())
                        {
                            sb.AppendLine("**Properties**:");
                            foreach (var prop in dto.Properties)
                            {
                                sb.AppendLine($"- {prop.Type} {prop.Name}");
                            }
                        }

                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private List<PropertyInfo> ExtractProperties(ClassDeclarationSyntax classDeclaration)
        {
            var properties = new List<PropertyInfo>();

            foreach (var property in classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var propertyInfo = new PropertyInfo
                {
                    Name = property.Identifier.Text,
                    Type = property.Type.ToString()
                };

                // Check for common attributes
                foreach (var attributeList in property.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        propertyInfo.Attributes.Add(attribute.Name.ToString());
                    }
                }

                properties.Add(propertyInfo);
            }

            return properties;
        }

        private List<string> ExtractMethods(ClassDeclarationSyntax classDeclaration)
        {
            var methods = new List<string>();

            foreach (var method in classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var modifiers = string.Join(" ", method.Modifiers.Select(m => m.ValueText));
                var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                methods.Add($"{modifiers} {method.ReturnType} {method.Identifier}({parameters})");
            }

            return methods;
        }

        private string GetNamespace(SyntaxNode node)
        {
            var namespaceDeclaration = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            return namespaceDeclaration?.Name.ToString() ?? "";
        }

        private Dictionary<string, List<T>> GroupByFeature<T>(List<T> items, Func<T, string> namespaceExtractor)
        {
            var featureGroups = new Dictionary<string, List<T>>();

            foreach (var item in items)
            {
                var ns = namespaceExtractor(item);
                string feature = "General";

                // Extract feature from namespace
                var parts = ns.Split('.');
                if (parts.Length > 1)
                {
                    // Look for common feature indicators
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        var part = parts[i];
                        if (part.EndsWith("Services") ||
                            part.EndsWith("Components") ||
                            part.EndsWith("Models") ||
                            part.EndsWith("Controllers") ||
                            part.EndsWith("Pages") ||
                            part.EndsWith("Handlers") ||
                            part.EndsWith("Queries") ||
                            part.EndsWith("Commands"))
                        {
                            // Return the previous part as the feature
                            if (i > 0)
                            {
                                feature = parts[i - 1];
                                break;
                            }
                        }
                    }
                }

                // Apply feature filter if specified
                if (!string.IsNullOrEmpty(_options.FeatureFilter) &&
                    !feature.Equals(_options.FeatureFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!featureGroups.ContainsKey(feature))
                {
                    featureGroups[feature] = new List<T>();
                }

                featureGroups[feature].Add(item);
            }

            return featureGroups;
        }

        private class EntityInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
            public List<string> Methods { get; set; } = new List<string>();
        }

        private class DtoInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
        }

        private class EnumInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public List<string> Values { get; set; } = new List<string>();
        }

        private class PropertyInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public List<string> Attributes { get; set; } = new List<string>();
        }
    }

    public class TokenCompressor
    {
        public string Compress(string content)
        {
            // Skip compression if content is small
            if (content.Length < 1000)
                return content;

            var compressed = content;

            // 1. Abbreviate common modifiers
            var modifierMap = new Dictionary<string, string>
            {
                { "public ", "pub " },
                { "private ", "priv " },
                { "protected ", "prot " },
                { "internal ", "int " },
                { "static ", "stat " },
                { "async ", "async " },
                { "override ", "ovr " },
                { "virtual ", "virt " },
                { "abstract ", "abs " },
                { "sealed ", "seal " },
                { "readonly ", "ro " },
                { "const ", "const " }
            };

            foreach (var kvp in modifierMap)
            {
                compressed = compressed.Replace(kvp.Key, kvp.Value);
            }

            // 2. Remove redundant phrases
            compressed = compressed.Replace("file-scoped", "(fs)");
            compressed = compressed.Replace("Search.AllDirectories", "**");
            compressed = compressed.Replace("StringComparison.OrdinalIgnoreCase", "OIC");
            compressed = compressed.Replace("System.", "");
            compressed = compressed.Replace("Microsoft.CodeAnalysis.", "");
            compressed = compressed.Replace("Microsoft.", "MS.");

            // 3. Compact whitespace (multiple line breaks to double)
            compressed = Regex.Replace(compressed, @"\n{3,}", "\n\n");

            // 4. Remove empty sections
            compressed = Regex.Replace(compressed, @"##[^\n]+\n(?=\n##|\n#|\z)", "");

            // 5. Shorten common patterns
            compressed = compressed.Replace("**Total", "**Tot");
            compressed = compressed.Replace("**Root Directory**:", "**Root**:");
            compressed = compressed.Replace("**Solution File**:", "**Sln**:");
            compressed = compressed.Replace("**Projects in Solution**:", "**Projs**:");
            compressed = compressed.Replace("**Target Frameworks**:", "**TFMs**:");
            compressed = compressed.Replace("**Runtime IDs**:", "**RIDs**:");
            compressed = compressed.Replace("**Lines of Code**:", "**LOC**:");

            // 6. Compact lists (remove bullet points, use semicolons)
            compressed = Regex.Replace(compressed, @"^- (.+)$", "$1;", RegexOptions.Multiline);
            compressed = Regex.Replace(compressed, @";\n(.+);", "; $1;", RegexOptions.Multiline);

            // 7. Remove markdown formatting where not essential
            compressed = compressed.Replace("```mermaid", "MERMAID:");
            compressed = compressed.Replace("```", "");

            // 8. Compact method signatures (remove spaces around parentheses and commas)
            compressed = Regex.Replace(compressed, @"\(\s+", "(");
            compressed = Regex.Replace(compressed, @"\s+\)", ")");
            compressed = Regex.Replace(compressed, @"\s*,\s*", ",");

            // 9. Deduplicate repeated patterns
            var lines = compressed.Split('\n');
            var uniqueLines = new List<string>();
            var lastLine = "";

            foreach (var line in lines)
            {
                // Skip duplicate adjacent lines
                if (line != lastLine || string.IsNullOrWhiteSpace(line))
                {
                    uniqueLines.Add(line);
                    lastLine = line;
                }
            }

            compressed = string.Join("\n", uniqueLines);

            // 10. Final trim
            compressed = compressed.Trim();

            // Report compression ratio if verbose
            var originalSize = content.Length;
            var compressedSize = compressed.Length;
            var ratio = (1 - (double)compressedSize / originalSize) * 100;

            if (ratio > 5) // Only report if meaningful compression
            {
                Console.WriteLine($"🗜️ Compressed: {originalSize:N0} → {compressedSize:N0} chars ({ratio:F1}% reduction)");
            }

            return compressed;
        }
    }
}



namespace DevContext.Core.Extractors
{
    public class ArchitectureViewExtractor
    {
        private readonly ExtractionOptions _options;
        public ArchitectureViewExtractor(ExtractionOptions options)
        {
            _options = options;
        }
        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Architecture View");

            if (solution == null)
            {
                sb.AppendLine("No solution loaded.");
                return sb.ToString();
            }

            // Project dependency analysis
            sb.AppendLine("## Project Dependencies");
            var projectDependencies = AnalyzeProjectDependencies(solution);
            foreach (var dep in projectDependencies)
            {
                sb.AppendLine($"{dep.Key} -> {string.Join(", ", dep.Value)}");
            }
            sb.AppendLine();

            // Architectural layers
            sb.AppendLine("## Architectural Layers");
            var layers = IdentifyArchitecturalLayers(solution);
            foreach (var layer in layers)
            {
                sb.AppendLine($"### {layer.Key}");
                foreach (var project in layer.Value)
                {
                    sb.AppendLine($"- {project}");
                }
                sb.AppendLine();
            }

            // DI and middleware analysis
            await AnalyzeStartupAndProgramAsync(sb, directory);

            return sb.ToString();
        }

        private Dictionary<string, List<string>> AnalyzeProjectDependencies(Solution solution)
        {
            var dependencies = new Dictionary<string, List<string>>();

            foreach (var project in solution.Projects)
            {
                var projectName = project.Name;
                dependencies[projectName] = new List<string>();

                foreach (var refProject in project.ProjectReferences)
                {
                    var refProjectName = solution.GetProject(refProject.ProjectId)?.Name;
                    if (!string.IsNullOrEmpty(refProjectName))
                    {
                        dependencies[projectName].Add(refProjectName);
                    }
                }
            }

            return dependencies;
        }

        private Dictionary<string, List<string>> IdentifyArchitecturalLayers(Solution solution)
        {
            var layers = new Dictionary<string, List<string>>();

            foreach (var project in solution.Projects)
            {
                var layer = DetermineArchitecturalLayer(project);
                if (!layers.ContainsKey(layer))
                {
                    layers[layer] = new List<string>();
                }
                layers[layer].Add(project.Name);
            }

            return layers;
        }

        private string DetermineArchitecturalLayer(Project project)
        {
            var name = project.Name.ToLower();

            if (name.Contains("api") || name.Contains("web") || name.Contains("ui"))
                return "Presentation";

            if (name.Contains("service") || name.Contains("business") || name.Contains("application"))
                return "Service";

            if (name.Contains("data") || name.Contains("repository") || name.Contains("ef"))
                return "Data";

            if (name.Contains("domain") || name.Contains("core"))
                return "Domain";

            if (name.Contains("test"))
                return "Test";

            return "Infrastructure";
        }

        private async Task AnalyzeStartupAndProgramAsync(StringBuilder sb, string directory)
        {
            var programFiles = Directory.EnumerateFiles(directory, "Program.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(directory, "Startup.cs", SearchOption.AllDirectories));

            foreach (var file in programFiles)
            {
                var code = await File.ReadAllTextAsync(file);
                var fileName = Path.GetFileName(file);

                sb.AppendLine($"## {fileName} Analysis");

                // Service registrations
                var services = ExtractServiceRegistrations(code);
                if (services.Any())
                {
                    sb.AppendLine("### Service Registrations");
                    foreach (var service in services)
                    {
                        sb.AppendLine($"- {service}");
                    }
                    sb.AppendLine();
                }

                // Middleware pipeline
                var middleware = ExtractMiddlewarePipeline(code);
                if (middleware.Any())
                {
                    sb.AppendLine("### Middleware Pipeline");
                    foreach (var item in middleware)
                    {
                        sb.AppendLine($"- {item}");
                    }
                    sb.AppendLine();
                }

                // Entry points
                var entryPoints = ExtractEntryPoints(code);
                if (entryPoints.Any())
                {
                    sb.AppendLine("### Entry Points");
                    foreach (var entryPoint in entryPoints)
                    {
                        sb.AppendLine($"- {entryPoint}");
                    }
                    sb.AppendLine();
                }
            }
        }

        private List<string> ExtractServiceRegistrations(string code)
        {
            var services = new List<string>();
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("AddScoped") || line.Contains("AddSingleton") || line.Contains("AddTransient"))
                {
                    var start = line.IndexOf('(') + 1;
                    var end = line.IndexOf(',', start);
                    if (end == -1)
                        end = line.IndexOf(')', start);

                    if (start > 0 && end > start)
                    {
                        var serviceType = line.Substring(start, end - start).Trim();
                        services.Add(serviceType);
                    }
                }
            }

            return services;
        }

        private List<string> ExtractMiddlewarePipeline(string code)
        {
            var middleware = new List<string>();
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("Use") && !line.Contains("Add"))
                {
                    var start = line.IndexOf("Use");
                    var end = line.IndexOf('(', start);
                    if (end == -1)
                        end = line.IndexOf(';', start);

                    if (start >= 0 && end > start)
                    {
                        var middlewareName = line.Substring(start, end - start).Trim();
                        middleware.Add(middlewareName);
                    }
                }
            }

            return middleware;
        }

        private List<string> ExtractEntryPoints(string code)
        {
            var entryPoints = new List<string>();
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Identifier.Text == "Main" || method.AttributeLists.Any(a =>
                    a.Attributes.Any(attr => attr.Name.ToString() == "EntryPoint")))
                {
                    entryPoints.Add(method.Identifier.Text);
                }
            }

            return entryPoints;
        }
    }
}


