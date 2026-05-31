using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace DevContext.Core.Extractors
{
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
                    .Where(g => g.Count() > 1)
                    .OrderByDescending(g => g.Count())
                    .Take(15);

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
}
