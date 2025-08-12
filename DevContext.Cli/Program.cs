using System;
using System.IO;
using DevContext.Core;

namespace DevContext.Cli
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var targetDir = Environment.CurrentDirectory;
            var options = new ExtractionOptions();

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
            Console.WriteLine("  directory    Target directory (default: current directory)");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  -s, --include-signatures    Include detailed method signatures (increases token count)");
            Console.WriteLine("  -o, --output <file>         Save output to file instead of console");
            Console.WriteLine("  -v, --verbose              Enable verbose output");
            Console.WriteLine("  -h, --help                 Show this help");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  devcontext                                    # Analyze current directory");
            Console.WriteLine("  devcontext C:\\MyProject                       # Analyze specific directory");
            Console.WriteLine("  devcontext -s -o context.md                  # Include signatures, save to file");
            Console.WriteLine("  devcontext --include-signatures --verbose    # Detailed analysis with verbose output");
            Console.WriteLine();
            Console.WriteLine("OUTPUT:");
            Console.WriteLine("  By default, outputs compact structure without method signatures to minimize");
            Console.WriteLine("  token usage for LLM context. Use -s for detailed method signatures when needed.");
        }
    }
}
