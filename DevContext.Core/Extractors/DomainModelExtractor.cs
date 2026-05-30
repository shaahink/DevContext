using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevContext.Core.Extractors
{
    public class DomainModelExtractor
    {
        private readonly ExtractionOptions _options;

        public DomainModelExtractor(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<string> ExtractAsync(string directory, Solution? solution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Domain Model");

            var entities = new ConcurrentBag<string>();
            var enums = new ConcurrentBag<string>();
            var dtos = new ConcurrentBag<string>();

            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_options.ExcludeDirectories.Any(ep => f.Contains(ep)));

            var tasks = csFiles.Select(async file =>
            {
                try
                {
                    var code = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync();

                    // Find entities
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var cls in classes)
                    {
                        var name = cls.Identifier.Text;
                        if (name.EndsWith("Entity") || name.EndsWith("Model") || name.EndsWith("Aggregate"))
                            entities.Add(name);
                        else if (name.EndsWith("Dto") || name.EndsWith("DTO") ||
                                name.EndsWith("Request") || name.EndsWith("Response") ||
                                name.EndsWith("Command") || name.EndsWith("Query"))
                            dtos.Add(name);
                    }

                    // Find enums
                    var enumTypes = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
                    foreach (var en in enumTypes)
                    {
                        enums.Add(en.Identifier.Text);
                    }
                }
                catch { }
            });

            await Task.WhenAll(tasks);

            if (entities.Any())
            {
                sb.AppendLine("## Entities");
                foreach (var entity in entities.Distinct().OrderBy(e => e).Take(int.MaxValue))
                    sb.AppendLine($"- {entity}");
            }

            if (enums.Any())
            {
                sb.AppendLine("\n## Enums");
                foreach (var en in enums.Distinct().OrderBy(e => e).Take(int.MaxValue))
                    sb.AppendLine($"- {en}");
            }

            if (dtos.Any())
            {
                sb.AppendLine("\n## DTOs/Commands/Queries");
                foreach (var dto in dtos.Distinct().OrderBy(d => d).Take(int.MaxValue))
                    sb.AppendLine($"- {dto}");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
