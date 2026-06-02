using System.Text.Json.Serialization;

namespace DevContext.Core
{
    public class ExtractionOptions
    {
        public bool IncludeMethodSignatures { get; set; } = true;
        public bool IncludeDependencyGraph { get; set; } = true;
        public bool IncludeCallGraph { get; set; } = true;
        public bool IncludeDomainModel { get; set; } = true;
        public bool UseMermaidForGraphs { get; set; } = true;
        public bool TokenCompact { get; set; } = true;
        public string? OutputFilePath { get; set; } = null;
        public bool VerboseOutput { get; set; } = true;

        public bool EnableParallelProcessing { get; set; } = true;
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        public bool EnableFeatureGrouping { get; set; } = true;
        public ArchitectureStyle DetectedArchitecture { get; set; } = ArchitectureStyle.Unknown;
        public List<string> FeaturePatterns { get; set; } = new()
        {
            "Features/*", "Areas/*", "Modules/*", "Domain/*"
        };

        public List<string> ExcludeDirectories { get; set; } = new()
        {
            ".git", ".vs", ".nuke", ".github", "bin", "obj",
            ".idea", "nupkg", ".packageguard", "node_modules"
        };

        public List<string> ExcludeNamespaces { get; set; } = new()
        {
            "Migrations", "*.Migrations.*", "*.Tests.*", "*.Test.*"
        };

        public List<string> TrivialMethodPatterns { get; set; } = new()
        {
            "get_*", "set_*", "OnInitialized", "OnParametersSet", "Dispose"
        };

        public int MaxCallGraphDepth { get; set; } = int.MaxValue;
        public int MinMethodComplexity { get; set; } = 2;

        public bool ShowElapsedTime { get; set; } = true;
        public bool ShowMemoryUsage { get; set; } = true;
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Markdown;

        public ExtractionDepth Depth { get; set; } = ExtractionDepth.Balanced;
        public ExtractionFocus Focus { get; set; } = ExtractionFocus.General;
        public List<string> FocusedFeatures { get; set; } = new();
        public List<string> FocusedPaths { get; set; } = new();

        public void ApplyDepthAndFocusRules()
        {
            switch (Depth)
            {
                case ExtractionDepth.Shallow:
                    IncludeCallGraph = false;
                    IncludeDomainModel = false;
                    IncludeMethodSignatures = false;
                    MaxCallGraphDepth = 0;
                    EnableFeatureGrouping = true;
                    IncludeDependencyGraph = true;
                    break;
                case ExtractionDepth.Deep:
                    IncludeCallGraph = true;
                    IncludeDomainModel = true;
                    IncludeMethodSignatures = true;
                    MaxCallGraphDepth = Math.Max(MaxCallGraphDepth, 6);
                    break;
                case ExtractionDepth.Balanced:
                default:
                    if (MaxCallGraphDepth == int.MaxValue)
                        MaxCallGraphDepth = 3;
                    break;
            }

            switch (Focus)
            {
                case ExtractionFocus.Architecture:
                    EnableFeatureGrouping = true;
                    IncludeDependencyGraph = true;
                    if (Depth == ExtractionDepth.Shallow)
                    {
                        IncludeCallGraph = false;
                        IncludeMethodSignatures = false;
                    }
                    break;
                case ExtractionFocus.Feature:
                    EnableFeatureGrouping = true;
                    if (Depth != ExtractionDepth.Shallow)
                        IncludeCallGraph = true;
                    break;
                case ExtractionFocus.Debug:
                case ExtractionFocus.Implementation:
                    IncludeCallGraph = true;
                    IncludeDomainModel = true;
                    IncludeMethodSignatures = true;
                    break;
            }

            if (FocusedPaths.Count > 0 && Focus == ExtractionFocus.General)
                Focus = ExtractionFocus.Feature;
        }
    }

    public enum ArchitectureStyle
    {
        Unknown,
        MVC,
        MinimalApi,
        Blazor,
        RazorPages,
        FastEndpoints,
        CleanArchitecture,
        VerticalSlice,
        Modular
    }

    public enum OutputFormat
    {
        Markdown,
        Json,
        Html,
        PlainText
    }

    /// <summary>
    /// Controls how much detail and noise is included in the extraction.
    /// This is a key lever for making output useful when attached to LLM prompts.
    /// </summary>
    public enum ExtractionDepth
    {
        /// <summary>High-level architecture, layers, boundaries, key contracts. Minimal implementation detail.</summary>
        Shallow,

        /// <summary>Good balance for most "understand this codebase" use cases (default).</summary>
        Balanced,

        /// <summary>Maximum detail — deeper call graphs, more internals, less filtering. Use for deep implementation or debugging work.</summary>
        Deep
    }

    /// <summary>
    /// Expresses the user's intent for this extraction run.
    /// Allows the tool to make smart decisions about what to emphasize or de-emphasize.
    /// </summary>
    public enum ExtractionFocus
    {
        /// <summary>General purpose — current broad behavior.</summary>
        General,

        /// <summary>Focus on architecture, layers, system boundaries and high-level design.</summary>
        Architecture,

        /// <summary>Focus on one or more specific features / vertical slices.</summary>
        Feature,

        /// <summary>Deep implementation details inside selected areas.</summary>
        Implementation,

        /// <summary>Debugging-oriented — richer call graphs and cross-layer flows.</summary>
        Debug
    }
}
