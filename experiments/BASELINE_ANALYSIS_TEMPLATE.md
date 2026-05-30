# Baseline Analysis — [REPO NAME]

**Date**: YYYY-MM-DD
**DevContext version/commit**: (e.g. v1-polish @ abc1234)
**Test repo**: https://github.com/...
**Command used**:
```powershell
dotnet run --project DevContext.Cli -- extract ... -o ...
```

## Configuration Tested
- Token compact: yes/no
- Parallel: yes/no
- Call graph: yes/no + depth
- Feature grouping: yes/no
- Mermaid: yes/no
- Other flags / config:

## Output Stats
- File size (chars / tokens rough)
- Number of sections
- Time taken
- Any errors/warnings during extraction

## Quality Assessment (against "attach to prompt" goal)

### What Worked Well
- ...

### What Was Noisy / Low Value
- ...

### Layer / Architecture Detection
- What did the tool claim the architecture was?
- Did it correctly identify Domain / Application / Infrastructure / Presentation / Slices?
- Was the grouping useful or misleading?

### Depth & Selectivity
- Did it give a good high-level architecture view when wanted?
- Did it include too much implementation detail in the wrong places?
- Missing important relationships or files?

### Usefulness for Real Tasks (imagine attaching to LLM)
- Architecture discussion / onboarding new dev
- Implementing a new feature in a specific slice/layer
- Debugging a cross-layer issue
- Generating tests or refactoring suggestions

**Score (1-10)**: X/10  
**Would I attach this as-is today?** Yes / No / With heavy editing

## Specific Improvement Ideas Triggered by This Run
1. ...
2. ...

## Next Configuration / Change to Test
- ...

---

## Iteration Log (append as you experiment)

| Run | Flags / Changes | Key Observation | Action |
|-----|-----------------|-----------------|--------|
| 1   | baseline        | ...             | ...    |
| 2   | + --max-depth 2 | ...             | ...    |
