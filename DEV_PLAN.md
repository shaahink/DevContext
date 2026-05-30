# DevContext v1 Polish & Ship Plan

**Goal**: Turn the current half-baked state into a credible, CV-worthy, actually-usable CLI tool for extracting high-signal architectural and code context from .NET solutions (optimized for pasting into LLMs).

**Base branch**: `better-cli` (currently the best foundation)
**Working branch**: `v1-polish` (created 2026-05-30 from better-cli)

**Status**: User approved direction — polish the existing approach, focus heavily on output quality for real LLM usage (attach to prompts for better deliverables, debugging hints, architecture understanding). Priority on **depth control** and **smart software layer detection** with selective inclusion.

**Realistic timeline for something you can be proud of on a CV**: 7–14 focused days (can be compressed or stretched).

---

## Current User Priorities (as of start of v1-polish)

- Generated context must be **good enough to attach directly to a prompt** and produce noticeably better results (architecture discussions, feature implementation, debugging hints, onboarding).
- **Depth control** — user should be able to ask for shallow (architecture overview) vs deep (implementation details for a specific area).
- **Smart software layer detection** — reliably identify layers (Domain, Application, Infrastructure, Presentation/Web, Vertical Slices, etc.) and **decide intelligently what to include or summarize** based on the requested depth or focus.
- Clean up noise in code and output.
- Professional GitHub presence: excellent README + CI/CD.

This shifts emphasis in Phase 2 toward "Layer-Aware + Intent-Aware Extraction" rather than just dumping more data.

---

## Honest Current State (as of late May 2026)

### What's Actually Working
- CLI (`devcontext extract`, `init`, `detect`) using Spectre.Console — nice UX, progress bars, config loading.
- Core extraction pipeline runs and produces output (tested on this repo itself).
- Sophisticated `TokenCompressor` (15+ compression strategies) — this is legitimately one of the best parts.
- Multiple extractors: Solution overview, dependency graph (Mermaid support), Roslyn call graph, feature/architecture detection, domain model, code structure + signatures.
- Parallel execution + progress reporting.
- Builds cleanly (0 errors).
- Packaged as a `dotnet` global tool.

### What's Broken / Half-Baked / Embarrassing for a CV
- **Structural rot**: `IProjectDetector.cs` is completely empty. The interface lives at the bottom of `ExtractionOptions.cs`. Namespaces are inconsistent (`DevContext.Core` vs `DevContext.Core.Extractors`).
- All real logic is crammed into two giant files (`GenericDotNetExtractor.cs` ~771 lines, `TokenCompressor.cs` ~830 lines).
- Feature detection is unreliable (called this solution "MinimalApi").
- Call graph output is noisy and low-signal.
- Almost zero meaningful tests (empty test classes, placeholder specs).
- READMEs and PackageReadme are 100% Nuke template garbage.
- Old vestigial `DevContext` library project (multi-target netstandard + net47) that the CLI doesn't use.
- Hard-coded defaults, some rough edges, and "works on my machine" smells.
- No compelling public demo or before/after examples.

**Verdict**: The *idea* and some of the implementation are better than average for this category of tool. The current presentation and structure are not CV-ready.

---

## Recommended v1 Scope (Pragmatic + High Signal)

**In scope for v1**
- Excellent .NET experience (Roslyn-powered where it adds value).
- Clean, maintainable architecture (easy for future you or a reviewer to understand).
- Output that is *actually useful* when pasted into Claude/GPT for architecture discussion or onboarding.
- Proper documentation + one strong real-world example.
- `dotnet tool install` works cleanly.
- A few meaningful tests + API surface under control.
- You can talk about the interesting technical choices (compression strategies, architecture-aware grouping, Roslyn call graph trade-offs).

**Out of scope for v1 (deliberately)**
- First-class multi-language support (tree-sitter, etc.).
- Web UI, VSCode extension, or fancy hosting.
- Perfect call graphs on every codebase (impossible without huge effort).
- Supporting every weird .NET project layout ever invented.

You can still add a minimal "generic mode" (file tree + key file sampling + package.json/csproj scanning) so the tool doesn't completely die on non-.NET repos. This is cheap and looks thoughtful.

---

## Phased Implementation Plan

### Phase 0: Branch Hygiene & Baseline (½ day) — IN PROGRESS
- [x] Create `v1-polish` from `better-cli` (2026-05-30)
- [ ] Delete or clearly mark `failed-claude-attempt` and `z-ai` (they are pure template noise)
- [ ] Decide fate of `refactor-to-better-cli` and `adding-generic-dotnet-extractor` (recommend: keep for history, or delete after merging any unique good ideas)
- [ ] Set up `experiments/` folder + gitignore for safe iteration on real repos
- [ ] Choose primary experimentation repository (strong recommendation: ardalis/CleanArchitecture for explicit layers)
- [ ] Generate baseline outputs with current tool on chosen repo + this repo under different configurations (depth, focus, etc.)
- [ ] Analyze outputs against the "attach to prompt" quality bar
- [ ] Create `DEV_PLAN.md` (this file) and a `CHANGELOG.md` skeleton

**Exit criteria**: Clean working branch + first set of experiment outputs + clear understanding of current output quality gaps vs the goal.

### Phase 1: Structural Cleanup & Architecture (2–3 days) — Highest CV impact per hour
This phase alone will make the codebase look professional.

- [ ] Fix the interface mess
  - Restore a proper `IProjectDetector.cs` in the right namespace
  - Decide on one namespace strategy (`DevContext.Core` or `DevContext.Core.Extractors`) and enforce it
- [ ] Split the monolith files
  - Move each extractor into its own file under `Extractors/` (SolutionOverviewExtractor, CallGraphExtractor, etc.)
  - Keep `GenericDotNetProjectDetector` as the orchestrator/composition root
  - Move `TokenCompressor` + `CompressionOptions` into their own folder
- [ ] Clean up `ExtractionOptions.cs`
  - Move supporting types (`FeatureGrouping`, `FeatureInfo`, etc.) to better homes
- [ ] Remove or quarantine the old `DevContext` multi-target library project (or clearly document why it exists)
- [ ] Add a simple `Directory.Build.props` or `.editorconfig` rule to prevent future giant files
- [ ] Update all `using` statements and fix the empty `IProjectDetector.cs`

**Nice-to-have in this phase**
- Introduce a small `IContextExtractor` interface so new extractors are easy to add (good for future extensibility story on CV).

**Exit criteria**: Solution builds, all tests (even the weak ones) pass, a new developer can understand the structure in <10 minutes by reading the folder layout.

### Phase 2: Output Quality & Signal (3–5 days) — This is what users will actually judge
This is the difference between "another repo-to-markdown tool" and something interesting.

- [ ] Call Graph improvements (biggest win)
  - Add better filtering (exclude trivial methods more aggressively, respect `MaxCallGraphDepth`)
  - Group calls by feature/namespace when `EnableFeatureGrouping` is on
  - Add a "top hot paths" or "most called internal methods" summary section
  - Optional: limit to public + internal methods only by default
- [ ] Feature / Architecture detection hardening
  - Make the detector more accurate on real codebases (test against several public repos)
  - Add fallback to namespace/folder heuristics when Roslyn indicators are weak
  - Better "Architecture Style" output with confidence or "detected via X" notes
- [ ] Output formatting for LLM consumption
  - Add a header with token estimate (rough) and "recommended use" guidance
  - Prioritize sections (put high-value stuff early)
  - Consistent heading levels and formatting
  - Consider a `--format compact|detailed|llm` flag
- [ ] Domain Model and Code Structure extractors
  - Reduce noise (fewer DTO spam, better entity detection)
  - Limit depth on large solutions

**Stretch (if time)**
- Add a very simple generic fallback mode (`--mode generic`) that works without Roslyn (just walks files + reads csproj/package.json for basic info).

**Exit criteria**: Running the tool on a real solution produces output you would actually paste into an LLM without heavy editing.

### Phase 3: Packaging, Documentation & Demo (2–3 days)
This is what turns "works on my machine" into "shipped project".

- [ ] README.md (real content)
  - Problem statement + why this exists
  - Clear "Quick Start" (install as tool + one command)
  - 2–3 realistic examples with sample output (small + medium repo)
  - Architecture diagram (even a simple Mermaid one)
  - "How it works" section (high level) — great for interviews
- [ ] PackageReadme.md — keep in sync, shorter version for NuGet
- [ ] Excellent `--help` and command examples (Spectre already makes this easy)
- [ ] One high-quality demo
  - Pick a well-known public .NET repo (eShopOnContainers, CleanArchitecture by ardalis, or a real production-ish open source project)
  - Include the generated context file + a short "what I learned from it" write-up
  - Optional: short Loom/YouTube clip (2–3 min) showing the tool in action
- [ ] Fix packaging friction
  - Make sure `dotnet tool install -g DevContext.Cli` works from a release
  - Versioning via GitVersion is already there — make sure it's sensible
- [ ] Update `GitVersion.yml` if needed + ensure clean tags

**CV artifacts from this phase**
- Public GitHub repo that looks maintained
- A real example people can try themselves
- Something you can link in a resume/LinkedIn with "generated architectural context for X codebase"

**Exit criteria**: A stranger can install the tool and get useful output in < 3 minutes.

### Phase 4: Testing, Hardening & Professional Touches (2–3 days)
- [ ] Add real tests for the valuable parts
  - Unit tests for `TokenCompressor` (easy wins, high coverage, shows you care about quality)
  - A couple of integration-style tests for the main extractors using small fixture solutions
  - Update `DevContext.Core.Tests` properly (currently almost empty)
- [ ] API verification tests — decide if they are still valuable or delete the noise
- [ ] Specs project — either delete or turn into proper BDD-style tests
- [ ] Add basic error handling / friendly messages for common failure modes (no solution file, MSBuild locator fails, etc.)
- [ ] Security / supply chain basics
  - Note the known vulnerable transitive packages (or pin/upgrade where possible)
  - Add Dependabot config if missing
- [ ] CONTRIBUTING.md — minimal but real (how to build, how to add an extractor)
- [ ] License check (MIT is already there — good)

**Optional but nice for CV**
- One GitHub Action that builds + runs the tool against a test repo on every PR
- Code coverage badge (even if not 80%+)

**Exit criteria**: You are not embarrassed when someone clones the repo and runs `dotnet test`.

### Phase 5: Release & CV Packaging (1 day)
- [ ] Git tag + GitHub release with good notes
- [ ] Push to NuGet (or at least document how)
- [ ] Update your personal site / LinkedIn / resume with a short blurb + link
- [ ] (Optional) Write a short blog post or LinkedIn thread: "I built a tool to stop pasting entire repos into Claude"
- [ ] Record the 2–3 minute demo if you didn't in Phase 3

---

## Key Technical Decisions to Make Early

1. **Stay heavy on Roslyn or lighten up?**
   - Current: Full MSBuild workspace + compilation for call graphs and deep feature detection.
   - Trade-off: Higher quality on .NET vs slower + more fragile.
   - Recommendation for v1: Keep it, but make the call graph optional (`--include-call-graph`) and off by default for speed.

2. **Namespace and public API strategy**
   - The library (`DevContext.Core`) is currently not really published as a reusable library.
   - Decision: Either commit to making the core a proper library with a clean public API, or treat the CLI as the product and keep Core internal-ish.

3. **Token budget / size control**
   - Add explicit `--max-tokens 8000` style control with a good default.
   - This is very valuable for LLM use cases.

4. **Future multi-language path**
   - Document (in an `ARCHITECTURE.md` or `FUTURE.md`) how you *would* add other languages later (tree-sitter + language-specific heuristics) without committing to doing it in v1.

---

## Risks & Things That Can Go Wrong

- Roslyn workspace loading is fragile on some corporate or very large solutions → have good error messages and a `--no-roslyn` fallback plan.
- Scope creep ("let's just add Python support real quick").
- Perfectionism on the call graph (it will never be perfect).
- Spending too much time on the old vestigial library project.

---

## Success Criteria (How we'll know v1 is "done")

- [ ] A person who has never seen the code can run `dotnet tool install -g ...` and get useful context from a real .NET solution in one command.
- [ ] The generated output is something *you* would use in a real conversation with an LLM about architecture.
- [ ] The codebase looks like it was written by someone who cares (no 800-line files, clear separation, decent tests, good docs).
- [ ] You can talk for 5–10 minutes about interesting technical choices you made (compression, feature detection heuristics, Roslyn trade-offs).
- [ ] You have at least one public artifact (repo + example output + maybe a short write-up) you are happy to link from a resume or LinkedIn.

---

## Next Steps (for you)

1. Reply with which phases you want to tackle and in what order.
2. Answer the four quick questions from my previous message if you haven't yet:
   - Primary goal (CV polish vs real tool)?
   - .NET-first or multi-lang ambition for v1?
   - Realistic calendar time?
   - Output quality bar?
3. Tell me if you want me to:
   - Start executing Phase 1 immediately on a new branch
   - First create more detailed task breakdowns or spike certain areas
   - Explore a cleaner architecture prototype first

---

*This plan was generated after full exploration of all branches, building every active line of development, running the current CLI, and reviewing the actual generated output quality.*

**You are in a much better position than "start from scratch."** The foundation on `better-cli` is real. It just needs structure, signal quality, and presentation work.

Let's make this something you're genuinely proud to show.