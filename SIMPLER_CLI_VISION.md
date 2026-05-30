# Simpler CLI Vision for DevContext

**Goal**: Make the primary experience feel more like "agent context retrieval" but much cheaper.

## Current Problem
- Too many flags: `--depth`, `--focus`, `--feature`, `--mermaid`, `--token-compact`, etc.
- User has to understand the internal model to get good results.
- Not aligned with how developers actually work with LLMs ("I want to work on feature X" or "help me understand this flow").

## Desired Experience (Inspired by Cursor / Claude Code / Agents)

### Simple & Common Cases

```bash
# Just give me good context for this area
devcontext src/Features/Orders

# Task-oriented (the killer feature)
devcontext "I need to add support for Iranian payment gateways" --around src/Features/Payments

devcontext "Help me debug why comments are not showing for guests" --entry src/Features/Comments/CommentService.cs

# High-level architecture review
devcontext . --for architecture
```

### Power User / Repeatable
- `devcontext.json` with named profiles
- `devcontext --profile feature-work --task "add X"`

## Key Ideas for Cheaper + Better Context

1. **Entry Point + Intent Driven**
   - User gives starting file(s) or folder + what they want to do.
   - Tool does bounded, relevant extraction instead of full solution scan + everything.

2. **Smart Defaults + Intent Inference**
   - No intent → good balanced architecture + layers view (cheap).
   - "Add feature" / "implement" language → balanced + feature focus + some implementation depth.
   - "Debug" / "why" language → deeper call graph around the entry points.
   - "Architecture" / "overview" → shallow + strong layer/dependency focus.

3. **Cheaper Modes**
   - Avoid full `MSBuildWorkspace` + semantic models when possible.
   - For many "overview + relevant files" cases, syntax trees + csproj parsing + lightweight static analysis is enough and much faster/cheaper.

4. **Output Optimized for Prompt Attachment**
   - Always include a short "How to use this context" header.
   - Clear sections with token estimates.
   - Profile used is explicit.

## Proposed Simpler Command Surface (v1 target)

Primary command becomes much friendlier:

```bash
devcontext [PATH] [options]
```

Common options (keep the advanced ones but de-emphasize):

- `--for` or `--task` "natural language description of what you're doing"
- `--around` or `--entry` file/folder (the important starting point)
- `--profile` overview | feature | debug | deep (simple named profiles instead of many flags)
- `-o` output file
- `--cheap` / `--fast` (prefer lighter analysis)

Advanced power users can still use full `--depth` / `--focus` or config files.

This reduces cognitive load dramatically while keeping the powerful engine we built underneath.

## Next Steps (Agile)
- Add a simple `--task` / `--intent` parameter that influences depth + focus automatically.
- Improve "relevant context around an entry point" extraction (cheaper + more targeted than full solution).
- Update help and examples to lead with the simple usage.
- Test on real projects like DntSite.
