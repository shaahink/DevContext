namespace DevContext.Core.Tests.Integration;

public static class TestRepos
{
    public static readonly TestRepoInfo CleanArchitecture = new()
    {
        Name = "CleanArchitecture",
        Url = "https://github.com/jasontaylordev/CleanArchitecture.git",
        Branch = "main"
    };

    public static readonly TestRepoInfo FastEndpoints = new()
    {
        Name = "FastEndpoints",
        Url = "https://github.com/FastEndpoints/FastEndpoints.git",
        Branch = "main"
    };

    public static readonly TestRepoInfo EShop = new()
    {
        Name = "eShop",
        Url = "https://github.com/dotnet/eshop.git",
        Branch = "main"
    };

    public static readonly TestRepoInfo RestApiTemplate = new()
    {
        Name = "RestApiTemplate",
        Url = "https://github.com/neozhu/CleanArchitectureWithBlazorServer.git",
        Branch = "main"
    };

    public static IEnumerable<TestRepoInfo> All => new[]
    {
        CleanArchitecture,
        FastEndpoints,
        EShop
    };
}
