using System.Collections.Concurrent;
using System.Xml.Linq;
using DevContext.Core.Models;
using Microsoft.CodeAnalysis;

namespace DevContext.Core.Analysis;

public sealed class SolutionAnalyzer : IAnalyzer
{
    private readonly ExtractionOptions _options;

    public SolutionAnalyzer(ExtractionOptions options)
    {
        _options = options;
    }

    public string Name => "Solution Overview";

    public async Task AnalyzeAsync(CodeModel model, CancellationToken ct = default)
    {
        var directory = model.RootDirectory;

        var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !_options.ExcludeDirectories.Any(ex => f.Contains(ex)))
            .ToList();

        var projects = new ConcurrentBag<ProjectModel>();

        await Parallel.ForEachAsync(csprojFiles, ct, async (csproj, ct) =>
        {
            try
            {
                using var stream = File.OpenRead(csproj);
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
                var projectModel = new ProjectModel
                {
                    Name = Path.GetFileNameWithoutExtension(csproj),
                    TargetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                        ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault()
                        ?? "unknown"
                };

                foreach (var pkg in doc.Descendants("PackageReference"))
                {
                    var name = pkg.Attribute("Include")?.Value;
                    if (name != null)
                        projectModel.NuGetPackages.Add(name);
                }

                foreach (var projRef in doc.Descendants("ProjectReference"))
                {
                    var refPath = projRef.Attribute("Include")?.Value;
                    if (refPath != null)
                        projectModel.ProjectReferences.Add(Path.GetFileNameWithoutExtension(refPath));
                }

                var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
                var toolCommand = doc.Descendants("ToolCommandName").FirstOrDefault()?.Value;
                var packAsTool = doc.Descendants("PackAsTool").FirstOrDefault()?.Value;

                if ((outputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true && toolCommand != null)
                    || packAsTool?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                {
                    projectModel.IsCliTool = true;
                    projectModel.CliCommandName = toolCommand ?? projectModel.Name;
                }

                projects.Add(projectModel);
            }
            catch
            {
                // skip unreadable projects
            }
        });

        model.Projects = projects.OrderBy(p => p.Name).ToList();
    }
}
