using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DevContext.Core.Extractors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevContext.Core
{
    public class ExtractionOptions
    {
        // Core options
        public bool IncludeMethodSignatures { get; set; } = true;
        public bool IncludeDependencyGraph { get; set; } = true;
        public bool IncludeCallGraph { get; set; } = true;
        public bool IncludeDomainModel { get; set; } = true;
        public bool UseMermaidForGraphs { get; set; } = true;
        public bool TokenCompact { get; set; } = true;
        public string? OutputFilePath { get; set; } = null;
        public bool VerboseOutput { get; set; } = true;

        // New performance options
        public bool EnableParallelProcessing { get; set; } = true;
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        // Feature grouping options
        public bool EnableFeatureGrouping { get; set; } = true;
        public ArchitectureStyle DetectedArchitecture { get; set; } = ArchitectureStyle.Unknown;
        public List<string> FeaturePatterns { get; set; } = new()
        {
            "Features/*",
            "Areas/*",
            "Modules/*",
            "Domain/*"
        };

        // Filtering options
        public List<string> ExcludeDirectories { get; set; } = new()
        {
            ".git", ".vs", ".nuke", ".github", "bin", "obj",
            ".idea", "nupkg", ".packageguard", "node_modules"
        };

        public List<string> ExcludeNamespaces { get; set; } = new()
        {
            "Migrations",
            "*.Migrations.*",
            "*.Tests.*",
            "*.Test.*"
        };

        public List<string> TrivialMethodPatterns { get; set; } = new()
        {
            "get_*",
            "set_*",
            "OnInitialized",
            "OnParametersSet",
            "Dispose"
        };

        // Graph options
        public int MaxCallGraphDepth { get; set; } = int.MaxValue;
        public int MinMethodComplexity { get; set; } = 2; // Cyclomatic complexity threshold

        // Output options
        public bool ShowElapsedTime { get; set; } = true;
        public bool ShowMemoryUsage { get; set; } = true;
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Markdown;

        // === New v1: Depth + Layer/Focus Awareness (core to making output LLM-prompt-ready) ===
        public ExtractionDepth Depth { get; set; } = ExtractionDepth.Balanced;
        public ExtractionFocus Focus { get; set; } = ExtractionFocus.General;

        /// <summary>
        /// When Focus == Feature, these are the feature/slice names to emphasize.
        /// </summary>
        public List<string> FocusedFeatures { get; set; } = new();
    }

    public enum ArchitectureStyle
    {
        Unknown,
        MVC,
        MinimalApi,
        Blazor,
        RazorPages,
        FastEndpoints,
        CleanArchitecture,
        VerticalSlice,
        Modular
    }

    public enum OutputFormat
    {
        Markdown,
        Json,
        Html,
        PlainText
    }

    /// <summary>
    /// Controls how much detail and noise is included in the extraction.
    /// This is a key lever for making output useful when attached to LLM prompts.
    /// </summary>
    public enum ExtractionDepth
    {
        /// <summary>High-level architecture, layers, boundaries, key contracts. Minimal implementation detail.</summary>
        Shallow,

        /// <summary>Good balance for most "understand this codebase" use cases (default).</summary>
        Balanced,

        /// <summary>Maximum detail — deeper call graphs, more internals, less filtering. Use for deep implementation or debugging work.</summary>
        Deep
    }

    /// <summary>
    /// Expresses the user's intent for this extraction run.
    /// Allows the tool to make smart decisions about what to emphasize or de-emphasize.
    /// </summary>
    public enum ExtractionFocus
    {
        /// <summary>General purpose — current broad behavior.</summary>
        General,

        /// <summary>Focus on architecture, layers, system boundaries and high-level design.</summary>
        Architecture,

        /// <summary>Focus on one or more specific features / vertical slices.</summary>
        Feature,

        /// <summary>Deep implementation details inside selected areas.</summary>
        Implementation,

        /// <summary>Debugging-oriented — richer call graphs and cross-layer flows.</summary>
        Debug
    }
}


namespace DevContext.Core.Extractors
{
    public class FeatureDetector
    {
        private readonly ExtractionOptions _options;

        public FeatureDetector(ExtractionOptions options)
        {
            _options = options;
        }

        public async Task<FeatureGrouping> DetectFeaturesAsync(Solution solution)
        {
            var result = new FeatureGrouping();

            // Detect architecture style
            result.Architecture = await DetectArchitectureStyleAsync(solution);

            // Group by features based on detected architecture
            result.Features = result.Architecture switch
            {
                ArchitectureStyle.FastEndpoints => await GroupByFastEndpointsAsync(solution),
                ArchitectureStyle.MinimalApi => await GroupByMinimalApiAsync(solution),
                ArchitectureStyle.MVC => await GroupByMvcAreasAsync(solution),
                ArchitectureStyle.CleanArchitecture => await GroupByCleanArchitectureAsync(solution),
                ArchitectureStyle.VerticalSlice => await GroupByVerticalSlicesAsync(solution),
                _ => await GroupByNamespacePatternAsync(solution)
            };

            return result;
        }

        private async Task<ArchitectureStyle> DetectArchitectureStyleAsync(Solution solution)
        {
            var indicators = new Dictionary<ArchitectureStyle, int>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                // Check for FastEndpoints
                if (compilation.ReferencedAssemblyNames.Any(a => a.Name.Contains("FastEndpoints")))
                    indicators[ArchitectureStyle.FastEndpoints] = indicators.GetValueOrDefault(ArchitectureStyle.FastEndpoints) + 10;

                bool hasMapGetPost = false;
                bool hasControllers = false;
                bool hasRazorPages = false;
                bool hasFeaturesFolders = false;

                foreach (var doc in project.Documents)
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();
                    var text = root.GetText().ToString();

                    if (text.Contains("app.MapGet") || text.Contains("app.MapPost"))
                        hasMapGetPost = true;

                    if (text.Contains(": Controller") || text.Contains(": ControllerBase"))
                        hasControllers = true;

                    if (text.Contains("@page") && text.Contains("PageModel"))
                        hasRazorPages = true;

                    var path = doc.FilePath ?? "";
                    if (path.Contains("/Features/") || path.Contains("\\Features\\") ||
                        path.Contains("/Slices/") || path.Contains("\\Slices\\"))
                        hasFeaturesFolders = true;
                }

                if (hasMapGetPost)
                    indicators[ArchitectureStyle.MinimalApi] = indicators.GetValueOrDefault(ArchitectureStyle.MinimalApi) + 6;
                if (hasControllers)
                    indicators[ArchitectureStyle.MVC] = indicators.GetValueOrDefault(ArchitectureStyle.MVC) + 6;
                if (hasRazorPages)
                    indicators[ArchitectureStyle.RazorPages] = indicators.GetValueOrDefault(ArchitectureStyle.RazorPages) + 5;
                if (hasFeaturesFolders)
                    indicators[ArchitectureStyle.VerticalSlice] = indicators.GetValueOrDefault(ArchitectureStyle.VerticalSlice) + 8;

                // Check folder structure / naming for Clean Architecture / Onion
                var name = project.Name;
                if (name.EndsWith(".Domain") || name.EndsWith(".Core") || name.Contains(".Domain"))
                    indicators[ArchitectureStyle.CleanArchitecture] = indicators.GetValueOrDefault(ArchitectureStyle.CleanArchitecture) + 9;
                if (name.EndsWith(".Application") || name.Contains(".Application"))
                    indicators[ArchitectureStyle.CleanArchitecture] += 7;
                if (name.EndsWith(".Infrastructure") || name.Contains(".Infrastructure"))
                    indicators[ArchitectureStyle.CleanArchitecture] += 5;

                // CLI / Console tool detection (important for tools like DevContext itself)
                if (name.Contains("Cli") || name.Contains("Console") || name.Contains("Tool"))
                    indicators[ArchitectureStyle.Unknown] = indicators.GetValueOrDefault(ArchitectureStyle.Unknown) + 2;
            }

            if (indicators.Any())
            {
                // Prefer CleanArchitecture or VerticalSlice over MinimalApi when signals are close
                var top = indicators.OrderByDescending(kvp => kvp.Value).First();
                if (top.Key == ArchitectureStyle.MinimalApi && indicators.ContainsKey(ArchitectureStyle.CleanArchitecture))
                    return ArchitectureStyle.CleanArchitecture;
                if (top.Key == ArchitectureStyle.MinimalApi && indicators.ContainsKey(ArchitectureStyle.VerticalSlice))
                    return ArchitectureStyle.VerticalSlice;

                return top.Key;
            }

            return ArchitectureStyle.Unknown;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByFastEndpointsAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                foreach (var doc in project.Documents)
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();
                    var model = compilation.GetSemanticModel(tree);

                    // Find all classes that inherit from Endpoint<TRequest, TResponse>
                    var endpointClasses = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Where(c => c.BaseList?.Types.Any(t =>
                            t.ToString().StartsWith("Endpoint<") ||
                            t.ToString().StartsWith("EndpointWithoutRequest")) == true);

                    foreach (var endpoint in endpointClasses)
                    {
                        var namespaceName = GetNamespace(endpoint);
                        var featureName = ExtractFeatureName(namespaceName, endpoint.Identifier.Text);

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        features[featureName].Endpoints.Add(new EndpointInfo
                        {
                            Name = endpoint.Identifier.Text,
                            HttpMethod = DetectHttpMethod(endpoint),
                            Route = ExtractRoute(endpoint),
                            Namespace = namespaceName
                        });
                    }
                }
            }

            return features;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByMinimalApiAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                foreach (var doc in project.Documents)
                {
                    if (!doc.FilePath?.Contains("Program.cs") == true &&
                        !doc.FilePath?.Contains("Endpoints") == true)
                        continue;

                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();

                    // Find MapGet, MapPost, etc. invocations
                    var mapMethods = root.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(i => i.Expression.ToString().Contains("Map"));

                    foreach (var mapMethod in mapMethods)
                    {
                        var methodName = mapMethod.Expression.ToString();
                        var route = ExtractRouteFromInvocation(mapMethod);
                        var featureName = ExtractFeatureFromRoute(route);

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        features[featureName].Endpoints.Add(new EndpointInfo
                        {
                            Route = route,
                            HttpMethod = ExtractHttpMethodFromMapMethod(methodName)
                        });
                    }
                }
            }

            return features;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByMvcAreasAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                foreach (var doc in project.Documents)
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();
                    var model = compilation.GetSemanticModel(tree);

                    // Find all controller classes
                    var controllers = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Where(c => c.Identifier.Text.EndsWith("Controller"));

                    foreach (var controller in controllers)
                    {
                        var area = ExtractAreaFromAttributes(controller) ?? "Default";
                        var controllerName = controller.Identifier.Text.Replace("Controller", "");
                        var featureName = $"{area}/{controllerName}";

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        // Find action methods
                        var actions = controller.Members
                            .OfType<MethodDeclarationSyntax>()
                            .Where(m => m.Modifiers.Any(mod => mod.Text == "public"));

                        foreach (var action in actions)
                        {
                            features[featureName].Endpoints.Add(new EndpointInfo
                            {
                                Name = action.Identifier.Text,
                                Controller = controllerName,
                                Area = area
                            });
                        }
                    }
                }
            }

            return features;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByCleanArchitectureAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            // Group by Use Cases / Commands / Queries
            foreach (var project in solution.Projects)
            {
                if (!project.Name.Contains("Application"))
                    continue;

                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                foreach (var doc in project.Documents)
                {
                    var path = doc.FilePath ?? "";

                    // Extract feature from path (e.g., Application/Products/Commands/CreateProduct)
                    var match = Regex.Match(path, @"Application[/\\](\w+)[/\\]");
                    if (match.Success)
                    {
                        var featureName = match.Groups[1].Value;

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        var tree = await doc.GetSyntaxTreeAsync();
                        if (tree == null)
                            continue;

                        var root = await tree.GetRootAsync();

                        // Find command/query handlers
                        var handlers = root.DescendantNodes()
                            .OfType<ClassDeclarationSyntax>()
                            .Where(c => c.Identifier.Text.EndsWith("Handler"));

                        foreach (var handler in handlers)
                        {
                            features[featureName].UseCases.Add(new UseCaseInfo
                            {
                                Name = handler.Identifier.Text,
                                Type = handler.Identifier.Text.Contains("Query") ? "Query" : "Command"
                            });
                        }
                    }
                }
            }

            return features;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByVerticalSlicesAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            foreach (var project in solution.Projects)
            {
                foreach (var doc in project.Documents)
                {
                    var path = doc.FilePath ?? "";

                    // Look for Features folder pattern
                    var match = Regex.Match(path, @"Features[/\\](\w+)[/\\]");
                    if (match.Success)
                    {
                        var featureName = match.Groups[1].Value;

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        // Add file to feature
                        features[featureName].Files.Add(Path.GetFileName(path));
                    }
                }
            }

            return features;
        }

        private async Task<Dictionary<string, FeatureInfo>> GroupByNamespacePatternAsync(Solution solution)
        {
            var features = new Dictionary<string, FeatureInfo>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                // Group by second-level namespace
                var namespaces = compilation.SyntaxTrees
                    .SelectMany(tree => tree.GetRoot().DescendantNodes())
                    .OfType<NamespaceDeclarationSyntax>()
                    .Select(n => n.Name.ToString())
                    .Distinct();

                foreach (var ns in namespaces)
                {
                    var parts = ns.Split('.');
                    if (parts.Length >= 2)
                    {
                        var featureName = parts[1];

                        if (!features.ContainsKey(featureName))
                            features[featureName] = new FeatureInfo { Name = featureName };

                        features[featureName].Namespaces.Add(ns);
                    }
                }
            }

            return features;
        }

        // Helper methods
        private string GetNamespace(SyntaxNode node)
        {
            var namespaceDeclaration = node.Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();

            return namespaceDeclaration?.Name.ToString() ?? "Global";
        }

        private string ExtractFeatureName(string namespaceName, string className)
        {
            // Try to extract from namespace (e.g., Features.Products.CreateProduct)
            var match = Regex.Match(namespaceName, @"Features\.(\w+)");
            if (match.Success)
                return match.Groups[1].Value;

            // Try to extract from class name pattern
            match = Regex.Match(className, @"(\w+)(Endpoint|Controller|Handler)");
            if (match.Success)
                return match.Groups[1].Value;

            return "General";
        }

        private string DetectHttpMethod(ClassDeclarationSyntax endpoint)
        {
            // Check for HTTP method attributes
            var attributes = endpoint.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(a => a.Name.ToString());

            foreach (var attr in attributes)
            {
                if (attr.Contains("HttpGet"))
                    return "GET";
                if (attr.Contains("HttpPost"))
                    return "POST";
                if (attr.Contains("HttpPut"))
                    return "PUT";
                if (attr.Contains("HttpDelete"))
                    return "DELETE";
                if (attr.Contains("HttpPatch"))
                    return "PATCH";
            }

            // Check method name conventions
            var className = endpoint.Identifier.Text;
            if (className.Contains("Get") || className.Contains("List"))
                return "GET";
            if (className.Contains("Create") || className.Contains("Add"))
                return "POST";
            if (className.Contains("Update") || className.Contains("Edit"))
                return "PUT";
            if (className.Contains("Delete") || className.Contains("Remove"))
                return "DELETE";

            return "UNKNOWN";
        }

        private string ExtractRoute(ClassDeclarationSyntax endpoint)
        {
            // Look for Route attribute
            var routeAttr = endpoint.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Contains("Route"));

            if (routeAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }

            return "/unknown";
        }

        private string ExtractRouteFromInvocation(InvocationExpressionSyntax invocation)
        {
            var firstArg = invocation.ArgumentList?.Arguments.FirstOrDefault();
            if (firstArg?.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }
            return "/unknown";
        }

        private string ExtractFeatureFromRoute(string route)
        {
            // Extract feature from route pattern (e.g., /api/products -> Products)
            var segments = route.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                return char.ToUpper(segments[1][0]) + segments[1].Substring(1);
            }
            return "General";
        }

        private string ExtractHttpMethodFromMapMethod(string methodName)
        {
            if (methodName.Contains("MapGet"))
                return "GET";
            if (methodName.Contains("MapPost"))
                return "POST";
            if (methodName.Contains("MapPut"))
                return "PUT";
            if (methodName.Contains("MapDelete"))
                return "DELETE";
            if (methodName.Contains("MapPatch"))
                return "PATCH";
            return "UNKNOWN";
        }

        private string? ExtractAreaFromAttributes(ClassDeclarationSyntax controller)
        {
            var areaAttr = controller.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Contains("Area"));

            if (areaAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }

            return null;
        }
    }

    // Supporting classes
    public class FeatureGrouping
    {
        public ArchitectureStyle Architecture { get; set; }
        public Dictionary<string, FeatureInfo> Features { get; set; } = new();
    }

    public class FeatureInfo
    {
        public string Name { get; set; } = "";
        public List<EndpointInfo> Endpoints { get; set; } = new();
        public List<UseCaseInfo> UseCases { get; set; } = new();
        public List<string> Files { get; set; } = new();
        public List<string> Namespaces { get; set; } = new();
    }

    public class EndpointInfo
    {
        public string Name { get; set; } = "";
        public string? Route { get; set; }
        public string? HttpMethod { get; set; }
        public string? Controller { get; set; }
        public string? Area { get; set; }
        public string? Namespace { get; set; }
    }

    public class UseCaseInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // Command, Query, etc.
    }
}



