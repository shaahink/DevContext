# DevContext — Real Usage Examples

These are actual outputs from the current pipeline on real .NET projects using the `--task` + `--around` pattern.

## 01 — Add Reputation Feature (self-analysis)

**Command:**
```bash
devcontext extract . --task "add a new feature for user reputation points and badges" --around "DevContext.Core"
```

Shows how the tool scopes context when given a clear feature intent: call graph, layer summary, project dependencies.

## 02 — Architecture Overview (CleanArchitecture)

**Command:**
```bash
devcontext extract . --depth shallow --focus architecture --around "src/Clean.Architecture.Web/Contributors"
```

Demonstrates shallow extraction focused on architecture boundaries — ideal for LLM prompts about high-level design.

## How to generate your own

```bash
# Feature-focused extraction
devcontext extract . --task "your task description" --around path/to/relevant/code

# Architecture overview (cheapest, highest-level)
devcontext extract . --depth shallow --focus architecture --around src/YourProject

# Deep debugging context
devcontext extract . --depth deep --focus debug --around src/YourProject
```

The more specific the `--task` + `--around` combination, the more useful the output.
