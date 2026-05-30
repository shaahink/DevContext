using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevContext.Core.Extractors
{
    public class CodeStructureExtractor
    {
        private readonly ExtractionOptions _options;

        public CodeStructureExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Code Structure");

            // File structure
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)))
                .Select(f => Path.GetRelativePath(directory, f))
                .OrderBy(f => f)
                .ToList();

            sb.AppendLine($"**Total Files**: {csFiles.Count} .cs");

            // Group by directory with limit
            var filesByDirectory = csFiles.GroupBy(f => Path.GetDirectoryName(f) ?? "")
                .OrderBy(g => g.Key)
                .Take(int.MaxValue); // Limit directories shown

            foreach (var group in filesByDirectory)
            {
                var dirName = string.IsNullOrEmpty(group.Key) ? "Root" : group.Key;
                var files = group.Take(5).Select(Path.GetFileName).ToList();
                var fileList = string.Join(", ", files);

                if (group.Count() > 5)
                    fileList += $" ... (+{group.Count() - 5} more)";

                sb.AppendLine($"**{dirName}**: {fileList}");
            }

            // Method signatures if requested
            if (_options.IncludeMethodSignatures)
            {
                sb.AppendLine("\n## Key Method Signatures");
                await ExtractMethodSignaturesAsync(sb, directory);
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private async Task ExtractMethodSignaturesAsync(StringBuilder sb, string directory)
        {
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)))
                .Take(int.MaxValue); // Limit files analyzed

            var tasks = csFiles.Select(async file =>
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();
                    var relativePath = Path.GetRelativePath(directory, file);

                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .Where(m => m.Modifiers.Any(mod => mod.Text == "public") &&
                                   !IsTrivialMethod(m.Identifier.Text))
                        .Take(int.MaxValue); // Limit methods per file

                    if (!methods.Any())
                        return (string.Empty, new List<string>());

                    var signatures = new List<string>();
                    foreach (var method in methods)
                    {
                        var modifiers = string.Join(" ", method.Modifiers.Select(m => m.ValueText));
                        var parameters = _options.TokenCompact
                            ? string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var"))
                            : string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));

                        signatures.Add($"- {modifiers} {method.ReturnType} {method.Identifier}({parameters})");
                    }

                    return (relativePath, signatures);
                }
                catch
                {
                    return (string.Empty, new List<string>());
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results.Where(r => r.Item2.Any()))
            {
                sb.AppendLine($"\n### {result.Item1}");
                foreach (var sig in result.Item2)
                {
                    sb.AppendLine(sig);
                }
            }
        }

        private bool IsTrivialMethod(string methodName)
        {
            return _options.TrivialMethodPatterns.Any(pattern =>
                Regex.IsMatch(methodName, pattern.Replace("*", ".*")));
        }
    }
}
