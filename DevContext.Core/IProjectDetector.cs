using System;
using System.Threading.Tasks;

namespace DevContext.Core
{
    /// <summary>
    /// Abstraction for project context extractors.
    /// Allows future support for different project types (e.g. non-.NET) via the same interface.
    /// </summary>
    public interface IProjectDetector
    {
        string Id { get; }
        bool Detect(string targetDir);
        Task<ExtractionResult> ExtractAsync(string targetDir, IProgress<ExtractionProgress>? progress = null);
    }

    /// <summary>
    /// Progress information reported during long-running extraction.
    /// </summary>
    public class ExtractionProgress
    {
        public string CurrentTask { get; set; } = "";
        public string CurrentDetail { get; set; } = "";
        public double PercentComplete { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public int CompletedTasks;
        public int TotalTasks { get; set; }
    }
}
