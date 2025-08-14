//using System.Text.RegularExpressions;
//using Microsoft.CodeAnalysis;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;



//namespace DevContext.Core.Extractors
//{
//    public class TokenCompressor
//    {
//        private readonly CompressionOptions _options;
//        private readonly Dictionary<string, string> _abbreviationCache;
//        private readonly HashSet<string> _seenPatterns;

//        public TokenCompressor(CompressionOptions? options = null)
//        {
//            _options = options ?? new CompressionOptions();
//            _abbreviationCache = new Dictionary<string, string>();
//            _seenPatterns = new HashSet<string>();
//        }

//        public string Compress(string content)
//        {
//            // Skip compression if content is small or compression is disabled
//            if (!_options.Enabled || content.Length < _options.MinContentLength)
//                return content;

//            var originalSize = content.Length;
//            var compressed = content;

//            // Apply compression stages in order of impact
//            if (_options.CompressModifiers)
//                compressed = CompressModifiers(compressed);

//            if (_options.CompressNamespaces)
//                compressed = CompressNamespaces(compressed);

//            if (_options.CompressTypes)
//                compressed = CompressCommonTypes(compressed);

//            if (_options.RemoveRedundantPhrases)
//                compressed = RemoveRedundantPhrases(compressed);

//            if (_options.CompactWhitespace)
//                compressed = CompactWhitespace(compressed);

//            if (_options.CompactLists)
//                compressed = CompactLists(compressed);

//            if (_options.CompressMethodSignatures)
//                compressed = CompressMethodSignatures(compressed);

//            if (_options.RemoveEmptySections)
//                compressed = RemoveEmptySections(compressed);

//            if (_options.CompressMarkdown)
//                compressed = CompressMarkdown(compressed);

//            if (_options.DeduplicatePatterns)
//                compressed = DeduplicatePatterns(compressed);

//            if (_options.UseAbbreviations)
//                compressed = ApplyContextualAbbreviations(compressed);

//            if (_options.CompressNumbers)
//                compressed = CompressNumbers(compressed);

//            // Final cleanup
//            compressed = FinalCleanup(compressed);

//            // Report compression if significant
//            ReportCompression(originalSize, compressed.Length);

//            return compressed;
//        }

//        private string CompressModifiers(string content)
//        {
//            var modifierMap = new Dictionary<string, string>
//            {
//                // Access modifiers
//                { "public ", "pub " },
//                { "private ", "priv " },
//                { "protected ", "prot " },
//                { "internal ", "int " },
//                { "protected internal ", "prot int " },
//                { "private protected ", "priv prot " },
                
//                // Method modifiers
//                { "static ", "stat " },
//                { "async ", "async " },
//                { "override ", "ovr " },
//                { "virtual ", "virt " },
//                { "abstract ", "abs " },
//                { "sealed ", "seal " },
//                { "readonly ", "ro " },
//                { "const ", "const " },
//                { "extern ", "ext " },
//                { "unsafe ", "unsf " },
//                { "volatile ", "vol " },
//                { "partial ", "part " },
                
//                // Combined patterns
//                { "public static ", "pub stat " },
//                { "private static ", "priv stat " },
//                { "public async ", "pub async " },
//                { "private async ", "priv async " },
//                { "public virtual ", "pub virt " },
//                { "public override ", "pub ovr " },
//                { "protected override ", "prot ovr " },
//                { "public abstract ", "pub abs " },
//                { "protected abstract ", "prot abs " }
//            };

//            // Apply replacements in order of length (longest first to avoid partial replacements)
//            foreach (var kvp in modifierMap.OrderByDescending(x => x.Key.Length))
//            {
//                content = Regex.Replace(content,
//                    @"\b" + Regex.Escape(kvp.Key),
//                    kvp.Value,
//                    RegexOptions.Multiline);
//            }

//            return content;
//        }

//        private string CompressNamespaces(string content)
//        {
//            var namespaceMap = new Dictionary<string, string>
//            {
//                // System namespaces
//                { "System.Collections.Generic", "SCG" },
//                { "System.Collections", "SC" },
//                { "System.Linq", "SL" },
//                { "System.Text", "ST" },
//                { "System.Threading.Tasks", "STT" },
//                { "System.Threading", "STh" },
//                { "System.IO", "SIO" },
//                { "System.Net.Http", "SNH" },
//                { "System.Net", "SN" },
//                { "System.Data", "SD" },
//                { "System.Xml", "SX" },
//                { "System.Text.Json", "STJ" },
//                { "System.Text.RegularExpressions", "STR" },
//                { "System.Diagnostics", "SDg" },
//                { "System.Reflection", "SR" },
//                { "System.Runtime", "SRt" },
//                { "System.ComponentModel", "SCM" },
//                { "System.Configuration", "SCf" },
//                { "System.", "S." },
                
//                // Microsoft namespaces
//                { "Microsoft.Extensions.DependencyInjection", "MEDI" },
//                { "Microsoft.Extensions.Configuration", "MEC" },
//                { "Microsoft.Extensions.Logging", "MEL" },
//                { "Microsoft.Extensions.Options", "MEO" },
//                { "Microsoft.Extensions.Hosting", "MEH" },
//                { "Microsoft.Extensions", "ME" },
//                { "Microsoft.AspNetCore.Mvc", "MAM" },
//                { "Microsoft.AspNetCore.Http", "MAH" },
//                { "Microsoft.AspNetCore.Authorization", "MAA" },
//                { "Microsoft.AspNetCore.Authentication", "MAAu" },
//                { "Microsoft.AspNetCore.Identity", "MAI" },
//                { "Microsoft.AspNetCore.Components", "MAC" },
//                { "Microsoft.AspNetCore.SignalR", "MAS" },
//                { "Microsoft.AspNetCore.Cors", "MACo" },
//                { "Microsoft.AspNetCore.Routing", "MAR" },
//                { "Microsoft.AspNetCore", "MA" },
//                { "Microsoft.EntityFrameworkCore", "EF" },
//                { "Microsoft.CodeAnalysis.CSharp", "MCC" },
//                { "Microsoft.CodeAnalysis", "MC" },
//                { "Microsoft.Build", "MB" },
//                { "Microsoft.VisualStudio", "MVS" },
//                { "Microsoft.Azure", "MAz" },
//                { "Microsoft.Data.SqlClient", "MDS" },
//                { "Microsoft.Data", "MD" },
//                { "Microsoft.", "M." },
                
//                // Common third-party
//                { "Newtonsoft.Json", "NJ" },
//                { "AutoMapper", "AM" },
//                { "FluentValidation", "FV" },
//                { "MediatR", "MR" },
//                { "Polly", "PL" },
//                { "Serilog", "SL" },
//                { "NLog", "NL" },
//                { "Dapper", "DP" },
//                { "FluentAssertions", "FA" },
//                { "Moq", "MQ" },
//                { "xunit", "XU" },
//                { "NUnit", "NU" },
//                { "Swashbuckle", "SW" },
//                { "IdentityServer", "IS" },
//                { "MassTransit", "MT" },
//                { "RabbitMQ", "RMQ" },
//                { "MongoDB", "MDB" },
//                { "Redis", "RD" },
//                { "Elasticsearch", "ES" }
//            };

//            // Apply namespace compressions
//            foreach (var kvp in namespaceMap.OrderByDescending(x => x.Key.Length))
//            {
//                content = content.Replace(kvp.Key, kvp.Value);
//            }

//            // Compress using statements
//            content = Regex.Replace(content, @"^using\s+", "u ", RegexOptions.Multiline);
//            content = Regex.Replace(content, @"^using\s+static\s+", "us ", RegexOptions.Multiline);
//            content = Regex.Replace(content, @"^global\s+using\s+", "gu ", RegexOptions.Multiline);

//            return content;
//        }

//        private string CompressCommonTypes(string content)
//        {
//            var typeMap = new Dictionary<string, string>
//            {
//                // Basic types
//                { "string", "str" },
//                { "object", "obj" },
//                { "dynamic", "dyn" },
//                { "decimal", "dec" },
//                { "double", "dbl" },
//                { "float", "flt" },
//                { "boolean", "bool" },
//                { "DateTime", "DT" },
//                { "TimeSpan", "TS" },
//                { "Guid", "G" },
//                { "CancellationToken", "CT" },
//                { "CancellationTokenSource", "CTS" },
                
//                // Collections
//                { "List<", "L<" },
//                { "Dictionary<", "D<" },
//                { "HashSet<", "HS<" },
//                { "Queue<", "Q<" },
//                { "Stack<", "St<" },
//                { "LinkedList<", "LL<" },
//                { "ObservableCollection<", "OC<" },
//                { "Collection<", "C<" },
//                { "IEnumerable<", "IE<" },
//                { "ICollection<", "IC<" },
//                { "IList<", "IL<" },
//                { "IDictionary<", "ID<" },
//                { "IReadOnlyList<", "IRL<" },
//                { "IReadOnlyDictionary<", "IRD<" },
//                { "IQueryable<", "IQ<" },
//                { "IAsyncEnumerable<", "IAE<" },
                
//                // Tasks
//                { "Task<", "T<" },
//                { "ValueTask<", "VT<" },
//                { "Task", "T" },
//                { "ValueTask", "VT" },
                
//                // Common generics
//                { "Nullable<", "N<" },
//                { "Action<", "A<" },
//                { "Func<", "F<" },
//                { "Predicate<", "P<" },
//                { "Comparison<", "Cmp<" },
//                { "Converter<", "Cnv<" },
//                { "EventHandler<", "EH<" },
                
//                // ASP.NET Core types
//                { "ActionResult<", "AR<" },
//                { "IActionResult", "IAR" },
//                { "HttpContext", "HC" },
//                { "HttpRequest", "HReq" },
//                { "HttpResponse", "HRes" },
//                { "ControllerBase", "CB" },
//                { "Controller", "C" },
//                { "DbContext", "DC" },
//                { "DbSet<", "DS<" },
                
//                // Common interfaces
//                { "ILogger<", "ILg<" },
//                { "IOptions<", "IO<" },
//                { "IConfiguration", "ICfg" },
//                { "IServiceCollection", "ISC" },
//                { "IServiceProvider", "ISP" },
//                { "IHostBuilder", "IHB" },
//                { "IWebHostBuilder", "IWHB" },
//                { "IApplicationBuilder", "IAB" },
//                { "IEndpointRouteBuilder", "IERB" },
                
//                // Attributes (when in brackets)
//                { "[HttpGet]", "[Get]" },
//                { "[HttpPost]", "[Post]" },
//                { "[HttpPut]", "[Put]" },
//                { "[HttpDelete]", "[Del]" },
//                { "[HttpPatch]", "[Patch]" },
//                { "[Authorize]", "[Auth]" },
//                { "[AllowAnonymous]", "[Anon]" },
//                { "[Required]", "[Req]" },
//                { "[StringLength", "[StrLen" },
//                { "[MaxLength", "[MaxLen" },
//                { "[MinLength", "[MinLen" },
//                { "[Range", "[Rng" },
//                { "[EmailAddress]", "[Email]" },
//                { "[Phone]", "[Tel]" },
//                { "[CreditCard]", "[CC]" },
//                { "[DataType", "[DT" },
//                { "[Display", "[Disp" },
//                { "[ScaffoldColumn", "[ScCol" },
//                { "[ForeignKey", "[FK" },
//                { "[InverseProperty", "[InvProp" },
//                { "[NotMapped]", "[NoMap]" }
//            };

//            // Apply type compressions, but be careful with word boundaries
//            foreach (var kvp in typeMap.OrderByDescending(x => x.Key.Length))
//            {
//                // For types without special characters, use word boundaries
//                if (!kvp.Key.Contains("<") && !kvp.Key.Contains("["))
//                {
//                    content = Regex.Replace(content,
//                        @"\b" + Regex.Escape(kvp.Key) + @"\b",
//                        kvp.Value);
//                }
//                else
//                {
//                    // For generics and attributes, direct replacement
//                    content = content.Replace(kvp.Key, kvp.Value);
//                }
//            }

//            return content;
//        }

//        private string RemoveRedundantPhrases(string content)
//        {
//            var redundantPhrases = new Dictionary<string, string>
//            {
//                // File paths and search patterns
//                { "SearchOption.AllDirectories", "**" },
//                { "SearchOption.TopDirectoryOnly", "*" },
//                { "StringComparison.OrdinalIgnoreCase", "OIC" },
//                { "StringComparison.Ordinal", "O" },
//                { "StringComparison.InvariantCultureIgnoreCase", "ICIC" },
//                { "StringComparison.InvariantCulture", "IC" },
//                { "StringComparison.CurrentCultureIgnoreCase", "CCIC" },
//                { "StringComparison.CurrentCulture", "CC" },
//                { "StringSplitOptions.RemoveEmptyEntries", "REE" },
//                { "StringSplitOptions.None", "" },
//                { "RegexOptions.IgnoreCase", "RIC" },
//                { "RegexOptions.Multiline", "RM" },
//                { "RegexOptions.Singleline", "RS" },
                
//                // Common phrases
//                { "file-scoped namespace", "fsn" },
//                { "record class", "rec" },
//                { "record struct", "recs" },
//                { "init-only", "init" },
//                { "expression-bodied", "=>" },
//                { "auto-implemented", "auto" },
//                { "dependency injection", "DI" },
//                { "inversion of control", "IoC" },
//                { "model-view-controller", "MVC" },
//                { "model-view-viewmodel", "MVVM" },
//                { "create, read, update, delete", "CRUD" },
//                { "cross-origin resource sharing", "CORS" },
//                { "javascript object notation", "JSON" },
//                { "extensible markup language", "XML" },
//                { "language integrated query", "LINQ" },
//                { "object-relational mapping", "ORM" },
//                { "domain-driven design", "DDD" },
//                { "test-driven development", "TDD" },
//                { "behavior-driven development", "BDD" },
//                { "continuous integration", "CI" },
//                { "continuous deployment", "CD" },
//                { "application programming interface", "API" },
//                { "software development kit", "SDK" },
//                { "integrated development environment", "IDE" },
//                { "object-oriented programming", "OOP" },
//                { "aspect-oriented programming", "AOP" },
//                { "service-oriented architecture", "SOA" },
//                { "representational state transfer", "REST" },
//                { "remote procedure call", "RPC" },
//                { "message queuing telemetry transport", "MQTT" },
//                { "advanced message queuing protocol", "AMQP" },
                
//                // Header compressions
//                { "**Total", "**Tot" },
//                { "**Root Directory**:", "**Root**:" },
//                { "**Solution File**:", "**Sln**:" },
//                { "**Projects in Solution**:", "**Projs**:" },
//                { "**Target Framework**:", "**TF**:" },
//                { "**Target Frameworks**:", "**TFs**:" },
//                { "**Runtime Identifiers**:", "**RIDs**:" },
//                { "**Runtime Identifier**:", "**RID**:" },
//                { "**Lines of Code**:", "**LOC**:" },
//                { "**Number of", "**#" },
//                { "**Count of", "**#" },
//                { "**Total Files**:", "**Files**:" },
//                { "**Dependencies**:", "**Deps**:" },
//                { "**References**:", "**Refs**:" },
//                { "**Packages**:", "**Pkgs**:" },
//                { "**Namespaces**:", "**NS**:" },
//                { "**Classes**:", "**Cls**:" },
//                { "**Interfaces**:", "**IFs**:" },
//                { "**Methods**:", "**Mths**:" },
//                { "**Properties**:", "**Props**:" },
//                { "**Fields**:", "**Flds**:" },
//                { "**Events**:", "**Evts**:" },
//                { "**Delegates**:", "**Dlgs**:" },
//                { "**Enumerations**:", "**Enums**:" },
//                { "**Structures**:", "**Structs**:" }
//            };

//            foreach (var kvp in redundantPhrases.OrderByDescending(x => x.Key.Length))
//            {
//                content = content.Replace(kvp.Key, kvp.Value);
//            }

//            return content;
//        }

//        private string CompactWhitespace(string content)
//        {
//            // Replace multiple line breaks with double line breaks
//            content = Regex.Replace(content, @"\n{3,}", "\n\n");

//            // Remove trailing whitespace from lines
//            content = Regex.Replace(content, @"[ \t]+$", "", RegexOptions.Multiline);

//            // Replace multiple spaces with single space (except at line start for indentation)
//            content = Regex.Replace(content, @"(?<!^)[ ]{2,}", " ", RegexOptions.Multiline);

//            // Remove space before punctuation
//            content = Regex.Replace(content, @"\s+([,;:\.!?])", "$1");

//            // Remove space after opening brackets/parentheses
//            content = Regex.Replace(content, @"([\(\[\{])\s+", "$1");

//            // Remove space before closing brackets/parentheses
//            content = Regex.Replace(content, @"\s+([\)\]\}])", "$1");

//            // Compact spaces around operators (careful not to break code)
//            content = Regex.Replace(content, @"\s*=\s*", "=");
//            content = Regex.Replace(content, @"\s*\+=\s*", "+=");
//            content = Regex.Replace(content, @"\s*-=\s*", "-=");
//            content = Regex.Replace(content, @"\s*\*=\s*", "*=");
//            content = Regex.Replace(content, @"\s*/=\s*", "/=");
//            content = Regex.Replace(content, @"\s*==\s*", "==");
//            content = Regex.Replace(content, @"\s*!=\s*", "!=");
//            content = Regex.Replace(content, @"\s*>=\s*", ">=");
//            content = Regex.Replace(content, @"\s*<=\s*", "<=");
//            content = Regex.Replace(content, @"\s*&&\s*", "&&");
//            content = Regex.Replace(content, @"\s*\|\|\s*", "||");

//            return content;
//        }

//        private string CompactLists(string content)
//        {
//            // Convert bullet points to semicolon-separated lists
//            var lines = content.Split('\n').ToList();
//            var compactedLines = new List<string>();
//            var currentList = new List<string>();
//            var inList = false;

//            for (int i = 0; i < lines.Count; i++)
//            {
//                var line = lines[i];
//                var trimmedLine = line.TrimStart();

//                // Check if this is a list item
//                if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ") ||
//                    trimmedLine.StartsWith("+ ") || Regex.IsMatch(trimmedLine, @"^\d+\.\s"))
//                {
//                    inList = true;
//                    var item = Regex.Replace(trimmedLine, @"^[-\*\+]\s+", "");
//                    item = Regex.Replace(item, @"^\d+\.\s+", "");
//                    currentList.Add(item);
//                }
//                else if (inList && string.IsNullOrWhiteSpace(line))
//                {
//                    // End of list
//                    if (currentList.Count > 0)
//                    {
//                        compactedLines.Add(string.Join("; ", currentList));
//                        currentList.Clear();
//                    }
//                    inList = false;
//                    if (!_options.RemoveBlankLines)
//                        compactedLines.Add(line);
//                }
//                else
//                {
//                    if (inList && currentList.Count > 0)
//                    {
//                        compactedLines.Add(string.Join("; ", currentList));
//                        currentList.Clear();
//                        inList = false;
//                    }
//                    compactedLines.Add(line);
//                }
//            }

//            // Handle remaining list items
//            if (currentList.Count > 0)
//            {
//                compactedLines.Add(string.Join("; ", currentList));
//            }

//            return string.Join("\n", compactedLines);
//        }

//        private string CompressMethodSignatures(string content)
//        {
//            // Compress method signatures to be more compact
//            content = Regex.Replace(content, @"\(\s+", "(");
//            content = Regex.Replace(content, @"\s+\)", ")");
//            content = Regex.Replace(content, @"\s*,\s*", ",");
//            content = Regex.Replace(content, @"\s*<\s*", "<");
//            content = Regex.Replace(content, @"\s*>\s*", ">");

//            // Remove parameter names in signatures (keep only types)
//            if (_options.RemoveParameterNames)
//            {
//                content = Regex.Replace(content,
//                    @"\(([^)]*)\)",
//                    m => "(" + CompressParameters(m.Groups[1].Value) + ")");
//            }

//            // Compress return types
//            content = Regex.Replace(content, @":\s+", ":");
//            content = Regex.Replace(content, @"->\s+", "->");

//            return content;
//        }

//        private string CompressParameters(string parameters)
//        {
//            if (string.IsNullOrWhiteSpace(parameters))
//                return "";

//            var parts = parameters.Split(',');
//            var compressed = new List<string>();

//            foreach (var part in parts)
//            {
//                var trimmed = part.Trim();
//                // Try to extract just the type (remove parameter name)
//                var match = Regex.Match(trimmed, @"^([^\s]+)(\s+\w+)?$");
//                if (match.Success)
//                {
//                    compressed.Add(match.Groups[1].Value);
//                }
//                else
//                {
//                    compressed.Add(trimmed);
//                }
//            }

//            return string.Join(",", compressed);
//        }

//        private string RemoveEmptySections(string content)
//        {
//            // Remove headers with no content
//            content = Regex.Replace(content, @"^#{1,6}[^\n]+\n+(?=#{1,6}|\z)", "",
//                RegexOptions.Multiline);

//            // Remove empty code blocks
//            content = Regex.Replace(content, @"```[^\n]*\n\s*```", "");

//            // Remove empty parentheses, brackets, braces
//            content = Regex.Replace(content, @"\(\s*\)", "()");
//            content = Regex.Replace(content, @"\[\s*\]", "[]");
//            content = Regex.Replace(content, @"\{\s*\}", "{}");

//            return content;
//        }

//        private string CompressMarkdown(string content)
//        {
//            // Simplify markdown formatting
//            content = content.Replace("```csharp", "```cs");
//            content = content.Replace("```javascript", "```js");
//            content = content.Replace("```typescript", "```ts");
//            content = content.Replace("```python", "```py");
//            content = content.Replace("```dockerfile", "```docker");
//            content = content.Replace("```yaml", "```yml");

//            // Remove unnecessary markdown
//            content = Regex.Replace(content, @"```(\w+)?\n", "```$1:");
//            content = content.Replace("```\n", "```");

//            // Compact tables
//            content = Regex.Replace(content, @"\|\s+", "|");
//            content = Regex.Replace(content, @"\s+\|", "|");

//            // Simplify emphasis
//            content = content.Replace("**", "*");
//            content = content.Replace("__", "_");

//            return content;
//        }

//        private string DeduplicatePatterns(string content)
//        {
//            var lines = content.Split('\n');
//            var uniqueLines = new List<string>();
//            var recentLines = new Queue<string>(capacity: 5);

//            foreach (var line in lines)
//            {
//                var trimmedLine = line.Trim();

//                // Skip if this line was very recently added (within last 5 lines)
//                if (!recentLines.Contains(trimmedLine) || string.IsNullOrWhiteSpace(trimmedLine))
//                {
//                    uniqueLines.Add(line);

//                    if (recentLines.Count >= 5)
//                        recentLines.Dequeue();
//                    recentLines.Enqueue(trimmedLine);
//                }
//            }

//            // Also remove patterns that repeat frequently
//            content = string.Join("\n", uniqueLines);

//            // Find and compress repeated patterns
//            var patternMatches = new Dictionary<string, int>();
//            var patternRegex = new Regex(@"^(.{10,100})$", RegexOptions.Multiline);

//            foreach (Match match in patternRegex.Matches(content))
//            {
//                var pattern = match.Groups[1].Value;
//                if (!patternMatches.ContainsKey(pattern))
//                    patternMatches[pattern] = 0;
//                patternMatches[pattern]++;
//            }

//            // Replace frequently repeated patterns with abbreviations
//            foreach (var kvp in patternMatches.Where(x => x.Value > 3).OrderByDescending(x => x.Key.Length))
//            {
//                var abbreviation = GenerateAbbreviation(kvp.Key);
//                if (abbreviation.Length < kvp.Key.Length / 2)
//                {
//                    _abbreviationCache[kvp.Key] = abbreviation;
//                    // Keep first occurrence, replace subsequent ones
//                    var first = true;
//                    content = Regex.Replace(content,
//                        Regex.Escape(kvp.Key),
//                        m => first ? (first = false, kvp.Key).Item2 : abbreviation);
//                }
//            }

//            return content;
//        }

//        private string ApplyContextualAbbreviations(string content)
//        {
//            // Apply smart abbreviations based on context
//            var contextualAbbreviations = new Dictionary<string, string>
//            {
//                // Domain-specific terms (detect and abbreviate)
//                { @"\bApplication\.Services\.(\w+)", "AS.$1" },
//                { @"\bDomain\.Entities\.(\w+)", "DE.$1" },
//                { @"\bInfrastructure\.Data\.(\w+)", "ID.$1" },
//                { @"\bPresentation\.Controllers\.(\w+)", "PC.$1" },
//                { @"\bFeatures\.(\w+)\.(\w+)", "F.$1.$2" },
//                { @"\bModules\.(\w+)\.(\w+)", "M.$1.$2" },
//                { @"\bHandlers\.(\w+)", "H.$1" },
//                { @"\bValidators\.(\w+)", "V.$1" },
//                { @"\bRepositories\.(\w+)", "R.$1" },
//                { @"\bServices\.(\w+)", "S.$1" },
//                { @"\bFactories\.(\w+)", "Fac.$1" },
//                { @"\bBuilders\.(\w+)", "B.$1" },
//                { @"\bMappers\.(\w+)", "Map.$1" },
//                { @"\bHelpers\.(\w+)", "Hlp.$1" },
//                { @"\bUtilities\.(\w+)", "Util.$1" },
//                { @"\bExtensions\.(\w+)", "Ext.$1" },
//                { @"\bExceptions\.(\w+)", "Ex.$1" },
//                { @"\bInterfaces\.I(\w+)", "I.$1" },
//                { @"\bAbstractions\.(\w+)", "Abs.$1" },
//                { @"\bImplementations\.(\w+)", "Impl.$1" },
//                { @"\bConfigurations\.(\w+)", "Cfg.$1" },
//                { @"\bMiddlewares\.(\w+)", "MW.$1" },
//                { @"\bFilters\.(\w+)", "Flt.$1" },
//                { @"\bAttributes\.(\w+)", "Attr.$1" }
//            };

//            foreach (var kvp in contextualAbbreviations)
//            {
//                content = Regex.Replace(content, kvp.Key, kvp.Value);
//            }

//            return content;
//        }

//        private string CompressNumbers(string content)
//        {
//            if (!_options.CompressNumbers)
//                return content;

//            // Convert large numbers to K/M/B notation
//            content = Regex.Replace(content, @"\b(\d{1,3})000000000\b", "$1B");
//            content = Regex.Replace(content, @"\b(\d{1,3})000000\b", "$1M");
//            content = Regex.Replace(content, @"\b(\
