using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace DevContext.Core.Extractors
{
    /// <summary>
    /// Extracts high-level solution and project metadata (frameworks, packages, project structure).
    /// </summary>
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
}
