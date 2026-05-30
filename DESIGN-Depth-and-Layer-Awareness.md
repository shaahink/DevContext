# Design: Depth + Smart Layer-Aware Context Extraction

**Date**: 2026-05-30 (started on v1-polish)
**Goal**: Make the generated context **dramatically more useful** when attached to LLM prompts for real work (architecture decisions, feature implementation, debugging).

## Problem Statement (from current baselines)

Current output on this repo (and similar .NET solutions):
- Always dumps at maximum detail.
- Call graphs are noisy with internal infrastructure.
- Feature / layer detection is inaccurate or missing ("MinimalApi" on a CLI tool, 0 features).
- No way for the user to say "I want a high-level architecture view" vs "I am debugging the feature grouping logic in depth".
- No intelligence about software layers (Domain vs Application vs Infrastructure vs Presentation/Slices).

Result: User has to heavily edit the output before it is useful in a prompt.

## Desired Experience (v1 target)

User should be able to do things like:

```bash
# High-level architecture for onboarding / design discussion
devcontext extract . --depth shallow --focus architecture -o arch-overview.md

# Focused on one vertical slice / feature for implementation
devcontext extract . --focus feature --feature Orders -o orders-feature.md

# Deep call graph for debugging a cross-layer issue
devcontext extract . --depth deep --include-call-graph -o debug-deep.md
```

Or via config for repeatable team use.

The output should intelligently:
- Adjust call graph depth and filtering
- Prioritize or de-emphasize certain layers based on requested depth/focus
- Produce a "Layer Summary" section with clear boundaries
- Reduce noise automatically for shallow views

## Proposed Model

### 1. Depth Levels

```csharp
public enum ExtractionDepth
{
    Shallow,      // Architecture, layers, high-level dependencies, key public contracts only. Minimal call graph.
    Balanced,     // Default. Good mix — most common useful case for "understand this system".
    Deep          // Maximum detail. Full call graphs (deeper), more method signatures, less filtering.
}
```

### 2. Focus Modes (Intent)

```csharp
public enum ExtractionFocus
{
    General,           // Current behavior (everything enabled based on depth)
    Architecture,      // Emphasize layers, boundaries, dependency graph, high-level structure. Suppress deep call graphs and implementation details.
    Feature,           // Focus on one or more vertical slices / features. Use feature grouping heavily.
    Implementation,    // Deep on code structure + call graphs inside selected areas.
    Debug              // Similar to Deep but with more call graph + runtime-relevant details.
}
```

### 3. Layer Awareness (the smart part)

We will improve `FeatureDetector` + add a new `LayerDetector` concept.

Recognized layers (extensible):

- `Domain` (entities, value objects, domain events, aggregates)
- `Application` (use cases, commands, queries, handlers, services orchestrating domain)
- `Infrastructure` (persistence, external services, messaging)
- `Presentation` / `Web` / `Api` (controllers, endpoints, minimal APIs, UI)
- `VerticalSlice` / `Feature` (when code is organized by feature rather than layer)
- CrossCutting (logging, validation, common utilities)

For each depth/focus combination, the system decides:

- Which layers to fully expand
- Which layers to summarize (e.g. in Shallow+Architecture: Infrastructure is summarized as "depends on EF Core + Redis", not full listing of every repository impl)
- How deep to go into call graphs within/between layers

### 4. CLI Surface (initial)

Add to `ExtractCommand.Settings`:

```csharp
[CommandOption("--depth")]
public ExtractionDepth Depth { get; set; } = ExtractionDepth.Balanced;

[CommandOption("--focus")]
public ExtractionFocus Focus { get; set; } = ExtractionFocus.General;

[CommandOption("--feature")]
public string[]? Features { get; set; }   // when Focus == Feature
```

These flow into `ExtractionOptions` (new properties).

`init` command wizard will also ask about preferred depth/focus.

### 5. How Extractors Will React (initial rules)

- **Shallow + Architecture**:
  - Call graph: off or depth=1 and heavily filtered to cross-layer only
  - Domain model: high-level entities only
  - Method signatures: public contracts on layer boundaries only
  - Strong "Layer Summary" + Mermaid component diagram

- **Deep + Implementation**:
  - Full call graph (deeper)
  - More internal methods
  - Less aggressive token compression

- **Focus == Feature**:
  - FeatureDetector becomes the primary organizer
  - Other extractors filter their output to the selected feature(s)

This logic lives primarily in `GenericDotNetProjectDetector` (the orchestrator) and the individual extractors.

## Implementation Phases (within v1-polish)

1. **Model + Options** (this spike)
   - Add enums + properties to `ExtractionOptions`
   - Wire new CLI flags
   - Update `LoadConfigurationAsync` + `DisplayConfiguration`

2. **Layer Detection Hardening**
   - Improve existing `FeatureDetector.DetectArchitectureStyleAsync`
   - Add explicit `Layer` classification for files/namespaces/classes
   - Produce a clean "Software Layers" section in output

3. **Orchestrator Changes**
   - `GenericDotNetProjectDetector` starts respecting `Depth` and `Focus`
   - Conditionally enable/disable or reconfigure extractors
   - Pass depth/focus context down

4. **Extractor Adaptations** (incremental)
   - CallGraphExtractor: respect MaxDepth + new "CrossLayerOnly" mode
   - Others: filter based on layer + focus

5. **Output Polish**
   - New top-level sections: "Layer Summary", "Focus Summary"
   - Better ordering and emphasis depending on depth/focus
   - Token estimates in header

## Open Questions / Trade-offs

- How aggressive should "smart omission" be? (Risk of hiding important details)
- Should we support multiple focuses in one run later?
- Performance: Deep mode on large solutions will still be slow — document this.

## Success Criteria for this Feature

- Running with `--depth shallow --focus architecture` on a Clean Architecture or Vertical Slice solution produces output that is *immediately* useful in an LLM prompt for architecture discussion, with clear layer boundaries and minimal noise.
- The same tool on `--depth deep` gives a developer enough signal to reason about a specific implementation area.

---

**Status**: Design started. Initial model + CLI wiring will be implemented on v1-polish in parallel with baseline analysis and structural cleanup.
