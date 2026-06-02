using DevContext.Core.Models;

namespace DevContext.Core.Scoring;

public sealed class RelevanceFilter
{
    private readonly ExtractionOptions _options;

    public RelevanceFilter(ExtractionOptions options)
    {
        _options = options;
    }

    public void ApplyBudget(CodeModel model)
    {
        var depth = _options.Depth;
        var focus = _options.Focus;

        // Remove low-relevance features
        model.Features.RemoveAll(f => f.RelevanceScore < 0.5);

        if (focus == ExtractionFocus.Architecture || depth == ExtractionDepth.Shallow)
        {
            model.CallGraph.Clear();
        }

        if (depth == ExtractionDepth.Shallow)
        {
            foreach (var project in model.Projects)
            {
                project.Types.RemoveAll(t => t.RelevanceScore < 2.0);
                foreach (var type in project.Types)
                {
                    type.Methods.RemoveAll(m => m.RelevanceScore < 2.0);
                }
            }
        }

        if (focus == ExtractionFocus.Feature)
        {
            model.Features.RemoveAll(f => f.RelevanceScore < 3.0);
        }

        if (depth == ExtractionDepth.Deep)
        {
            // Keep everything — deep means exhaustive
        }
    }
}
