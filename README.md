# DevContext

A CLI tool that extracts focused context from .NET projects for LLM prompts. Uses Roslyn static analysis to understand call graphs, dependencies, and architecture layers — then prunes output based on your entry point and task description.

## Install

```bash
dotnet tool install --global DevContext.Cli
```

## Usage

```bash
# Point at an entry point (file or folder)
devcontext extract --around src/MyApp/Features/Orders

# With a task description (helps tune depth/focus automatically)
devcontext extract --task "understand checkout flow" --around src/MyApp/Features/Orders

# Architecture overview (cheapest mode)
devcontext extract --depth shallow --focus architecture

# Deep debugging context
devcontext extract --depth deep --focus debug --around src/MyApp/Services
```

When no entry point is given, defaults to a high-level architecture summary.

## Examples

See [`examples/`](./examples) for real outputs against CleanArchitecture and this repo.

## Profiles

| Depth | Focus | Use case |
|---|---|---|
| `shallow` | `architecture` | High-level layer/dependency overview |
| `balanced` | `feature` | Feature work with implementation context |
| `deep` | `debug` | Full call graphs and cross-layer flows |

Intent inference: `--task` text like "add", "implement", "debug", "architecture" auto-selects depth and focus.

## Build

```bash
./build.ps1
dotnet test DevContext.Core.Tests --filter "FullyQualifiedName~Integration"
```

Requires .NET 9 SDK.

## License

MIT
