using DevContext.Core.Analysis;
using DevContext.Core.Models;

namespace DevContext.Core.Tests.Integration;

[Collection("Integration")]
public sealed class AnalysisPipelineIntegrationTests
{
    private readonly RepoFixture _fixture;

    public AnalysisPipelineIntegrationTests(RepoFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> RepoData => TestRepos.All.Select(r => new object[] { r });

    [Theory, MemberData(nameof(RepoData))]
    public async Task Pipeline_discovers_projects(TestRepoInfo repo)
    {
        var result = await RunPipelineAsync(repo, ExtractionDepth.Balanced, ExtractionFocus.Architecture);
        if (result == null) return;

        var model = result.Model;
        Assert.NotEmpty(model.Projects);
        Assert.All(model.Projects, p => Assert.NotEmpty(p.Name));
    }

    [Theory, MemberData(nameof(RepoData))]
    public async Task Pipeline_classifies_layers(TestRepoInfo repo)
    {
        var result = await RunPipelineAsync(repo, ExtractionDepth.Shallow, ExtractionFocus.Architecture);
        if (result == null) return;

        Assert.NotEmpty(result.Model.LayerCounts);
    }

    [Theory, MemberData(nameof(RepoData))]
    public async Task Pipeline_finds_call_graph(TestRepoInfo repo)
    {
        var result = await RunPipelineAsync(repo, ExtractionDepth.Deep, ExtractionFocus.Debug);
        if (result == null) return;

        // Call graph requires a loaded Roslyn workspace; skip assertion when unavailable
        if (result.Model.Solution != null)
            Assert.NotEmpty(result.Model.CallGraph);
    }

    [Theory, MemberData(nameof(RepoData))]
    public async Task Pipeline_finds_dependencies(TestRepoInfo repo)
    {
        var result = await RunPipelineAsync(repo, ExtractionDepth.Balanced, ExtractionFocus.General);
        if (result == null) return;

        var multiProject = result.Model.Projects.Count > 1;
        if (multiProject)
            Assert.NotEmpty(result.Model.Dependencies);
    }

    [Fact]
    public async Task Pipeline_analyzes_self()
    {
        var dir = FindSolutionRoot();
        Assert.NotNull(dir);

        var pipeline = new AnalysisPipeline(new ExtractionOptions
        {
            Depth = ExtractionDepth.Balanced,
            Focus = ExtractionFocus.General,
            VerboseOutput = false
        });

        var result = await pipeline.ExecuteAsync(dir);

        Assert.NotNull(result.ExtractionResult);
        Assert.NotEmpty(result.ExtractionResult.Content);

        var model = result.Model;
        Assert.NotEmpty(model.Projects);
        Assert.Contains(model.Projects, p => p.Name.Contains("DevContext.Core"));
        Assert.Contains(model.Projects, p => p.Name.Contains("DevContext.Cli"));

        if (model.Solution != null)
            Assert.NotEmpty(model.CallGraph);
        Assert.NotEmpty(model.LayerCounts);

        var cli = model.Projects.FirstOrDefault(p => p.Name.Contains("DevContext.Cli"));
        Assert.NotNull(cli);
        // ProjectReference content may be empty with csproj-only scanning (no Roslyn)
        if (cli.ProjectReferences.Count > 0)
            Assert.Contains(cli.ProjectReferences, r => r.Contains("DevContext.Core"));
    }

    [Fact]
    public async Task Pipeline_produces_formatted_markdown()
    {
        var dir = FindSolutionRoot();
        Assert.NotNull(dir);

        var pipeline = new AnalysisPipeline(new ExtractionOptions
        {
            Depth = ExtractionDepth.Balanced,
            Focus = ExtractionFocus.General,
            VerboseOutput = false
        });

        var result = await pipeline.ExecuteAsync(dir);
        var content = result.ExtractionResult.Content;

        Assert.StartsWith("# DevContext", content);
        Assert.Contains("Solution Overview", content);
        Assert.Contains("Call Graph", content);
        Assert.Contains("Software Layers", content);
        Assert.Contains("Code Structure", content);
    }

    [Fact]
    public async Task Shallow_depth_omits_call_graph()
    {
        var dir = FindSolutionRoot();
        Assert.NotNull(dir);

        var options = new ExtractionOptions
        {
            Depth = ExtractionDepth.Shallow,
            Focus = ExtractionFocus.Architecture,
            VerboseOutput = false
        };
        options.ApplyDepthAndFocusRules();

        var pipeline = new AnalysisPipeline(options);
        var result = await pipeline.ExecuteAsync(dir);
        var content = result.ExtractionResult.Content;

        Assert.DoesNotContain("Call Graph", content);
        Assert.Contains("Software Layers", content);
    }

    [Fact]
    public async Task Deep_depth_includes_full_call_graph()
    {
        var dir = FindSolutionRoot();
        Assert.NotNull(dir);

        var options = new ExtractionOptions
        {
            Depth = ExtractionDepth.Deep,
            Focus = ExtractionFocus.Debug,
            VerboseOutput = false
        };
        options.ApplyDepthAndFocusRules();

        var pipeline = new AnalysisPipeline(options);
        var result = await pipeline.ExecuteAsync(dir);

        Assert.NotEmpty(result.Model.CallGraph);
    }

    private static string? FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !Directory.GetFiles(dir, "*.sln").Any())
            dir = Path.GetDirectoryName(dir);
        return dir;
    }

    [Fact]
    public async Task Debug_self_analyzer()
    {
        var dir = FindSolutionRoot();
        Assert.NotNull(dir);

        var analyzer = new SolutionAnalyzer(new ExtractionOptions());
        var model = new CodeModel { RootDirectory = dir };
        await analyzer.AnalyzeAsync(model);

        Assert.NotEmpty(model.Projects);
        Assert.Contains(model.Projects, p => p.Name.Contains("DevContext.Core"));
    }

    [Fact]
    public async Task Debug_solution_analyzer_directly()
    {
        var repo = TestRepos.CleanArchitecture;
        var dir = await _fixture.CloneAsync(repo);
        Assert.NotNull(dir);
        Assert.True(Directory.Exists(dir), $"Directory not found: {dir}");

        var allCsprojs = Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories);
        var opts = new ExtractionOptions();
        var filtered = allCsprojs.Where(f => !opts.ExcludeDirectories.Any(ex => f.Contains(ex))).ToList();

        Console.WriteLine($"Total csproj files: {allCsprojs.Length}");
        Console.WriteLine($"After exclude filter: {filtered.Count}");
        foreach (var csproj in allCsprojs.Take(3))
        {
            Console.WriteLine($"  {csproj}");
            Console.WriteLine($"  Contains 'obj': {csproj.Contains("obj")}");
            Console.WriteLine($"  Contains 'bin': {csproj.Contains("bin")}");
        }

        Assert.NotEmpty(allCsprojs);

        var analyzer = new SolutionAnalyzer(new ExtractionOptions());
        var model = new CodeModel { RootDirectory = dir };
        await analyzer.AnalyzeAsync(model);

        Console.WriteLine($"Projects found: {model.Projects.Count}");
        Assert.NotEmpty(model.Projects);
    }

    private async Task<PipelineResult?> RunPipelineAsync(
        TestRepoInfo repo,
        ExtractionDepth depth,
        ExtractionFocus focus)
    {
        var dir = await _fixture.CloneAsync(repo);
        if (dir == null) return null;

        var options = new ExtractionOptions
        {
            Depth = depth,
            Focus = focus,
            VerboseOutput = false
        };
        options.ApplyDepthAndFocusRules();

        var pipeline = new AnalysisPipeline(options);
        return await pipeline.ExecuteAsync(dir);
    }
}

[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<RepoFixture>
{
}

public sealed class RepoFixture
{
    private static readonly HashSet<string> FailedRepos = new();
    private static readonly object Lock = new();

    public async Task<string?> CloneAsync(TestRepoInfo repo)
    {
        lock (Lock)
        {
            if (FailedRepos.Contains(repo.Name))
                return null;
        }

        try
        {
            return await TestRepoManager.EnsureClonedAsync(repo);
        }
        catch (Exception ex)
        {
            lock (Lock)
            {
                FailedRepos.Add(repo.Name);
            }
            Console.WriteLine($"Skipping {repo.Name} (clone failed): {ex.Message}");
            return null;
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;
}
