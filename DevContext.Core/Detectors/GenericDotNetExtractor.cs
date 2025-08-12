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
    public class ExtractionOptions
    {
        public bool IncludeMethodSignatures { get; set; } = false; // Default: false to save tokens
        public string? OutputFilePath { get; set; } = null; // Default: output to console
        public bool VerboseOutput { get; set; } = false;
    }

    public class GenericDotNetProjectDetector : IProjectDetector
    {
        private readonly ExtractionOptions _options;

        public GenericDotNetProjectDetector(ExtractionOptions? options = null)
        {
            _options = options ?? new ExtractionOptions();
        }

        public string Id => "generic-dotnet";

        public bool Detect(string directory)
        {
            return Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).Any() ||
                   Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any();
        }

        public ExtractionResult Extract(string directory)
        {
            var sb = new StringBuilder();

            // LLM Context Header
            sb.AppendLine("# DevContext - .NET Project Analysis");
            sb.AppendLine("**Purpose**: This file contains extracted context from a .NET solution to help Large Language Models (LLMs) understand the codebase structure, dependencies, and architecture without needing to analyze individual source files.");
            sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Method Signatures Included**: {(_options.IncludeMethodSignatures ? "Yes" : "No (use --include-signatures for detailed method info)")}");
            sb.AppendLine();

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

            // 5. Code Summary - Conditional based on options
            if (_options.IncludeMethodSignatures)
            {
                sb.AppendLine("\n# Detailed Code Structure & Method Signatures");
                ExtractDetailedCodeStructure(sb, directory, excludePatterns);
            }
            else
            {
                sb.AppendLine("\n# Code Structure Summary (Compact)");
                ExtractCompactCodeStructure(sb, directory, excludePatterns);
            }

            // Save to file if specified
            var result = new ExtractionResult("generic-dotnet", sb.ToString());

            if (!string.IsNullOrEmpty(_options.OutputFilePath))
            {
                try
                {
                    File.WriteAllText(_options.OutputFilePath, result.Content);
                    Console.WriteLine($"✅ Context saved to: {_options.OutputFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to save to file: {ex.Message}");
                }
            }

            return result;
        }

        private void ExtractCompactCodeStructure(StringBuilder sb, string directory, string[] excludePatterns)
        {
            var totalLoc = 0;
            var fullCsFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                                     .Where(f => !excludePatterns.Any(ep => f.Contains(ep)))
                                     .ToList();

            var typesSummary = new Dictionary<string, List<string>>();

            foreach (var file in fullCsFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    totalLoc += code.Split('\n').Length;
                    var relativePath = Path.GetRelativePath(directory, file);

                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();

                    // Extract types in compact format
                    var fileTypes = new List<string>();

                    // Handle all types (in namespaces and top-level)
                    var allTypes = root.DescendantNodes()
                        .Where(n => n is ClassDeclarationSyntax ||
                                   n is InterfaceDeclarationSyntax ||
                                   n is EnumDeclarationSyntax ||
                                   n is StructDeclarationSyntax)
                        .Cast<BaseTypeDeclarationSyntax>();

                    foreach (var type in allTypes)
                    {
                        var typeName = type.Identifier.ValueText;
                        var typeKind = type switch
                        {
                            ClassDeclarationSyntax => "class",
                            InterfaceDeclarationSyntax => "interface",
                            EnumDeclarationSyntax => "enum",
                            StructDeclarationSyntax => "struct",
                            _ => "type"
                        };

                        //var memberCount = type.Members.Count;
                        //var publicMemberCount = type.Members.Count(m =>
                        //    m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

                        //fileTypes.Add($"{typeKind} {typeName} ({memberCount}m/{publicMemberCount}p)");
                    }

                    if (fileTypes.Any())
                    {
                        typesSummary[relativePath] = fileTypes;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error parsing {file}: {ex.Message}");
                }
            }

            // Output compact summary
            foreach (var kvp in typesSummary.OrderBy(x => x.Key))
            {
                sb.AppendLine($"**{kvp.Key}**: {string.Join(", ", kvp.Value)}");
            }

            sb.AppendLine($"\n**Legend**: (Xm/Yp) = X total members, Y public members");
            sb.AppendLine($"**Total Lines of Code (approx)**: {totalLoc}");
        }

        private void ExtractDetailedCodeStructure(StringBuilder sb, string directory, string[] excludePatterns)
        {
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

                    // Handle namespaces
                    var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                    foreach (var ns in namespaces)
                    {
                        var nsName = ns.Name.ToString();
                        sb.AppendLine($"### Namespace: {nsName}");
                        ProcessTypesInContainer(sb, ns);
                    }

                    // Handle file-scoped namespaces
                    var fileScopedNamespaces = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>();
                    foreach (var ns in fileScopedNamespaces)
                    {
                        var nsName = ns.Name.ToString();
                        sb.AppendLine($"### Namespace: {nsName} (file-scoped)");
                        ProcessTypesInContainer(sb, ns);
                    }

                    // Handle top-level types (not in any namespace)
                    var topLevelTypes = root.ChildNodes()
                        .Where(n => n is ClassDeclarationSyntax ||
                                   n is InterfaceDeclarationSyntax ||
                                   n is EnumDeclarationSyntax ||
                                   n is StructDeclarationSyntax);

                    foreach (var type in topLevelTypes)
                    {
                        ProcessType(sb, type);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error parsing {file}: {ex.Message}");
                }
            }
            sb.AppendLine($"\n**Total Lines of Code (approx)**: {totalLoc}");
        }

        private void ProcessTypesInContainer(StringBuilder sb, SyntaxNode container)
        {
            var types = container.ChildNodes()
                .Where(n => n is ClassDeclarationSyntax ||
                           n is InterfaceDeclarationSyntax ||
                           n is EnumDeclarationSyntax ||
                           n is StructDeclarationSyntax);

            foreach (var type in types)
            {
                ProcessType(sb, type);
            }
        }

        private void ProcessType(StringBuilder sb, SyntaxNode typeNode)
        {
            switch (typeNode)
            {
                case ClassDeclarationSyntax cls:
                    var classModifiers = string.Join(" ", cls.Modifiers.Select(m => m.ValueText));
                    var baseTypes = cls.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
                    var inheritance = baseTypes.Any() ? $" : {string.Join(", ", baseTypes)}" : "";
                    sb.AppendLine($"#### {classModifiers} class {cls.Identifier.Text}{inheritance}");
                    AppendMembersWithSignatures(sb, cls.Members);
                    break;

                case InterfaceDeclarationSyntax iface:
                    var ifaceModifiers = string.Join(" ", iface.Modifiers.Select(m => m.ValueText));
                    var ifaceBaseTypes = iface.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
                    var ifaceInheritance = ifaceBaseTypes.Any() ? $" : {string.Join(", ", ifaceBaseTypes)}" : "";
                    sb.AppendLine($"#### {ifaceModifiers} interface {iface.Identifier.Text}{ifaceInheritance}");
                    AppendMembersWithSignatures(sb, iface.Members);
                    break;

                case EnumDeclarationSyntax en:
                    var enumModifiers = string.Join(" ", en.Modifiers.Select(m => m.ValueText));
                    sb.AppendLine($"#### {enumModifiers} enum {en.Identifier.Text}");
                    sb.AppendLine($"  - Values: {string.Join(", ", en.Members.Select(m => m.Identifier))}");
                    break;

                case StructDeclarationSyntax strct:
                    var structModifiers = string.Join(" ", strct.Modifiers.Select(m => m.ValueText));
                    sb.AppendLine($"#### {structModifiers} struct {strct.Identifier.Text}");
                    AppendMembersWithSignatures(sb, strct.Members);
                    break;
            }
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
