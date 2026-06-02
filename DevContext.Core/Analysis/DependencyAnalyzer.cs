using DevContext.Core.Models;

namespace DevContext.Core.Analysis;

public sealed class DependencyAnalyzer : IAnalyzer
{
    public string Name => "Dependency Graph";

    public Task AnalyzeAsync(CodeModel model, CancellationToken ct = default)
    {
        var edges = new List<DependencyEdge>();

        foreach (var project in model.Projects)
        {
            foreach (var refName in project.ProjectReferences)
            {
                edges.Add(new DependencyEdge
                {
                    SourceProject = project.Name,
                    TargetProject = refName,
                    Type = DependencyType.ProjectReference
                });
            }
        }

        model.Dependencies = edges;
        return Task.CompletedTask;
    }
}
