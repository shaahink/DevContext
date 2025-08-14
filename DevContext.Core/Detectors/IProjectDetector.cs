using DevContext.Core.Extractors;

namespace DevContext.Core
{
    public interface IProjectDetector
    {
        string Id { get; }
        bool Detect(string directory);
        Task<ExtractionResult> ExtractAsync(string directory);
    }
}
