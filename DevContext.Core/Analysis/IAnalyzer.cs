using DevContext.Core.Models;

namespace DevContext.Core.Analysis;

public interface IAnalyzer
{
    string Name { get; }
    Task AnalyzeAsync(CodeModel model, CancellationToken ct = default);
}
