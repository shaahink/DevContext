# DevContext

A CLI tool that extracts relevant context from .NET projects for use with LLMs.

## Install

```bash
dotnet tool install --global DevContext.Cli
```

## How it works

Point it at an entry point (a file or folder). It extracts the code that is structurally connected to it — mainly through the call graph and related types.

By default it tries to stay focused on what is directly relevant from your entry point.

## Basic usage

```bash
# Point at an entry point (file or folder)
devcontext --around src/MyApp/Features/Orders

# With optional description of what you're doing (helps tuning)
devcontext --around src/MyApp/Features/Orders --for "understand checkout flow"
```

When no specific entry point is given, it defaults to a high-level architecture / structure summary of the solution.

## Examples

See the [`examples/`](./examples) folder for real outputs.

## Building

```bash
./build.ps1
```

Requires the .NET 8 or 9 SDK.

## License

MIT