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

            // 3. Source Files Structure
            sb.AppendLine("\n# Source Files Structure");
            var excludePatterns = new[] { ".git", ".vs", ".nuke", ".github", "bin", "obj", ".idea", "nupkg", ".packageguard" };

            // Get all .cs files with their relative paths
            var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                                  .Where(f => !excludePatterns.Any(ep => f.Contains(ep)))
                                  .Select(f => Path.GetRelativePath(directory, f))
                                  .OrderBy(f => f)
                                  .ToList();

            // Group by directory for better organization
            var filesByDirectory = csFiles.GroupBy(f => Path.GetDirectoryName(f) ?? "")
                                         .OrderBy(g => g.Key);

            foreach (var group in filesByDirectory)
            {
                var dirName = string.IsNullOrEmpty(group.Key) ? "Root" : group.Key;
                sb.AppendLine($"## {dirName}");
                foreach (var file in group)
                {
                    sb.AppendLine($"- {Path.GetFileName(file)}");
                }
            }

            sb.AppendLine($"\n**Total Source Files**: {csFiles.Count} .cs files");

            // 4. Folder Structure (Non-source folders)
            sb.AppendLine("\n# Project Folder Structure");
            var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                                  .Where(f => !excludePatterns.Any(ep => f.Contains($"{Path.DirectorySeparatorChar}{ep}{Path.DirectorySeparatorChar}") || Path.GetFileName(f) == ep))
                                  .Select(f => Path.GetRelativePath(directory, f))
                                  .OrderBy(f => f);
            foreach (var folder in folders)
            {
                sb.AppendLine($"- {folder}");
            }

            // 5. Code Summary with Method Signatures
            sb.AppendLine("\n# Code Structure & Method Signatures");
            var totalLoc = 0;
            var fullCsFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                                     .Where(f => !excludePatterns.Any(ep => f.Contains(ep)))
                                     .ToList();

            foreach (var file in fullCsFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    totalLoc += code.Split('\n').Length;
                    var relativePath = Path.GetRelativePath(directory, file);

                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();

                    sb.AppendLine($"\n## File: {relativePath}");

                    var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                    foreach (var ns in namespaces)
                    {
                        var nsName = ns.Name.ToString();
                        sb.AppendLine($"### Namespace: {nsName}");

                        // Classes
                        var classes = ns.DescendantNodes().OfType<ClassDeclarationSyntax>();
                        foreach (var cls in classes)
                        {
                            var modifiers = string.Join(" ", cls.Modifiers.Select(m => m.ValueText));
                            var baseTypes = cls.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
                            var inheritance = baseTypes.Any() ? $" : {string.Join(", ", baseTypes)}" : "";

                            sb.AppendLine($"#### {modifiers} class {cls.Identifier.Text}{inheritance}");
                            AppendMembersWithSignatures(sb, cls.Members);
                        }

                        // Interfaces
                        var interfaces = ns.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
                        foreach (var iface in interfaces)
                        {
                            var modifiers = string.Join(" ", iface.Modifiers.Select(m => m.ValueText));
                            sb.AppendLine($"#### {modifiers} interface {iface.Identifier.Text}");
                            AppendMembersWithSignatures(sb, iface.Members);
                        }

                        // Enums
                        var enums = ns.DescendantNodes().OfType<EnumDeclarationSyntax>();
                        foreach (var en in enums)
                        {
                            var modifiers = string.Join(" ", en.Modifiers.Select(m => m.ValueText));
                            sb.AppendLine($"#### {modifiers} enum {en.Identifier.Text}");
                            sb.AppendLine($"  - Values: {string.Join(", ", en.Members.Select(m => m.Identifier))}");
                        }

                        // Structs
                        var structs = ns.DescendantNodes().OfType<StructDeclarationSyntax>();
                        foreach (var strct in structs)
                        {
                            var modifiers = string.Join(" ", strct.Modifiers.Select(m => m.ValueText));
                            sb.AppendLine($"#### {modifiers} struct {strct.Identifier.Text}");
                            AppendMembersWithSignatures(sb, strct.Members);
                        }
                    }

                    // Handle classes/interfaces not in explicit namespaces (file-scoped namespaces or global)
                    var topLevelClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .Where(c => c.Parent is CompilationUnitSyntax || c.Parent is FileScopedNamespaceDeclarationSyntax);

                    foreach (var cls in topLevelClasses)
                    {
                        var modifiers = string.Join(" ", cls.Modifiers.Select(m => m.ValueText));
                        sb.AppendLine($"#### {modifiers} class {cls.Identifier.Text}");
                        AppendMembersWithSignatures(sb, cls.Members);
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

        private void AppendMembersWithSignatures(StringBuilder sb, SyntaxList<MemberDeclarationSyntax> members)
        {
            // Constructors
            var ctors = members.OfType<ConstructorDeclarationSyntax>();
            foreach (var ctor in ctors)
            {
                var modifiers = string.Join(" ", ctor.Modifiers.Select(m => m.ValueText));
                var parameters = string.Join(", ", ctor.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                sb.AppendLine($"  - {modifiers} ctor({parameters})");
            }

            // Methods (both public and private)
            var methods = members.OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var modifiers = string.Join(" ", method.Modifiers.Select(m => m.ValueText));
                var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                sb.AppendLine($"  - {modifiers} {method.ReturnType} {method.Identifier}({parameters})");
            }

            // Properties (both public and private)
            var props = members.OfType<PropertyDeclarationSyntax>();
            foreach (var prop in props)
            {
                var modifiers = string.Join(" ", prop.Modifiers.Select(m => m.ValueText));
                var accessors = prop.AccessorList?.Accessors.Select(a => a.Keyword.ValueText) ?? Enumerable.Empty<string>();
                var accessorInfo = accessors.Any() ? $" {{ {string.Join("; ", accessors)} }}" : "";
                sb.AppendLine($"  - {modifiers} {prop.Type} {prop.Identifier}{accessorInfo}");
            }

            // Fields (both public and private)
            var fields = members.OfType<FieldDeclarationSyntax>();
            foreach (var field in fields)
            {
                var modifiers = string.Join(" ", field.Modifiers.Select(m => m.ValueText));
                var variables = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.ValueText));
                sb.AppendLine($"  - {modifiers} {field.Declaration.Type} {variables}");
            }

            // Events
            var events = members.OfType<EventDeclarationSyntax>();
            foreach (var evt in events)
            {
                var modifiers = string.Join(" ", evt.Modifiers.Select(m => m.ValueText));
                sb.AppendLine($"  - {modifiers} event {evt.Type} {evt.Identifier}");
            }
        }
    }
}
