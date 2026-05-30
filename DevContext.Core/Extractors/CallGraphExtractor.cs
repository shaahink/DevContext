using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevContext.Core.Extractors
{
    public class CallGraphExtractor
    {
        private readonly ExtractionOptions _options;

        public CallGraphExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(Solution solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Call Graph");

            // In shallow or pure architecture mode, we intentionally produce very little here
            // (the orchestrator already disables the extractor in many shallow cases).
            if (_options.Depth == ExtractionDepth.Shallow || _options.Focus == ExtractionFocus.Architecture)
            {
                sb.AppendLine("_Call graph omitted for this profile (shallow/architecture focus)._");
                sb.AppendLine();
                return sb.ToString();
            }

            var calls = new ConcurrentBag<(string caller, string callee, string feature)>();

            // Process projects in parallel
            var tasks = solution.Projects.Select(async project =>
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    return;

                // Process documents in parallel within each project
                var docTasks = project.Documents.Select(async doc =>
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        return;

                    var root = await tree.GetRootAsync();
                    var model = compilation.GetSemanticModel(tree);

                    // Find all method declarations
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .Where(m => !IsTrivialMethod(m));

                    foreach (var method in methods)
                    {
                        var methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
                        if (methodSymbol == null || ShouldExcludeMethod(methodSymbol))
                            continue;

                        var callerName = GetFullMethodName(methodSymbol);
                        var feature = ExtractFeatureFromNamespace(methodSymbol.ContainingNamespace?.ToString() ?? "");

                        // Find all invocations within this method
                        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

                        foreach (var invocation in invocations)
                        {
                            var invokedSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                            if (invokedSymbol == null || ShouldExcludeMethod(invokedSymbol))
                                continue;

                            // Filter to only track internal calls (within solution)
                            if (invokedSymbol.ContainingAssembly.Name == compilation.AssemblyName ||
                                solution.Projects.Any(p => p.AssemblyName == invokedSymbol.ContainingAssembly.Name))
                            {
                                var calleeName = GetFullMethodName(invokedSymbol);
                                calls.Add((callerName, calleeName, feature));
                            }
                        }
                    }
                });

                await Task.WhenAll(docTasks);
            });

            await Task.WhenAll(tasks);

            // Group by feature if enabled
            if (_options.EnableFeatureGrouping)
            {
                var callsByFeature = calls.GroupBy(c => c.feature)
                    .OrderBy(g => g.Key);

                foreach (var featureGroup in callsByFeature)
                {
                    sb.AppendLine($"## {featureGroup.Key}");

                    var featureCalls = featureGroup
                        .GroupBy(c => c.caller)
                        .OrderBy(g => g.Key);

                    foreach (var group in featureCalls)
                    {
                        var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                        sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                // Regular grouping
                var groupedCalls = calls.GroupBy(c => c.caller)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedCalls)
                {
                    var callees = string.Join(", ", group.Select(c => GetShortName(c.callee)).Distinct());
                    sb.AppendLine($"{GetShortName(group.Key)} -> {callees}");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private bool IsTrivialMethod(MethodDeclarationSyntax method)
        {
            var name = method.Identifier.Text;
            return _options.TrivialMethodPatterns.Any(pattern =>
                Regex.IsMatch(name, pattern.Replace("*", ".*")));
        }

        private bool ShouldExcludeMethod(IMethodSymbol method)
        {
            var ns = method.ContainingNamespace?.ToString() ?? "";
            return _options.ExcludeNamespaces.Any(pattern =>
                pattern.Contains("*")
                    ? Regex.IsMatch(ns, pattern.Replace("*", ".*"))
                    : ns.Contains(pattern));
        }

        private string ExtractFeatureFromNamespace(string ns)
        {
            // Try to extract feature from namespace pattern
            foreach (var pattern in _options.FeaturePatterns)
            {
                var regex = new Regex(pattern.Replace("*", @"(\w+)"));
                var match = regex.Match(ns);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            // Fallback to second-level namespace
            var parts = ns.Split('.');
            return parts.Length >= 2 ? parts[1] : "Core";
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
    }
}
