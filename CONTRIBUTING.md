# Contributing

## Reporting issues

Search existing issues before filing a new one. Include:
- .NET SDK version, OS
- Steps to reproduce
- Expected vs actual behavior

## Pull requests

- Target the `main` branch.
- Run integration tests: `dotnet test DevContext.Core.Tests --filter "FullyQualifiedName~Integration"`
- If changing public API, update the approval test snapshots via `AcceptApiChanges.ps1`.
- Keep changes focused. Open an issue first for large changes.

## Build

```bash
dotnet build
./build.ps1       # NUKE full pipeline (pack, test, api checks)
```
