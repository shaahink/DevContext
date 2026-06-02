# Example 01: Feature-focused extraction

**Command:** `devcontext extract . --task "add a new feature for user reputation points and badges" --around "DevContext.Core"`

Extraction against the DevContext repo itself. Output includes 7 projects, layer breakdown, dependency graph, and call graph grouped by namespace.

---

# DevContext - .NET Project Context
Generated: 2026-06-02 12:31:25
Profile: Depth=Balanced, Focus=Feature

> Paste this into your LLM prompt for focused, task-relevant context.

# Solution Overview
**Root**: C:\Code\DevContext
**Projects**: 7
- **_build** — net8.0
- **DevContext** — netstandard2.0
- **DevContext.ApiVerificationTests** — net8.0
- **DevContext.Cli** — net9.0 (CLI tool)
- **DevContext.Core** — net9.0
- **DevContext.Core.Tests** — net9.0
- **DevContext.Specs** — net6.0

# Software Layers
- **Entry Point / CLI** — 1 files (3%)
- **Build / Tests** — 5 files (16%)
- **Core / Shared** — 26 files (81%)

# Dependency Graph
- DevContext.Cli → DevContext.Core
- DevContext.Core.Tests → DevContext.Core
- DevContext.Specs → DevContext

# Call Graph
## Cli
- ExtractCommand.ExecuteAsync → DisplayResultsSummary, GenericDotNetProjectDetector.ExtractAsync, GenericDotNetProjectDetector.Detect, DisplayConfiguration, LoadConfigurationAsync
- DetectCommand.ExecuteAsync → GenerateRecommendations, FeatureDetector.DetectFeaturesAsync

## Core
- AnalysisPipeline.ExecuteAsync → MarkdownRenderer.RenderAsync, RelevanceFilter.ApplyBudget, RelevanceScorer.Score
- CallGraphAnalyzer.AnalyzeAsync → ExtractFeatureGroup, IsInSolution, ShouldExclude
- LayerAnalyzer.AnalyzeAsync → ClassifyLayer
- SolutionAnalyzer.AnalyzeAsync → TryLoadDirectoryBuildProps
- GenericDotNetProjectDetector.ExtractAsync → AnalysisPipeline.ExecuteAsync, ApplyDepthAndFocusRules, TryDetectArchitectureAsync
- MarkdownRenderer.RenderAsync → RenderProjects, RenderCallGraph, RenderDependencies, RenderLayers, RenderSolutionOverview, RenderHeader

## General
- ChainablePath methods (Pathy interop)

# Code Structure

---

**Total Time**: 21.64s
**Memory Used**: 53MB
