# DevContext Experiments

This folder is for **safe, iterative experimentation** on real-world repositories while improving DevContext.

## Purpose

- Generate baseline context outputs using the current version of the tool
- Compare outputs before/after changes (especially around depth control and layer detection)
- Test output quality against the real goal: *"Can a user attach this file to a prompt and get meaningfully better results for architecture work, feature implementation, or debugging?"*
- Keep large cloned repos and generated artifacts out of the main git history

## Recommended Primary Test Repository

**ardalis/CleanArchitecture** (https://github.com/ardalis/CleanArchitecture)

**Why this one first?**
- Very explicit, clean software layers: `Domain`, `Application`, `Infrastructure`, `Web`
- Small enough to iterate quickly, large enough to be realistic
- Canonical example of the architecture styles our `FeatureDetector` claims to support
- Perfect for developing and validating **smart layer detection + selective inclusion** based on depth/focus

Secondary candidates (once primary is solid):
- Vertical Slice architecture examples
- eShopOnContainers (for scale and microservices complexity)
- Your own real internal solutions (when comfortable)

## How to Run Experiments

1. Clone the target repo into this folder (or a subfolder):
   ```powershell
   git clone https://github.com/ardalis/CleanArchitecture.git CleanArchitecture
   ```

2. Generate context using the local dev build:
   ```powershell
   # From repo root
   dotnet run --project DevContext.Cli -- extract experiments/CleanArchitecture `
       -o experiments/outputs/cleanarch-baseline.md `
       --token-compact `
       --parallel
   ```

3. Try different configurations and record them:
   - Different depth settings
   - With/without call graph
   - Focus on architecture vs implementation
   - etc.

4. Document findings in `BASELINE_ANALYSIS.md` (use the template).

## What "Good Enough" Output Looks Like

When attached to an LLM prompt, the context should help the model produce:
- Better architectural recommendations
- More accurate debugging hints
- Correctly scoped feature implementations
- Good questions back to the user about the system

Current weaknesses we're actively improving:
- Noisy call graphs
- Weak/ inaccurate layer detection
- No real depth or focus control
- Too much low-value detail or not enough when needed

## Output Naming Convention (suggested)

- `repo-name-baseline.md` — current tool with default settings
- `repo-name-v1-depth-shallow.md`
- `repo-name-v1-layers-only.md`
- `repo-name-v1-feature-focus-Orders.md`
- etc.

Keep the raw outputs + your analysis notes. This becomes gold for the final README examples and your CV story ("I tested and iterated the output quality against real Clean Architecture codebases...").

## Safety Notes

- Never commit the cloned source repos or huge raw outputs
- The `.gitignore` already protects `experiments/`
- Use `--exclude` aggressively when testing on sensitive internal codebases

Let's make the output quality the standout feature of v1.