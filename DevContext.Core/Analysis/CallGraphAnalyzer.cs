using System.Collections.Concurrent;
using DevContext.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevContext.Core.Analysis;

public sealed class CallGraphAnalyzer : IAnalyzer
{
    private readonly ExtractionOptions _options;

    public CallGraphAnalyzer(ExtractionOptions options)
    {
        _options = options;
    }

    public string Name => "Call Graph";

    public async Task AnalyzeAsync(CodeModel model, CancellationToken ct = default)
    {
        var solution = model.Solution;
        if (solution == null) return;

        var edges = new ConcurrentBag<CallGraphEdge>();

        await Parallel.ForEachAsync(solution.Projects, ct, async (project, ct) =>
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null) return;

            await Parallel.ForEachAsync(project.Documents, ct, async (doc, ct) =>
            {
                var tree = await doc.GetSyntaxTreeAsync(ct);
                if (tree == null) return;

                var root = await tree.GetRootAsync(ct);
                var semanticModel = compilation.GetSemanticModel(tree);

                var methods = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>();

                foreach (var method in methods)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(method, ct) as IMethodSymbol;
                    if (symbol == null || ShouldExclude(symbol))
                        continue;

                    var caller = $"{symbol.ContainingType}.{symbol.Name}";

                    var invocations = method.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>();

                    foreach (var invocation in invocations)
                    {
                        var target = semanticModel.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
                        if (target == null || ShouldExclude(target))
                            continue;

                        if (!IsInSolution(target, solution))
                            continue;

                        edges.Add(new CallGraphEdge
                        {
                            CallerMethod = symbol.Name,
                            CallerType = symbol.ContainingType.ToDisplayString(),
                            CalleeMethod = target.Name,
                            CalleeType = target.ContainingType.ToDisplayString(),
                            Feature = ExtractFeatureGroup(symbol),
                            CallCount = 1
                        });
                    }
                }
            });
        });

        model.CallGraph = edges.ToList();
    }

    private static bool IsInSolution(IMethodSymbol method, Solution solution)
    {
        var asm = method.ContainingAssembly?.Name;
        return asm != null && solution.Projects.Any(p => p.AssemblyName == asm);
    }

    private bool ShouldExclude(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToString() ?? "";
        return _options.ExcludeNamespaces.Any(pattern =>
            pattern.Contains('*')
                ? System.Text.RegularExpressions.Regex.IsMatch(ns, pattern.Replace("*", ".*"))
                : ns.Contains(pattern));
    }

    private static string? ExtractFeatureGroup(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToString() ?? "";
        var parts = ns.Split('.');
        return parts.Length >= 2 ? parts[1] : null;
    }
}
