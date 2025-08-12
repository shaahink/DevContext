using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevContext.Core
{
    public class GenericDotNetProjectDetector : IProjectDetector
    {
        public string Id => "generic-dotnet";

        public bool Detect(string directory)
        {
            return Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).Any() ||
                   Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any();
        }

        public ExtractionResult Extract(string directory)
        {
            var sb = new StringBuilder();

            // Initialize MSBuild
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            // 1. Project Overview
            sb.AppendLine("# Solution Overview");
            sb.AppendLine($"**Root Directory**: {directory}");

            // Solution File
            var slnFiles = Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).ToList();
            var slnPath = slnFiles.FirstOrDefault();
            MSBuildWorkspace? workspace = null;
            Solution? solution = null;
            if (slnPath != null)
            {
                try
                {
                    workspace = MSBuildWorkspace.Create();
                    solution = workspace.OpenSolutionAsync(slnPath).Result;
                    sb.AppendLine($"**Solution File**: {Path.GetFileName(slnPath)}");
                    sb.AppendLine($"**Projects in Solution**: {solution.Projects.Count()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load solution {slnPath}: {ex.Message}");
                }
            }
            else
            {
                sb.AppendLine("**Solution File**: Not found");
            }

            // Project Files and Metadata
            var csprojFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).ToList();
            sb.AppendLine($"**Total Projects**: {csprojFiles.Count}");

            var frameworks = new HashSet<string>();
            var isCliTool = false;
            string cliCommandName = string.Empty;
            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var doc = XDocument.Load(csproj);
                    var tf = doc.Descendants("TargetFramework").FirstOrDefault()?.Value ??
                             doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault();
                    if (!string.IsNullOrEmpty(tf))
                        frameworks.Add(tf);

                    var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
                    var toolCommand = doc.Descendants("ToolCommandName").FirstOrDefault()?.Value;
                    if (outputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true &&
                        toolCommand != null)
                    {
                        isCliTool = true;
                        cliCommandName = toolCommand;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error reading {csproj}: {ex.Message}");
                }
            }

            sb.AppendLine($"**Target Frameworks**: {string.Join(", ", frameworks)}");
            sb.AppendLine($"**Project Type**: {(isCliTool ? $"CLI Tool (Command: {cliCommandName})" : "Library or Application")}");
            if (isCliTool)
            {
                sb.AppendLine($"**CLI Command**: `dotnet run --project {cliCommandName}`");
            }

            // 2. Dependencies
            sb.AppendLine("\n# Dependencies");

            // NuGet Packages
            var allPackages = new Dictionary<string, List<string>>(); // PackageName -> Versions
            var projectReferences = new List<string>();
            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var doc = XDocument.Load(csproj);
                    var csprojName = Path.GetFileName(csproj);

                    // NuGet Packages
                    foreach (var pkg in doc.Descendants("PackageReference"))
                    {
                        var name = pkg.Attribute("Include")?.Value;
                        var ver = pkg.Attribute("Version")?.Value;
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!allPackages.ContainsKey(name))
                                allPackages[name] = new List<string>();
                            allPackages[name].Add(ver ?? "unknown");
                        }
                    }

                    // Project References
                    foreach (var projRef in doc.Descendants("ProjectReference"))
                    {
                        var refPath = projRef.Attribute("Include")?.Value;
                        if (!string.IsNullOrEmpty(refPath))
                        {
                            var refName = Path.GetFileNameWithoutExtension(refPath);
                            projectReferences.Add($"{csprojName} -> {refName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error reading {csproj}: {ex.Message}");
                }
            }

            sb.AppendLine("## NuGet Packages (Deduplicated)");
            var corePackages = allPackages.Where(p => !p.Key.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                                                    !p.Key.Contains("xunit", StringComparison.OrdinalIgnoreCase))
                                        .OrderBy(p => p.Key);
            var testPackages = allPackages.Except(corePackages).OrderBy(p => p.Key);

            if (corePackages.Any())
            {
                sb.AppendLine("### Core Packages");
                foreach (var pkg in corePackages)
                {
                    sb.AppendLine($"- {pkg.Key} ({string.Join("/", pkg.Value.Distinct())})");
                }
            }
            if (testPackages.Any())
            {
                sb.AppendLine("### Test/Build Packages");
                foreach (var pkg in testPackages)
                {
                    sb.AppendLine($"- {pkg.Key} ({string.Join("/", pkg.Value.Distinct())})");
                }
            }

            if (projectReferences.Any())
            {
                sb.AppendLine("\n## Project References");
                foreach (var projectReference in projectReferences.Distinct())
                {
                    sb.AppendLine($"- {projectReference}");
                }
            }

            // 3. Folder Structure
            sb.AppendLine("\n# Source Folder Structure");
            var excludePatterns = new[] { ".git", ".vs", ".nuke", ".github", "bin", "obj", ".idea", "nupkg", ".packageguard" };
            var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                                  .Where(f => !excludePatterns.Any(ep => f.Contains($"{Path.DirectorySeparatorChar}{ep}{Path.DirectorySeparatorChar}") || Path.GetFileName(f) == ep))
                                  .Select(f => Path.GetRelativePath(directory, f))
                                  .OrderBy(f => f);
            foreach (var folder in folders)
            {
                sb.AppendLine($"- {folder}");
            }

            // File Metrics
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                                  .Where(f => !excludePatterns.Any(ep => f.Contains(ep)))
                                  .ToList();
            sb.AppendLine($"\n**Source Files**: {csFiles.Count} .cs files");

            // 4. Code Summary
            sb.AppendLine("\n# Code Summary");
            var totalLoc = 0;
            foreach (var file in csFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    totalLoc += code.Split('\n').Length;

                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();

                    var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                    foreach (var ns in namespaces)
                    {
                        var nsName = ns.Name.ToString();
                        sb.AppendLine($"## Namespace: {nsName}");

                        // Classes
                        var classes = ns.DescendantNodes().OfType<ClassDeclarationSyntax>();
                        foreach (var cls in classes)
                        {
                            sb.AppendLine($"### Class: {cls.Identifier.Text}");
                            AppendMembers(sb, cls.Members);
                        }

                        // Interfaces
                        var interfaces = ns.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
                        foreach (var iface in interfaces)
                        {
                            sb.AppendLine($"### Interface: {iface.Identifier.Text}");
                            AppendMembers(sb, iface.Members);
                        }

                        // Enums
                        var enums = ns.DescendantNodes().OfType<EnumDeclarationSyntax>();
                        foreach (var en in enums)
                        {
                            sb.AppendLine($"### Enum: {en.Identifier.Text} ({string.Join(", ", en.Members.Select(m => m.Identifier))})");
                        }

                        // Structs
                        var structs = ns.DescendantNodes().OfType<StructDeclarationSyntax>();
                        foreach (var strct in structs)
                        {
                            sb.AppendLine($"### Struct: {strct.Identifier.Text}");
                            AppendMembers(sb, strct.Members);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error parsing {file}: {ex.Message}");
                }
            }
            sb.AppendLine($"\n**Total Lines of Code (approx)**: {totalLoc}");

            // Cleanup
            workspace?.Dispose();
            return new ExtractionResult("generic-dotnet", sb.ToString());
        }

        private void AppendMembers(StringBuilder sb, SyntaxList<MemberDeclarationSyntax> members)
        {
            // Constructors
            var ctors = members.OfType<ConstructorDeclarationSyntax>()
                               .Where(c => c.Modifiers.Any(SyntaxKind.PublicKeyword))
                               .Select(c => $"ctor({string.Join(", ", c.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"))})");
            if (ctors.Any())
                sb.AppendLine($"  - Constructors: {string.Join("; ", ctors)}");

            // Methods
            var methods = members.OfType<MethodDeclarationSyntax>()
                                 .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword))
                                 .Select(m => $"{m.ReturnType} {m.Identifier}({string.Join(", ", m.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"))})");
            if (methods.Any())
                sb.AppendLine($"  - Methods: {string.Join("; ", methods)}");

            // Properties
            var props = members.OfType<PropertyDeclarationSyntax>()
                               .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword))
                               .Select(p => $"{p.Type} {p.Identifier}");
            if (props.Any())
                sb.AppendLine($"  - Properties: {string.Join("; ", props)}");

            // Fields
            var fields = members.OfType<FieldDeclarationSyntax>()
                                .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword))
                                .Select(f => string.Join(", ", f.Declaration.Variables.Select(v => $"{f.Declaration.Type} {v.Identifier}")));
            if (fields.Any())
                sb.AppendLine($"  - Fields: {string.Join("; ", fields)}");
        }
    }
}
