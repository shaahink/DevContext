using DevContext.Core.Models;

namespace DevContext.Core.Analysis;

public sealed class LayerAnalyzer : IAnalyzer
{
    private readonly ExtractionOptions _options;

    public LayerAnalyzer(ExtractionOptions options)
    {
        _options = options;
    }

    public string Name => "Software Layers";

    public Task AnalyzeAsync(CodeModel model, CancellationToken ct = default)
    {
        var root = model.RootDirectory;
        var csFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(root, f);
                return !_options.ExcludeDirectories.Any(ex => rel.Contains(ex));
            });

        var counts = new Dictionary<string, int>();

        foreach (var file in csFiles)
        {
            var relative = Path.GetRelativePath(model.RootDirectory, file);
            var layer = ClassifyLayer(relative, file);

            counts.TryGetValue(layer, out var count);
            counts[layer] = count + 1;
        }

        model.LayerCounts = counts;
        return Task.CompletedTask;
    }

    private static string ClassifyLayer(string relativePath, string fullPath)
    {
        var lower = relativePath.ToLowerInvariant();
        var fileName = Path.GetFileName(fullPath).ToLowerInvariant();

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
}
