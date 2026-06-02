# DevContext.Cli

A .NET global tool that extracts focused, LLM-ready context from .NET projects.

## Install

```bash
dotnet tool install --global DevContext.Cli
```

## Usage

```bash
# Feature-focused context
devcontext extract --task "add payment support" --around src/Features/Payments

# Architecture overview
devcontext extract --depth shallow --focus architecture

# Debug a specific area
devcontext extract --depth deep --focus debug --around src/Services/PaymentService.cs
```

Output is markdown optimized for attaching to LLM prompts: architecture layers, call graphs, dependency graphs, and project structure — pruned to your entry point and task.

## Requirements

- .NET 9 SDK

## Build from source

```bash
git clone https://github.com/shaahink/DevContext
cd DevContext
./build.ps1
```

## License

MIT
