namespace DevContext.Core
{
    public class ExtractionOptions
    {
        public bool IncludeMethodSignatures { get; set; } = true;
        public bool IncludeDependencyGraph { get; set; } = true;
        public bool IncludeCallGraph { get; set; } = true;
        public bool IncludeDomainModel { get; set; } = false;
        public bool UseMermaidForGraphs { get; set; } = false;
        public bool TokenCompact { get; set; } = true; // Default ON for LLM usage
        public string? OutputFilePath { get; set; } = null;
        public bool VerboseOutput { get; set; } = true;
    }
}
