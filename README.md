
<h1 align="center">
  <br>
  DevContext
  <br>
</h1>


<h4 align="center">Cheap, targeted context extraction for .NET developers working with LLMs</h4>


<div align="center">

[![](https://img.shields.io/github/actions/workflow/status/your-github-username/devcontext/build.yml?branch=main)](https://github.com/your-github-username/devcontext/actions?query=branch%3amain)
[![Coveralls branch](https://img.shields.io/coverallsCoverage/github/your-github-username/devcontext?branch=main)](https://coveralls.io/github/your-github-username/devcontext?branch=main)
[![](https://img.shields.io/github/release/your-github-username/devcontext.svg?label=latest%20release&color=007edf)](https://github.com/your-github-username/devcontext/releases/latest)
[![](https://img.shields.io/nuget/dt/devcontext.svg?label=downloads&color=007edf&logo=nuget)](https://www.nuget.org/packages/devcontext)
[![](https://img.shields.io/librariesio/dependents/nuget/devcontext.svg?label=dependent%20libraries)](https://libraries.io/nuget/devcontext)
![GitHub Repo stars](https://img.shields.io/github/stars/your-github-username/devcontext?style=flat)
[![GitHub contributors](https://img.shields.io/github/contributors/your-github-username/devcontext)](https://github.com/your-github-username/devcontext/graphs/contributors)
[![GitHub last commit](https://img.shields.io/github/last-commit/your-github-username/devcontext)](https://github.com/your-github-username/devcontext)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/your-github-username/devcontext)](https://github.com/your-github-username/devcontext/graphs/commit-activity)
[![open issues](https://img.shields.io/github/issues/your-github-username/devcontext)](https://github.com/your-github-username/devcontext/issues)
![Static Badge](https://img.shields.io/badge/4.7%2C_8.0%2C_netstandard2.0%2C_netstandard2.1-dummy?label=dotnet&color=%235027d5)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://makeapullrequest.com)
![](https://img.shields.io/badge/release%20strategy-githubflow-orange.svg)

<a href="#about">About</a> •
<a href="#how-to-use-it">How To Use</a> •
<a href="#examples">Examples</a> •
<a href="#download">Download</a> •
<a href="#building">Building</a> •
<a href="#contributing">Contributing</a> •
<a href="#versioning">Versioning</a> •
<a href="#credits">Credits</a> •
<a href="#license">License</a>

</div>

## About

### What's this?

DevContext is a CLI tool that helps .NET developers quickly extract high-signal, bounded context from their solutions so they can paste it into LLMs (Claude, GPT, Cursor, etc.) for architecture discussions, debugging, or feature development — without sending the entire repo or manually curating files.

It is particularly useful when you have a specific task:
- "Help me understand how this payment flow works"
- "Why is this method throwing a null reference here?"
- "I'm adding a new contributor management feature — give me the relevant pieces"

### Key Differentiator (the "cheap" part)

Unlike full agent-style tools that maintain heavy indexes or send large amounts of context on every interaction, DevContext lets you generate **focused, one-shot context** on demand using `--task` + `--around` (entry point). This is much cheaper and more controllable while still producing context that is genuinely useful when attached to a prompt.

It currently excels at .NET solutions and understands common architectural styles (Clean Architecture, Vertical Slice, Modular, etc.).

### Who created this?
* Something about you, your company, your team, etc.

* How to contact you like LinkedIn, Twitter, Bluesky, Mastodon, email, etc.

## How do I use it?

### The simple, recommended way (task + entry point)

```bash
# Understand an area while working on a feature
devcontext "I need to add contributor contact information" --around src/Clean.Architecture.Core

# Debug a specific flow
devcontext "why are comments not appearing for guests?" --around src/DntSite.Web/Features/Comments

# High-level architecture review
devcontext . --task "architecture review of the user profile system" --around src/DntSite.Web/Features/UserProfiles
```

The `--task` description helps the tool choose smart defaults for depth and focus. `--around` (or `--entry`) strongly scopes what gets extracted, keeping output relevant and relatively cheap.

See the [Usage Scenarios](#usage-scenarios) section below with real examples from actual .NET projects.

Curated real outputs are also available in the [`examples/`](./examples) folder.

For advanced control you can still use `--depth` and `--focus` explicitly, but for most day-to-day work `--task` + `--around` is the recommended approach.

## Usage Scenarios

We have tested DevContext extensively on real projects including:
- [DntSite](https://github.com/VahidN/DntSite) (large feature-sliced production-ish Blazor app)
- [CleanArchitecture](https://github.com/ardalis/CleanArchitecture) reference implementation + samples
- [ContosoUniversity](https://github.com/jbogard/ContosoUniversity) (classic small ASP.NET + EF)

Here is an honest assessment against the scenarios you actually care about:

### Scenario: Understand how a method (or service) works + related code

**Recommended usage:**
```bash
devcontext "Explain how the CurrentUserService works and what it depends on, including authentication" \
  --around "src/DntSite.Web/Features/UserProfiles/Services/CurrentUserService.cs"
```

**What you get today (from real runs):**
- Very tight scoping when you point `--around` at the specific `.cs` file.
- Relevant services, entities, mappers, and configuration in that bounded context.
- Dependency graph showing what touches this area.
- Good enough for an LLM to give a solid explanation of responsibilities and data flow.

**Current gaps (from actual generated outputs):**
- Shallow mode gives excellent file scoping and structure, but limited deep call sequences and invariants.
- Call graph quality is still the weakest area for rich "this calls this under condition X" explanations.

### Scenario: Debug why something is throwing or behaving wrongly

**Recommended usage:**
```bash
devcontext "Debug why a guest might not see comments on a post even though the data exists" \
  --around "src/DntSite.Web/Features/Comments"
```

**What you get today (from real DntSite runs):**
- Strong focus on the relevant feature area.
- The task description helps bias toward more detailed extraction.
- You get the services, data access, and related components needed to investigate.

**Current gaps:**
- Call graph is still too noisy for precise root-cause debugging. Best used as a high-quality "here are the most relevant pieces" starter pack for the LLM.

### Architecture Detection

Current detection (project naming + folder structure + syntax/package signals) is **directionally useful**:

- On DntSite (real feature-sliced app) it correctly surfaces strong "Vertical Slices / Features" dominance.
- On ContosoUniversity (classic small ASP.NET + EF) it picks up feature folders and infrastructure well.
- On CleanArchitecture reference it shows layered relationships in the dependency graph reasonably.

The explicit top-level "Architecture Style" label is still heuristic and can be weak. The practical value today comes more from the scoped layer summary + dependency graph than from the single label. We are actively improving this based on real repo testing.
- The task description helps the tool lean toward more detailed extraction.
- You get the services, data access, and related components needed to investigate.

**Current gaps:**
- Call graph quality is the weakest area for true root cause debugging today.

### Scenario: Develop / implement a new feature Y

**Recommended usage:**
```bash
devcontext "Add support for multiple contact methods (email, phone, social) per contributor" \
  --around "src/Clean.Architecture.Core"
```

**What you get today (this is currently the strongest scenario):**
- Excellent at surfacing existing patterns in the target bounded context.
- Layer summary + dependency graph help you place the new code correctly.
- Relevant entities, services, endpoints, and mappers.

This scenario benefits the most from the `--task` + `--around` approach.

### 1. Understand how a method / service works + related code

**Example command:**
```bash
devcontext "Explain how the CurrentUserService works and what it depends on" --around "src/DntSite.Web/Features/UserProfiles/Services/CurrentUserService.cs"
```

**What the output gives you today:**
- Very tight scoping to the target file and its immediate feature folder (thanks to `--around`).
- Good overview of related services, entities, and configuration in that bounded context.
- Dependency graph showing cross-feature relationships.
- The LLM gets a focused, relevant slice instead of noise.

**Current limitations:**
- In shallow mode you get structure more than deep call sequences.
- For very rich "this calls this which calls this with these rules" explanations, you may still need to run with higher depth or manually include 1-2 more files.

### 2. Debug why something is throwing / behaving incorrectly

**Example command:**
```bash
devcontext "Debug why a user might not see their own comments on a post" --around "src/DntSite.Web/Features/Comments"
```

**What the output gives you today:**
- Excellent focus on the relevant feature area when using `--around`.
- The task description helps bias toward more detailed extraction.
- You get the relevant services, data access, and related components in one place.

**Current limitations:**
- Call graph quality is still the weakest part for deep debugging flows (it can be noisy).
- Best used as a "here are the 80% most relevant pieces" starter pack for the LLM, after which you can iterate by asking follow-up questions.

### 3. Develop / implement a new feature Y

**Example command:**
```bash
devcontext "I want to add support for multiple contact methods per contributor" --around "src/Clean.Architecture.Core"
```

**What the output gives you today (this is currently the strongest use case):**
- Very good at surfacing the existing patterns in the target area (entities, services, endpoints, mappers, etc.).
- Layer summary helps you understand where to put things.
- Dependency graph shows what else touches this area.
- Many developers report this is already useful as a high-quality starting context pack.

**Current limitations:**
- Still benefits from the user knowing roughly which bounded context to point `--around` at.

### Architecture Detection

DevContext attempts to detect common .NET architectural styles (Clean Architecture, Vertical Slice, Modular, etc.) and produces a "Software Layers" section.

On strict reference implementations (like ardalis/CleanArchitecture) it does a reasonable job in the dependency graph and code structure. On real-world mixed codebases the explicit "Architecture Style" label is still heuristic and not always loud/confident. The practical value today comes more from the **layer-aware file grouping** and dependency graph than from the single "Architecture Style: X" line.

We are actively iterating on this based on real repo testing.

## Download

This library is available as [a NuGet package](https://www.nuget.org/packages/devcontext) on https://nuget.org. To install it, use the following command-line:

  `dotnet add package devcontext`


## Building

To build this repository locally, you need the following:
* The [.NET SDKs](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) for .NET 4.7 and 8.0.
* Visual Studio, JetBrains Rider or Visual Studio Code with the C# DevKit

You can also build, run the unit tests and package the code using the following command-line:

`build.ps1`

Or, if you have, the [Nuke tool installed](https://nuke.build/docs/getting-started/installation/):

`nuke`

Also try using `--help` to see all the available options or `--plan` to see what the scripts does.

## Contributing

Your contributions are always welcome! Please have a look at the [contribution guidelines](CONTRIBUTING.md) first.

Previous contributors include:

<a href="https://github.com/your-github-username/devcontext/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=your-github-username/devcontext" alt="contrib.rocks image" />
</a>

(Made with [contrib.rocks](https://contrib.rocks))

## Versioning
This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [tags](/releases) on this repository.

## Credits
This library wouldn't have been possible without the following tools, packages and companies:

* [Nuke](https://nuke.build/) - Smart automation for DevOps teams and CI/CD pipelines by [Matthias Koch](https://github.com/matkoch)
* [xUnit](https://xunit.net/) - Community-focused unit testing tool for .NET by [Brad Wilson](https://github.com/bradwilson)
* [Coverlet](https://github.com/coverlet-coverage/coverlet) - Cross platform code coverage for .NET by [Toni Solarin-Sodara](https://github.com/tonerdo)
* [Polysharp](https://github.com/Sergio0694/PolySharp) - Generated, source-only polyfills for C# language features by [Sergio Pedri](https://github.com/Sergio0694)
* [GitVersion](https://gitversion.net/) - From git log to SemVer in no time
* [ReportGenerator](https://reportgenerator.io/) - Converts coverage reports by [Daniel Palme](https://github.com/danielpalme)
* [StyleCopyAnalyzer](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) - StyleCop rules for .NET
* [Roslynator](https://github.com/dotnet/roslynator) - A set of code analysis tools for C# by [Josef Pihrt](https://github.com/josefpihrt)
* [CSharpCodingGuidelines](https://github.com/bkoelman/CSharpGuidelinesAnalyzer) - Roslyn analyzers by [Bart Koelman](https://github.com/bkoelman) to go with the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
* [Meziantou](https://github.com/meziantou/Meziantou.Framework) - Another set of awesome Roslyn analyzers by [Gérald Barré](https://github.com/meziantou)
* [Verify](https://github.com/VerifyTests/Verify) - Snapshot testing by [Simon Cropp](https://github.com/SimonCropp)

## Support the project
* [Github Sponsors](https://github.com/sponsors/your-github-username)
* [Tip Me](https://paypal.me/your-paypal-username)
* [Buy me a Coffee](https://ko-fi.com/your-github-username)
* [Patreon](https://patreon.com/your-patreon-username)

## You may also like

* Your blog
* Your other projects
* Related projects you think are cool or interesting for the consumers of this project

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
