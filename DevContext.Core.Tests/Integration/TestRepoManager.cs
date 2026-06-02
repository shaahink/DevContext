using System.Diagnostics;

namespace DevContext.Core.Tests.Integration;

public sealed record TestRepoInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string Branch { get; init; } = "main";
    public string? Subdirectory { get; init; }
}

public static class TestRepoManager
{
    private static readonly string RepoDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".test-repos");

    public static async Task<string> EnsureClonedAsync(TestRepoInfo repo, CancellationToken ct = default)
    {
        var targetDir = Path.Combine(RepoDir, repo.Name);

        if (Directory.Exists(targetDir))
            return Path.Combine(targetDir, repo.Subdirectory ?? "");

        Directory.CreateDirectory(RepoDir);

        var psi = new ProcessStartInfo("git")
        {
            Arguments = $"clone --depth 1 --branch {repo.Branch} --single-branch {repo.Url} \"{targetDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git");
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"git clone failed for {repo.Name}: {stderr}");
        }

        return Path.Combine(targetDir, repo.Subdirectory ?? "");
    }
}
