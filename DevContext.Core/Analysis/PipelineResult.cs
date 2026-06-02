using DevContext.Core.Models;

namespace DevContext.Core.Analysis;

public sealed class PipelineResult
{
    public required CodeModel Model { get; init; }
    public required ExtractionResult ExtractionResult { get; init; }
}
