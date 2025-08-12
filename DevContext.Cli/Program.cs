using System;
using DevContext.Core;

namespace DevContext.Cli
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var targetDir = string.Empty;

            if (args.Length == 0)
            {
                targetDir = Environment.CurrentDirectory;
            }

            if (!System.IO.Directory.Exists(targetDir))
            {
                Console.WriteLine($"❌ Directory not found: {targetDir}");
                return;
            }

            IProjectDetector detector = new GenericDotNetProjectDetector();

            Console.WriteLine($"🔍 Checking directory: {targetDir}");
            if (!detector.Detect(targetDir))
            {
                Console.WriteLine("❌ No .NET project detected.");
                return;
            }

            Console.WriteLine("✅ .NET project detected. Extracting info...\n");

            var result = detector.Extract(targetDir);

            Console.WriteLine("===== Extraction Result =====");
            Console.WriteLine(result.Content);
        }
    }
}
