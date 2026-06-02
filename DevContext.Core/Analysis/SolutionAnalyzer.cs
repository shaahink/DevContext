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
            .Where(f =>
            {
                var rel = Path.GetRelativePath(directory, f);
                return !_options.ExcludeDirectories.Any(ex => rel.Contains(ex));
            })
            .ToList();

        var dirProps = TryLoadDirectoryBuildProps(directory);

        var projects = new ConcurrentBag<ProjectModel>();

        await Parallel.ForEachAsync(csprojFiles, ct, async (csproj, ct) =>
        {
            try
            {
                using var stream = File.OpenRead(csproj);
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);

                var tf = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                      ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault()
                      ?? dirProps;
                var projectModel = new ProjectModel
                {
                    Name = Path.GetFileNameWithoutExtension(csproj),
                    TargetFramework = tf ?? "unknown"
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

    private static string? TryLoadDirectoryBuildProps(string directory)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directory);
            while (dirInfo != null)
            {
                var propsPath = Path.Combine(dirInfo.FullName, "Directory.Build.props");
                if (File.Exists(propsPath))
                {
                    var doc = XDocument.Load(propsPath);
                    return doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                        ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault();
                }
                dirInfo = dirInfo.Parent;
            }
        }
        catch
        {
        }
        return null;
    }
}
