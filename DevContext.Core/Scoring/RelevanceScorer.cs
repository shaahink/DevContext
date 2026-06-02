using DevContext.Core.Models;

namespace DevContext.Core.Scoring;

public sealed class RelevanceScorer
{
    private readonly ExtractionOptions _options;

    public RelevanceScorer(ExtractionOptions options)
    {
        _options = options;
    }

    public void Score(CodeModel model)
    {
        // Focused paths get highest score
        var focusPaths = _options.FocusedPaths
            .Select(p => p.Replace('\\', '/').ToLowerInvariant().TrimEnd('/'))
            .ToList();

        // Focused features get a boost
        var focusFeatures = _options.FocusedFeatures
            .Select(f => f.ToLowerInvariant())
            .ToList();

        foreach (var project in model.Projects)
        {
            foreach (var type in project.Types)
            {
                var score = 1.0;

                var normalizedPath = type.FilePath.Replace('\\', '/').ToLowerInvariant();

                // Boost by proximity to --around paths
                if (focusPaths.Count > 0)
                {
                    var isNear = focusPaths.Any(fp =>
                        normalizedPath.Contains(fp) || fp.Contains(normalizedPath));

                    if (isNear)
                        score += 5.0;
                }

                // Boost by feature match
                if (focusFeatures.Count > 0)
                {
                    var typeLower = type.FullName.ToLowerInvariant();
                    if (focusFeatures.Any(f => typeLower.Contains(f)))
                        score += 3.0;
                }

                // Boost types in the call graph
                if (model.CallGraph.Any(e =>
                        e.CallerType.Contains(type.Name) || e.CalleeType.Contains(type.Name)))
                {
                    score += 2.0;
                }

                // Depth-based adjustments
                if (_options.Depth == ExtractionDepth.Shallow && score < 3.0)
                    score *= 0.3;

                type.RelevanceScore = score;

                foreach (var method in type.Methods)
                {
                    method.RelevanceScore = score;

                    // Boost methods that appear in call graph
                    if (model.CallGraph.Any(e => e.CallerMethod == method.Name || e.CalleeMethod == method.Name))
                        method.RelevanceScore += 2.0;
                }
            }
        }

        // Score features
        foreach (var feature in model.Features)
        {
            feature.RelevanceScore = 1.0;

            if (focusFeatures.Count > 0 && focusFeatures.Contains(feature.Name.ToLowerInvariant()))
                feature.RelevanceScore += 5.0;
        }
    }
}
