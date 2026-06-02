using Microsoft.CodeAnalysis;

namespace DevContext.Core.Models;

public sealed class CodeModel
{
    public Solution? Solution { get; set; }
    public string RootDirectory { get; set; } = "";
    public List<ProjectModel> Projects { get; set; } = new();
    public List<CallGraphEdge> CallGraph { get; set; } = new();
    public List<DependencyEdge> Dependencies { get; set; } = new();
    public List<FeatureGroup> Features { get; set; } = new();
    public Dictionary<string, int> LayerCounts { get; set; } = new();
}

public sealed class ProjectModel
{
    public string Name { get; set; } = "";
    public string AssemblyName { get; set; } = "";
    public string TargetFramework { get; set; } = "";
    public List<string> NuGetPackages { get; set; } = new();
    public List<string> ProjectReferences { get; set; } = new();
    public List<TypeModel> Types { get; set; } = new();
    public bool IsCliTool { get; set; }
    public string? CliCommandName { get; set; }
}

public sealed class TypeModel
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public TypeKind Kind { get; set; }
    public string FilePath { get; set; } = "";
    public List<MethodModel> Methods { get; set; } = new();
    public double RelevanceScore { get; set; }
}

public enum TypeKind
{
    Class, Interface, Struct, Enum, Record
}

public sealed class MethodModel
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public List<ParameterModel> Parameters { get; set; } = new();
    public List<string> Modifiers { get; set; } = new();
    public bool IsPublic { get; set; }
    public string ContainingType { get; set; } = "";
    public double RelevanceScore { get; set; }
}

public sealed class ParameterModel
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class CallGraphEdge
{
    public string CallerMethod { get; set; } = "";
    public string CalleeMethod { get; set; } = "";
    public string CallerType { get; set; } = "";
    public string CalleeType { get; set; } = "";
    public string? Feature { get; set; }
    public int CallCount { get; set; } = 1;
}

public sealed class DependencyEdge
{
    public string SourceProject { get; set; } = "";
    public string TargetProject { get; set; } = "";
    public DependencyType Type { get; set; }
}

public enum DependencyType { ProjectReference, Package }

public sealed class FeatureGroup
{
    public string Name { get; set; } = "";
    public List<string> FilePaths { get; set; } = new();
    public List<string> TypeNames { get; set; } = new();
    public List<string> Endpoints { get; set; } = new();
    public double RelevanceScore { get; set; }
}
