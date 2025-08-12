namespace DevContext.Core;

public interface IProjectDetector
{
    string Id { get; }
    bool Detect(string directory);
    ExtractionResult Extract(string directory);
}



public sealed record ExtractionResult(string ProjectType, string Content);
