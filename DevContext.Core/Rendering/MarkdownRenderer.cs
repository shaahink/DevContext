using System.Text;
using DevContext.Core.Models;

namespace DevContext.Core.Rendering;

public sealed class MarkdownRenderer
{
    public async Task<string> RenderAsync(CodeModel model, ExtractionOptions options)
    {
        var sb = new StringBuilder();

        RenderHeader(sb, options);
        RenderSolutionOverview(sb, model);
        RenderLayers(sb, model);
        RenderDependencies(sb, model);
        RenderCallGraph(sb, model);
        RenderFeatures(sb, model);
        RenderProjects(sb, model);

        return sb.ToString();
    }

    private static void RenderHeader(StringBuilder sb, ExtractionOptions options)
    {
        sb.AppendLine("# DevContext - .NET Project Context");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Profile: Depth={options.Depth}, Focus={options.Focus}" +
                      (options.FocusedFeatures.Count > 0 ? $" (features: {string.Join(", ", options.FocusedFeatures)})" : ""));
        sb.AppendLine();
        sb.AppendLine("> Paste this into your LLM prompt for focused, task-relevant context.");
        sb.AppendLine();
    }

    private static void RenderSolutionOverview(StringBuilder sb, CodeModel model)
    {
        sb.AppendLine("# Solution Overview");
        sb.AppendLine($"**Root**: {model.RootDirectory}");
        sb.AppendLine($"**Projects**: {model.Projects.Count}");

        foreach (var project in model.Projects)
        {
            sb.AppendLine($"- **{project.Name}** — {project.TargetFramework}" +
                          (project.IsCliTool ? " (CLI tool)" : ""));
        }
        sb.AppendLine();
    }

    private static void RenderLayers(StringBuilder sb, CodeModel model)
    {
        if (model.LayerCounts.Count == 0) return;

        sb.AppendLine("# Software Layers");
        var total = model.LayerCounts.Values.Sum();

        foreach (var kvp in model.LayerCounts.OrderBy(l => GetLayerOrder(l.Key)))
        {
            var pct = total > 0 ? (kvp.Value * 100.0 / total) : 0;
            sb.AppendLine($"- **{kvp.Key}** — {kvp.Value} files ({pct:F0}%)");
        }
        sb.AppendLine();
    }

    private static void RenderDependencies(StringBuilder sb, CodeModel model)
    {
        if (model.Dependencies.Count == 0) return;

        sb.AppendLine("# Dependency Graph");

        foreach (var dep in model.Dependencies)
        {
            sb.AppendLine($"- {dep.SourceProject} → {dep.TargetProject}");
        }
        sb.AppendLine();
    }

    private static void RenderCallGraph(StringBuilder sb, CodeModel model)
    {
        if (model.CallGraph.Count == 0) return;

        sb.AppendLine("# Call Graph");

        var grouped = model.CallGraph
            .GroupBy(e => e.Feature ?? "General")
            .OrderBy(g => g.Key);

        foreach (var featureGroup in grouped)
        {
            sb.AppendLine($"## {featureGroup.Key}");

            var byCaller = featureGroup
                .GroupBy(e => e.CallerType + "." + e.CallerMethod)
                .OrderBy(g => g.Key);

            foreach (var caller in byCaller)
            {
                var callees = caller
                    .Select(e => $"{e.CalleeType}.{e.CalleeMethod}")
                    .Distinct();
                var shortName = caller.Key.Split('.').Length >= 2
                    ? string.Join(".", caller.Key.Split('.').TakeLast(2))
                    : caller.Key;
                sb.AppendLine($"- {shortName} → {string.Join(", ", callees)}");
            }
            sb.AppendLine();
        }
    }

    private static void RenderFeatures(StringBuilder sb, CodeModel model)
    {
        if (model.Features.Count == 0) return;

        sb.AppendLine("# Features");

        foreach (var feature in model.Features.OrderBy(f => f.Name))
        {
            sb.AppendLine($"## {feature.Name}");
            if (feature.Endpoints.Count > 0)
            {
                sb.AppendLine("Endpoints:");
                foreach (var ep in feature.Endpoints)
                    sb.AppendLine($"- {ep}");
            }
            if (feature.TypeNames.Count > 0)
            {
                sb.AppendLine("Types:");
                foreach (var tn in feature.TypeNames.Take(10))
                    sb.AppendLine($"- {tn}");
            }
            sb.AppendLine();
        }
    }

    private static void RenderProjects(StringBuilder sb, CodeModel model)
    {
        sb.AppendLine("# Code Structure");

        var hasTypes = model.Projects.Any(p => p.Types.Count > 0);

        if (!hasTypes)
        {
            sb.AppendLine("_Deep type analysis available with `--depth deep` and a loadable Roslyn workspace._");
            sb.AppendLine();
            return;
        }

        foreach (var project in model.Projects)
        {
            if (project.Types.Count == 0) continue;

            sb.AppendLine($"## {project.Name}");

            foreach (var type in project.Types.OrderByDescending(t => t.RelevanceScore))
            {
                sb.AppendLine($"- **{type.Name}** ({type.Kind})");

                if (type.Methods.Count > 0)
                {
                    var methods = type.Methods
                        .OrderByDescending(m => m.RelevanceScore)
                        .Take(5);

                    foreach (var method in methods)
                    {
                        var mods = method.Modifiers.Count > 0 ? string.Join(" ", method.Modifiers) + " " : "";
                        var parms = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        sb.AppendLine($"  - {mods}{method.ReturnType} {method.Name}({parms})");
                    }

                    if (type.Methods.Count > 5)
                        sb.AppendLine($"  - *... and {type.Methods.Count - 5} more methods*");
                }
            }
        }
        sb.AppendLine();
    }

    private static int GetLayerOrder(string layer)
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
