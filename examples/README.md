# Examples

Real outputs from DevContext on real .NET projects. Each example was generated with a specific command — the output is included verbatim.

## 01 — Feature-focused extraction (self-analysis)

```bash
devcontext extract . --task "add a new feature for user reputation points and badges" --around "DevContext.Core"
```

Shows how the tool scopes context around a specific project with call graph, layer summary, and dependency graph.

## 02 — Architecture overview (CleanArchitecture)

```bash
devcontext extract ".test-repos/CleanArchitecture" --depth balanced --focus feature --around "src/Web/Endpoints/Contributors"
```

13-project Clean Architecture template with correct layer breakdown and dependency enforcement.

## Generating your own

```bash
devcontext extract . --task "your task" --around path/to/code
devcontext extract . --depth shallow --focus architecture
devcontext extract . --depth deep --focus debug --around src/Target
```
