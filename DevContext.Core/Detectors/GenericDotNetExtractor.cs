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

namespace DevContext.Core
{
    public class ExtractionOptions
    {
        public bool IncludeMethodSignatures { get; set; } = true;
        public bool IncludeDependencyGraph { get; set; } = true;
        public bool IncludeCallGraph { get; set; } = true;
        public bool IncludeDomainModel { get; set; } = false;
        public bool UseMermaidForGraphs { get; set; } = false;
        public bool TokenCompact { get; set; } = true; // Default ON for LLM usage
        public string? OutputFilePath { get; set; } = null;
        public bool VerboseOutput { get; set; } = true;
    }

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

        public ExtractionResult Extract(string directory)
        {
            var sb = new StringBuilder();

            // Initialize MSBuild
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            // Header
            sb.AppendLine("# DevContext - .NET Project Analysis");
            sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Token-Compact**: {(_options.TokenCompact ? "ON" : "OFF")}");
            sb.AppendLine();

            // Load workspace and solution
            var slnPath = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            MSBuildWorkspace? workspace = null;
            Solution? solution = null;

            if (slnPath != null)
            {
                try
                {
                    workspace = MSBuildWorkspace.Create();
                    solution = workspace.OpenSolutionAsync(slnPath).Result;
                }
                catch (Exception ex)
                {
                    if (_options.VerboseOutput)
                        Console.WriteLine($"Warning: Failed to load solution: {ex.Message}");
                }
            }

            // 1. Solution Overview
            var overviewExtractor = new SolutionOverviewExtractor(_options);
            sb.Append(overviewExtractor.Extract(directory, solution));

            // 2. Dependency Graph
            if (_options.IncludeDependencyGraph)
            {
                var depExtractor = new DependencyGraphExtractor(_options);
                sb.Append(depExtractor.Extract(directory, solution));
            }

            // 3. Call Graph
            if (_options.IncludeCallGraph && solution != null)
            {
                var callExtractor = new CallGraphExtractor(_options);
                sb.Append(callExtractor.Extract(solution));
            }

            // 4. Code Structure
            var codeExtractor = new CodeStructureExtractor(_options);
            sb.Append(codeExtractor.Extract(directory, solution));

            // 5. Domain Model (placeholder for future)
            if (_options.IncludeDomainModel)
            {
                var domainExtractor = new DomainModelExtractor(_options);
                sb.Append(domainExtractor.Extract(directory, solution));
            }

            // Apply token compression if enabled
            string finalContent = sb.ToString();
            if (_options.TokenCompact)
            {
                var compressor = new TokenCompressor();
                finalContent = compressor.Compress(finalContent);
            }

            // Save to file if specified
            var result = new ExtractionResult("generic-dotnet", finalContent);

            if (!string.IsNullOrEmpty(_options.OutputFilePath))
            {
                try
                {
                    File.WriteAllText(_options.OutputFilePath, result.Content);
                    Console.WriteLine($"✅ Context saved to: {_options.OutputFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to save: {ex.Message}");
                }
            }

            workspace?.Dispose();
            return result;
        }
    }

    public interface IProjectDetector
    {
        bool Detect(string targetDir);
        ExtractionResult Extract(string targetDir);
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

        public string Extract(string directory, Solution? solution)
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

            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var doc = XDocument.Load(csproj);

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
            }

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

        public string Extract(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Dependency Graph");

            var projectDeps = new Dictionary<string, List<string>>();
            var projectPackages = new Dictionary<string, List<string>>();

            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).ToList();

            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var doc = XDocument.Load(csproj);
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
            }

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

        public CallGraphExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public string Extract(Solution solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Call Graph");

            var calls = new List<(string caller, string callee)>();

            foreach (var project in solution.Projects)
            {
                var compilation = project.GetCompilationAsync().Result;
                if (compilation == null)
                    continue;

                foreach (var doc in project.Documents)
                {
                    var tree = doc.GetSyntaxTreeAsync().Result;
                    if (tree == null)
                        continue;

                    var root = tree.GetRoot();
                    var model = compilation.GetSemanticModel(tree);

                    // Find all method declarations
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                    foreach (var method in methods)
                    {
                        var methodSymbol = model.GetDeclaredSymbol(method);
                        if (methodSymbol == null)
                            continue;

                        var callerName = GetFullMethodName(methodSymbol);

                        // Find all invocations within this method
                        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

                        foreach (var invocation in invocations)
                        {
                            var invokedSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                            if (invokedSymbol == null)
                                continue;

                            // Filter to only track internal calls (within solution)
                            if (invokedSymbol.ContainingAssembly.Name == compilation.AssemblyName ||
                                solution.Projects.Any(p => p.AssemblyName == invokedSymbol.ContainingAssembly.Name))
                            {
                                var calleeName = GetFullMethodName(invokedSymbol);
                                calls.Add((callerName, calleeName));
                            }
                        }
                    }
                }
            }

            // Group and display
            var groupedCalls = calls.GroupBy(c => c.caller)
                .OrderBy(g => g.Key);


            if (_options.UseMermaidForGraphs)
            {
                sb.AppendLine("```mermaid");
                sb.AppendLine("graph TD");
                foreach (var call in calls)
                {
                    sb.AppendLine($"  {SanitizeForMermaid(call.caller)} --> {SanitizeForMermaid(call.callee)}");
                }
                sb.AppendLine("```");
            }
            else
            {
                foreach (var group in groupedCalls)
                {
                    var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                    sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                }
            }

            sb.AppendLine();
            return sb.ToString();
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

        private string SanitizeForMermaid(string name)
        {
            return name.Replace(".", "_").Replace("-", "_").Replace("<", "").Replace(">", "");
        }
    }

    public class CodeStructureExtractor
    {
        private readonly ExtractionOptions _options;

        public CodeStructureExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public string Extract(string directory, Solution? solution)
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
            ///; // Limit directories shown

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
                ExtractMethodSignatures(sb, directory, excludePatterns);
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
                        totalLoc += File.ReadAllLines(fullPath).Length;
                    }
                    catch { }
                }
                sb.AppendLine($"\n**Lines of Code**: ~{totalLoc:N0}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private void ExtractMethodSignatures(StringBuilder sb, string directory, string[] excludePatterns)
        {
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludePatterns.Any(ep => f.Contains(ep)));


            foreach (var file in csFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();
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

        public string Extract(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Domain Model");

            var excludePatterns = new[] { ".git", ".vs", "bin", "obj", "Test" };
            var entities = new List<string>();
            var enums = new List<string>();
            var dtos = new List<string>();

            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludePatterns.Any(ep => f.Contains(ep)));


            foreach (var file in csFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();

                    // Find entities (classes ending with Entity, Model, or in Domain namespace)
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var cls in classes)
                    {
                        var name = cls.Identifier.Text;
                        if (name.EndsWith("Entity") || name.EndsWith("Model") || name.EndsWith("Domain"))
                            entities.Add(name);
                        else if (name.EndsWith("Dto") || name.EndsWith("DTO") || name.EndsWith("Request") || name.EndsWith("Response"))
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
            }

            if (entities.Any())
            {
                sb.AppendLine("## Entities");
                foreach (var entity in entities.Distinct().OrderBy(e => e))
                    sb.AppendLine($"- {entity}");
            }

            if (enums.Any())
            {
                sb.AppendLine("\n## Enums");
                foreach (var en in enums.Distinct().OrderBy(e => e))
                    sb.AppendLine($"- {en}");
            }

            if (dtos.Any())
            {
                sb.AppendLine("\n## DTOs");
                foreach (var dto in dtos.Distinct().OrderBy(d => d))
                    sb.AppendLine($"- {dto}");
            }

            sb.AppendLine();
            return sb.ToString();
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
