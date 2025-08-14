using DevContext.Core.Extractors;

namespace DevContext.Core
{
    public interface IProjectDetector
    {
        bool Detect(string targetDir);
        ExtractionResult Extract(string targetDir);
    }
}
