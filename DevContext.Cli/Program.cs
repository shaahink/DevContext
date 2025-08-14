using System;
using System.IO;
using DevContext.Core;
using DevContext.Core.Extractors;

namespace DevContext.Cli
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var targetDir = Environment.CurrentDirectory;

#if DEBUG
            targetDir = @"C:\code\github\dntsite";
#endif
            var options = new ExtractionOptions
            {
                TokenCompact = true // Default to token-saving mode for LLM usage
            };

            // Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--help":
                    case "-h":
                        ShowHelp();
                        return;

                    case "--include-signatures":
                    case "-s":
                        options.IncludeMethodSignatures = true;
                        break;

                    case "--include-dependency-graph":
                    case "-d":
                        options.IncludeDependencyGraph = true;
                        break;

                    case "--include-call-graph":
                    case "-c":
                        options.IncludeCallGraph = true;
                        break;

                    case "--include-domain-model":
                    case "-m":
                        options.IncludeDomainModel = true;
                        break;

                    case "--mermaid-graph":
                        options.UseMermaidForGraphs = true;
                        break;

                    case "--token-compact":
                        options.TokenCompact = true;
                        break;

                    case "--no-token-compact":
                        options.TokenCompact = false;
                        break;

                    case "--output":
                    case "-o":
                        if (i + 1 < args.Length)
                        {
                            options.OutputFilePath = args[++i];
                        }
                        else
                        {
                            Console.WriteLine("❌ --output requires a file path");
                            return;
                        }
                        break;

                    case "--verbose":
                    case "-v":
                        options.VerboseOutput = true;
                        break;

                    default:
                        if (!args[i].StartsWith("-"))
                        {
                            targetDir = args[i];
                        }
                        else
                        {
                            Console.WriteLine($"❌ Unknown option: {args[i]}");
                            Console.WriteLine("Use --help for usage information");
                            return;
                        }
                        break;
                }
            }

            // Validate directory
            if (!Directory.Exists(targetDir))
            {
                Console.WriteLine($"❌ Directory not found: {targetDir}");
                return;
            }

            IProjectDetector detector = new GenericDotNetProjectDetector(options);

            Console.WriteLine($"🔍 Checking directory: {targetDir}");
            Console.WriteLine($"📝 Method signatures: {(options.IncludeMethodSignatures ? "Included" : "Excluded (use -s to include)")}");
            Console.WriteLine($"📊 Dependency graph: {(options.IncludeDependencyGraph ? "Included" : "Excluded (use -d to include)")}");
            Console.WriteLine($"🔗 Call graph: {(options.IncludeCallGraph ? "Included" : "Excluded (use -c to include)")}");
            Console.WriteLine($"🏛️ Domain model: {(options.IncludeDomainModel ? "Included" : "Excluded (use -m to include)")}");
            Console.WriteLine($"🗜️ Token compact: {(options.TokenCompact ? "Enabled (optimized for LLMs)" : "Disabled (verbose output)")}");

            if (options.UseMermaidForGraphs && (options.IncludeDependencyGraph || options.IncludeCallGraph))
            {
                Console.WriteLine($"📈 Graph format: Mermaid");
            }

            if (!string.IsNullOrEmpty(options.OutputFilePath))
            {
                Console.WriteLine($"💾 Output will be saved to: {options.OutputFilePath}");
            }

            if (!detector.Detect(targetDir))
            {
                Console.WriteLine("❌ No .NET project detected.");
                return;
            }

            Console.WriteLine("✅ .NET project detected. Extracting info...\n");

            var result = detector.Extract(targetDir);

            // Only output to console if no file output specified
            if (string.IsNullOrEmpty(options.OutputFilePath))
            {
                Console.WriteLine("===== Extraction Result =====");
                Console.WriteLine(result.Content);
            }
            else
            {
                // Show summary stats
                var lines = result.Content.Split('\n').Length;
                var sizeKb = System.Text.Encoding.UTF8.GetByteCount(result.Content) / 1024.0;
                Console.WriteLine($"📊 Generated {lines:N0} lines ({sizeKb:F1} KB)");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("DevContext - .NET Project Context Extractor for LLMs");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  devcontext [directory] [options]");
            Console.WriteLine();
            Console.WriteLine("ARGUMENTS:");
            Console.WriteLine("  directory                    Target directory (default: current directory)");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  -s, --include-signatures     Include detailed method signatures");
            Console.WriteLine("  -d, --include-dependency-graph  Include project dependency graph");
            Console.WriteLine("  -c, --include-call-graph     Include method-to-method call mapping");
            Console.WriteLine("  -m, --include-domain-model   Include domain entities and models");
            Console.WriteLine("  --mermaid-graph              Output graphs in Mermaid format (default: plain text)");
            Console.WriteLine("  --token-compact              Enable token-saving mode (DEFAULT: on for LLMs)");
            Console.WriteLine("  --no-token-compact           Disable token-saving mode for verbose output");
            Console.WriteLine("  -o, --output <file>          Save output to file instead of console");
            Console.WriteLine("  -v, --verbose                Enable verbose output");
            Console.WriteLine("  -h, --help                   Show this help");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  devcontext                   # Minimal analysis with token-saving (best for LLMs)");
            Console.WriteLine("  devcontext -s -d -c          # Include signatures, dependencies, and call graph");
            Console.WriteLine("  devcontext -o context.md     # Save to file");
            Console.WriteLine("  devcontext --no-token-compact --verbose  # Detailed human-readable output");
            Console.WriteLine("  devcontext -d --mermaid-graph  # Include dependency graph in Mermaid format");
            Console.WriteLine();
            Console.WriteLine("NOTES:");
            Console.WriteLine("  Token-compact mode is ON by default to optimize for LLM context windows.");
            Console.WriteLine("  Use --no-token-compact for human-readable verbose output.");
        }
    }
}
