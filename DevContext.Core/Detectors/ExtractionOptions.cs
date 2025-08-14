namespace DevContext.Core
{
    public class ExtractionOptions
    {
        public bool TokenCompact { get; set; } = true;
        public bool IncludeMethodSignatures { get; set; }
        public bool IncludeDependencyGraph { get; set; } = true;
        public bool IncludeCallGraph { get; set; } = true;
        public bool IncludeDomainModel { get; set; } = true;
        public bool IncludeArchitectureView { get; set; } = true;
        public bool UseMermaidForGraphs { get; set; }
        public bool VerboseOutput { get; set; }
        public string? OutputFilePath { get; set; }

        // Noise reduction options
        public bool ExcludeTrivialMethods { get; set; } = true;
        public bool ExcludeEFBoilerplate { get; set; } = true;
        public bool ExcludeBlazorLifecycle { get; set; } = true;
        public bool GroupByFeature { get; set; } = true;

        // Filtering options
        public string? EntryPointFilter { get; set; }
        public string? FeatureFilter { get; set; }
        public int MaxDepth { get; set; } = 3;
    }

}
