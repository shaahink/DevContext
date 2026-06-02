# Cleanup Report

Files identified as redundant, vestigial, or dead as of 2026-06-02.

## Safe to delete immediately (no code changes)

| File | Reason |
|---|---|
| `DevContext.Core.Tests/UnitTest1.cs` | Empty `[Fact]` with no assertions |
| `DevContext.Core.Tests/DotNetCoreApiDetectorTests.cs` | Empty class; `DotNetCoreApiDetector` no longer exists |
| `1.md` | Stale generated output from Aug 2025 |
| `llm.ps1` | Old prototype script, unrelated to DevContext |
| `context.txt` | Unknown stale artifact |
| `experiments/` | Entire folder (30+ output files, assessments, configs, cloned repos, setup script) |

## Dead extractors (replaced by new pipeline)

These 5 files in `DevContext.Core/Extractors/` are unreferenced — replaced by `IAnalyzer` implementations in `DevContext.Core/Analysis/`:

| File | Replacement |
|---|---|
| `CallGraphExtractor.cs` | `CallGraphAnalyzer` |
| `CodeStructureExtractor.cs` | `LayerAnalyzer` + `MarkdownRenderer` |
| `DependencyGraphExtractor.cs` | `DependencyAnalyzer` |
| `DomainModelExtractor.cs` | (no direct replacement yet) |
| `SolutionOverviewExtractor.cs` | `SolutionAnalyzer` |

**Keep: `FeatureDetector.cs`** — still used by CLI `DetectCommand` and `GenericDotNetProjectDetector.TryDetectArchitectureAsync`.

## Dead compression engine

`DevContext.Core/Detectors/TokenCompressor.cs` (837 lines) — unreferenced. The new pipeline uses relevance-based pruning instead. Requires:
- Delete the file
- Remove `TokenCompact` from `ExtractionOptions`
- Remove `--token-compact` CLI flag from `Program.cs`

## Vestigial projects

1. **`DevContext/`** — Multi-target library with zero source files, placeholder NuGet metadata, not referenced by CLI. Requires removing the project + `DevContext.Specs/` from the solution.
2. **`DevContext.Specs/`** — Single tautological test (`1.Should().Be(1)`). Can delete with the vestigial library or retarget to `DevContext.Core`.

## After cleanup, the source tree becomes:

```
DevContext.Core/          -- core engine (analysis pipeline, models, scoring, rendering)
DevContext.Cli/           -- CLI tool (3 commands: extract, init, detect)
DevContext.Core.Tests/    -- integration tests (18 tests against real repos)
DevContext.ApiVerificationTests/ -- API approval tests
Build/                    -- NUKE build infrastructure
```
