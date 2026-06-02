# DevContext — Example 02: Architecture Overview of CleanArchitecture

> **Command:** `devcontext extract ".test-repos/CleanArchitecture" --depth balanced --focus feature --around "src/Web/Endpoints/Contributors"`  
> **Date:** 2026-06-02  
> **Profile:** Depth=Balanced, Focus=Feature

This output was generated against the `jasontaylordev/CleanArchitecture` template (the canonical .NET Clean Architecture reference). Notice:

- **13 projects** correctly detected, all on `net10.0` (including the `Directory.Build.props` fallback).
- **Layer breakdown** matches the architecture perfectly: Domain → Application → Infrastructure → Presentation.
- **Dependency graph** enforces Clean Architecture rules: Web depends on Application + Infrastructure, Application depends on Domain, etc.
- At **1,580 characters**, this is cheap enough to attach to any LLM prompt.

---

# DevContext - .NET Project Context
Generated: 2026-06-02 12:33:23
Profile: Depth=Balanced, Focus=Feature

> Paste this into your LLM prompt for focused, task-relevant context.

# Solution Overview
**Root**: C:\Code\DevContext\.test-repos\CleanArchitecture
**Projects**: 13
- **AppHost** — net10.0
- **Application** — net10.0
- **Application.FunctionalTests** — net10.0
- **Application.UnitTests** — net10.0
- **Domain** — net10.0
- **Domain.UnitTests** — net10.0
- **Infrastructure** — net10.0
- **Infrastructure.IntegrationTests** — net10.0
- **ServiceDefaults** — net10.0
- **Shared** — net10.0
- **TestAppHost** — net10.0
- **Web** — net10.0
- **Web.AcceptanceTests** — net10.0

# Software Layers
- **Domain** — 12 files (11%)
- **Application** — 35 files (32%)
- **Infrastructure** — 23 files (21%)
- **Presentation / API** — 8 files (7%)
- **Entry Point / CLI** — 2 files (2%)
- **Core / Shared** — 30 files (27%)

# Dependency Graph
- AppHost → Shared
- AppHost → Web
- Application → Domain
- Application.FunctionalTests → Shared
- Application.FunctionalTests → Web
- Application.FunctionalTests → TestAppHost
- Application.UnitTests → Application
- Application.UnitTests → Infrastructure
- Domain.UnitTests → Domain
- Infrastructure → Application
- Infrastructure → Shared
- TestAppHost → Shared
- Web → Application
- Web → Infrastructure
- Web → ServiceDefaults
- Web.AcceptanceTests → AppHost
- Web.AcceptanceTests → Shared

# Code Structure
_Deep type analysis available with `--depth deep` and a loadable Roslyn workspace._

---

**Total Time**: 0.63s
**Memory Used**: 2MB