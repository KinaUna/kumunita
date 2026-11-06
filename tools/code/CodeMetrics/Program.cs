// CodeMetrics — the IMPROVE-CODE lane's code-metrics tool (C00).
//
// Modes:
//   measure   CodeMetrics <root> [output-dir]
//   check     CodeMetrics --check <baseline.json> <root> [output-dir]
//   tighten   CodeMetrics --tighten <baseline.json> <root> [output-dir]
//   report    CodeMetrics --report <root> [output-dir]
//
// Approach for the boundary pass (D6): a SYNTAX-ONLY heuristic (documented
// choice, per the C00 plan note "pick the approach that works"). A full
// semantic compilation of 500+ files in a console tool is heavy; instead we
// (1) collect every type definition under src/ into a name -> defs map,
// (2) for each file collect the set of C# identifiers it contains, (3) an
// edge A -> B/symbol exists when A's identifier set contains a simple name
// defined in module B (B != A). An edge is a VIOLATION unless the target is
// an interface, a record, an enum, or a static class whose name ends in
// Policy / Keys / Kinds.
//
// Cyclomatic complexity: 1 + count of { if, for, foreach, while, do, switch,
// &&, ||, ternary ? } decision tokens inside the method's token span.
// switch counts once; case labels do not count.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeMetrics;

public static class Program
{
    private const int CsFileCeiling = 600;
    private const int MethodLinesCeiling = 80;
    private const int MethodCcCeiling = 15;
    private const int ClassPublicMethodsCeiling = 25;
    private const int InterfaceMethodsCeiling = 20;
    private const int CshtmlCeiling = 600;
    private const int TsFileCeiling = 600;
    private const int TsFunctionLinesCeiling = 80;
    private const int TsFunctionCcCeiling = 15;

    private static readonly HashSet<string> CsKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class",
        "const","continue","decimal","default","delegate","do","double","else","enum","event",
        "explicit","extern","false","finally","fixed","float","for","foreach","goto","if",
        "implicit","in","int","interface","internal","is","lock","long","namespace","new",
        "null","object","operator","out","override","params","private","protected","public",
        "readonly","record","ref","return","sbyte","sealed","short","sizeof","stackalloc",
        "static","string","struct","switch","this","throw","true","try","typeof","uint",
        "ulong","unchecked","unsafe","ushort","using","var","virtual","void","volatile","while",
        "add","alias","and","ascending","async","await","by","descending","dynamic","equals",
        "from","global","group","into","let","nameof","not","notnull","on","or","orderby",
        "partial","remove","select","set","when","where","with","yield","file"
    };

    public static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex) { Console.Error.WriteLine("CodeMetrics error: " + ex.Message); return 2; }
    }

    private static int Run(string[] args)
    {
        string mode = "measure";
        string? baselinePath = null;
        string? root = null;
        string? outDir = null;
        string? tsScript = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--check": mode = "check"; if (i + 1 < args.Length) { baselinePath = args[++i]; } break;
                case "--tighten": mode = "tighten"; if (i + 1 < args.Length) { baselinePath = args[++i]; } break;
                case "--report": mode = "report"; break;
                case "--ts-script": if (i + 1 < args.Length) { tsScript = args[++i]; } break;
                default:
                    if (root is null) { root = args[i]; }
                    else if (outDir is null) { outDir = args[i]; }
                    else { Console.Error.WriteLine("unexpected argument: " + args[i]); return 2; }
                    break;
            }
        }
        if (root is null)
        {
            Console.Error.WriteLine("usage: CodeMetrics <root> [output-dir] | --check <baseline.json> <root> | --tighten <baseline.json> <root> | --report <root>");
            return 2;
        }
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) { Console.Error.WriteLine("root not found: " + root); return 2; }
        outDir ??= Path.Combine(root, ".tmp", "metrics");
        outDir = Path.GetFullPath(outDir);

        if (tsScript is null)
        {
            var candidate = Path.Combine(root, "tools", "code", "ts-metrics.js");
            if (File.Exists(candidate)) { tsScript = candidate; }
        }

        var data = MeasureCSharp(root);
        var views = MeasureViews(root);

        bool needTs = true; // every mode measures TS (measure writes it, the rest read it)
        var ts = new TsData();
        if (needTs)
        {
            string tsCsv = Path.Combine(outDir, "ts.csv");
            if (tsScript is not null && File.Exists(tsScript))
            {
                Directory.CreateDirectory(outDir);
                int code = RunProcess("node", new[] { tsScript, root, outDir });
                if (code != 0) { Console.Error.WriteLine("ts-metrics failed (exit " + code + ")"); return code; }
            }
            if (File.Exists(tsCsv)) { ts = TsData.Parse(tsCsv); }
            else { Console.Error.WriteLine("warning: no ts.csv available — TS checks skipped"); }
        }

        switch (mode)
        {
            case "measure":
                Directory.CreateDirectory(outDir);
                WriteCsv(Path.Combine(outDir, "files.csv"), data.Files);
                WriteCsv(Path.Combine(outDir, "methods.csv"), data.Methods);
                WriteCsv(Path.Combine(outDir, "types.csv"), data.Types);
                WriteCsv(Path.Combine(outDir, "boundaries.csv"), data.ViolationEdges);
                WriteCsv(Path.Combine(outDir, "clones.csv"), data.CloneRegions);
                if (ts.Files.Count > 0 || ts.Functions.Count > 0) { ts.Write(Path.Combine(outDir, "ts.csv")); }
                Console.WriteLine($"CodeMetrics: wrote {outDir} (files={data.Files.Count} methods={data.Methods.Count} types={data.Types.Count} edges={data.ViolationEdges.Count} clones={data.CloneRegions.Count})");
                return 0;

            case "check":
                if (baselinePath is null) { Console.Error.WriteLine("--check requires <baseline.json>"); return 2; }
                baselinePath = Path.GetFullPath(baselinePath);
                if (!File.Exists(baselinePath)) { Console.Error.WriteLine("baseline not found: " + baselinePath); return 2; }
                var violations = CheckAgainstBaseline(File.ReadAllText(baselinePath), data, views, ts, root);
                if (violations.Count == 0) { Console.WriteLine("code-check: OK (0 violations)"); return 0; }
                Console.WriteLine("code-check: " + violations.Count + " violation(s)");
                foreach (var v in violations) { Console.WriteLine("  - " + v); }
                return 1;

            case "tighten":
                if (baselinePath is null) { Console.Error.WriteLine("--tighten requires <baseline.json>"); return 2; }
                baselinePath = Path.GetFullPath(baselinePath);
                if (!File.Exists(baselinePath)) { Console.Error.WriteLine("baseline not found: " + baselinePath); return 2; }
                string tightened = TightenBaseline(File.ReadAllText(baselinePath), data, views, ts);
                File.WriteAllText(baselinePath, tightened);
                Console.WriteLine("code-tighten: rewrote " + baselinePath);
                return 0;

            case "report":
                PrintReport(data, views, ts, root);
                return 0;
        }
        return 2;
    }

    // ============================ measurement ================================

    private static CSharpData MeasureCSharp(string root)
    {
        string src = Path.Combine(root, "src");
        var csFiles = new List<string>();
        if (Directory.Exists(src))
        {
            csFiles = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("/bin/") && !p.Contains("/obj/") && !p.Contains("/node_modules/"))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        var files = new List<FileRecord>();
        var methods = new List<MethodRecord>();
        var types = new List<TypeRecord>();
        var classes = new List<ClassCountRecord>();
        var knownTypes = new Dictionary<string, List<TypeDef>>(StringComparer.Ordinal);
        var fileIdentifiers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var parseResults = new List<ParseResult>();

        foreach (var path in csFiles)
        {
            string code;
            try { code = File.ReadAllText(path); }
            catch { continue; }

            var tree = CSharpSyntaxTree.ParseText(code, path: path, options: new CSharpParseOptions(LanguageVersion.Preview));
            var rootNode = tree.GetRoot();
            var errDiags = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errDiags.Count > 0)
            {
                files.Add(MakeFileRecord(root, path, code, null));
                continue;
            }

            var (tokens, codeLines) = CollectTokens(rootNode);
            files.Add(MakeFileRecord(root, path, code, codeLines));

            foreach (var md in rootNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                methods.Add(new MethodRecord
                {
                    File = Rel(root, path),
                    Type = FullyQualified(md),
                    Member = MethodId(md),
                    Lines = CountCodeLines(rootNode, codeLines, md.Span),
                    Cc = Cyclomatic(rootNode, tokens, md.Span)
                });
            }

            foreach (var td in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                string fq = FullyQualified(td);
                string name = td.Identifier.Text;
                bool isInterface = td is InterfaceDeclarationSyntax;
                bool isClass = td is ClassDeclarationSyntax;
                if ((isClass || isInterface) && td is TypeDeclarationSyntax tds)
                {
                    int count = 0;
                    foreach (var m in tds.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (isInterface) { count++; continue; }
                        if (m.Modifiers.Any(mo => mo.IsKind(SyntaxKind.PublicKeyword))) { count++; }
                    }
                    if (isInterface) { types.Add(new TypeRecord { File = Rel(root, path), Type = fq, Kind = "interface", PublicMethods = count }); }
                    else { types.Add(new TypeRecord { File = Rel(root, path), Type = fq, Kind = "class", PublicMethods = count }); }
                    if (isClass && count > 0) { classes.Add(new ClassCountRecord { File = Rel(root, path), Type = fq, PublicMethods = count }); }
                }
                if (name.Length > 0)
                {
                    if (!knownTypes.TryGetValue(name, out var list)) { list = new List<TypeDef>(); knownTypes[name] = list; }
                    list.Add(new TypeDef
                    {
                        Name = name,
                        File = Rel(root, path),
                        Module = ModuleOf(root, path),
                        Kind = td is InterfaceDeclarationSyntax ? "interface"
                              : td is RecordDeclarationSyntax ? "record"
                              : td is EnumDeclarationSyntax ? "enum"
                              : td is ClassDeclarationSyntax ? "class"
                              : td is StructDeclarationSyntax ? "struct" : "other",
                        IsStatic = td is ClassDeclarationSyntax c && c.Modifiers.Any(mo => mo.IsKind(SyntaxKind.StaticKeyword)),
                        Fq = fq
                    });
                }
            }

            var ownNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var td in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()) { ownNames.Add(td.Identifier.Text); }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in tokens)
            {
                if (t.IsKind(SyntaxKind.IdentifierToken) && !CsKeywords.Contains(t.Text) && !ownNames.Contains(t.Text)) { ids.Add(t.Text); }
            }
            fileIdentifiers[Rel(root, path)] = ids;
            parseResults.Add(new ParseResult(path, rootNode, tokens, codeLines));
        }

        // boundaries (D6)
        var violationEdges = new List<EdgeRecord>();
        foreach (var (file, ids) in fileIdentifiers.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string? from = ModuleOf(root, file.Replace('/', Path.DirectorySeparatorChar));
            if (from is null) { continue; }
            var edgesFrom = new Dictionary<(string to, string symbol), HashSet<string>>();
            foreach (var id in ids)
            {
                if (!knownTypes.TryGetValue(id, out var defs)) { continue; }
                foreach (var def in defs)
                {
                    if (def.Module is null || def.Module == from) { continue; }
                    if (IsAllowedBoundaryTarget(def)) { continue; }
                    var key = (def.Module, id);
                    if (!edgesFrom.TryGetValue(key, out var set)) { set = new HashSet<string>(); edgesFrom[key] = set; }
                    set.Add(file);
                }
            }
            foreach (var (key, set) in edgesFrom.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2))
            {
                violationEdges.Add(new EdgeRecord { From = from, To = key.Item1, Symbol = key.Item2, Files = string.Join(";", set.OrderBy(s => s, StringComparer.Ordinal)) });
            }
        }

        var controllers = ControllersWithMarten(root);
        var clones = DetectClones(root, parseResults);

        return new CSharpData(files, methods, types, classes, violationEdges, controllers, clones);
    }

    private sealed class ParseResult
    {
        public ParseResult(string file, SyntaxNode root, List<SyntaxToken> tokens, HashSet<int> codeLines) { File = file; Root = root; Tokens = tokens; CodeLines = codeLines; }
        public string File { get; }
        public SyntaxNode Root { get; }
        public List<SyntaxToken> Tokens { get; }
        public HashSet<int> CodeLines { get; }
    }

    private static FileRecord MakeFileRecord(string root, string path, string code, HashSet<int>? codeLines)
    {
        string[] lines = code.Replace("\r\n", "\n").Split('\n');
        int n = lines.Length;
        int codeCount = codeLines is null ? 0 : codeLines.Count;
        if (codeLines is null) { codeCount = lines.Count(l => l.Trim().Length > 0); }
        int comment = 0;
        for (int i = 0; i < n; i++)
        {
            if (codeLines is not null && codeLines.Contains(i)) { continue; }
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("/*") || t.StartsWith("*")) { comment++; }
        }
        int blank = n - codeCount - comment;
        if (blank < 0) { blank = 0; }
        return new FileRecord { File = Rel(root, path), CodeLines = codeCount, CommentLines = comment, BlankLines = blank, Physical = n };
    }

    private static (List<SyntaxToken> tokens, HashSet<int> codeLines) CollectTokens(SyntaxNode root)
    {
        var tokens = new List<SyntaxToken>();
        var codeLines = new HashSet<int>();
        foreach (var t in root.DescendantTokens(descendIntoTrivia: false))
        {
            tokens.Add(t);
            codeLines.Add(TokenLine(root, t.Span));
        }
        return (tokens, codeLines);
    }

    private static int TokenLine(SyntaxNode root, TextSpan span)
    {
        var tree = root.SyntaxTree;
        return tree.GetLineSpan(span).StartLinePosition.Line;
    }

    private static int CountCodeLines(SyntaxNode root, HashSet<int> codeLines, TextSpan span)
    {
        int lo = TokenLine(root, new TextSpan(span.Start, 0));
        int hi = TokenLine(root, new TextSpan(span.End, 0));
        int count = 0;
        for (int i = lo; i <= hi; i++) { if (codeLines.Contains(i)) { count++; } }
        return count;
    }

    private static int Cyclomatic(SyntaxNode root, List<SyntaxToken> tokens, TextSpan span)
    {
        int cc = 1;
        foreach (var t in tokens)
        {
            if (!t.Span.OverlapsWith(span)) { continue; }
            switch (t.Kind())
            {
                case SyntaxKind.IfKeyword:
                case SyntaxKind.ForKeyword:
                case SyntaxKind.ForEachKeyword:
                case SyntaxKind.WhileKeyword:
                case SyntaxKind.DoKeyword:
                case SyntaxKind.SwitchKeyword:
                case SyntaxKind.AmpersandAmpersandToken:
                case SyntaxKind.BarBarToken:
                case SyntaxKind.QuestionToken:
                    cc++;
                    break;
            }
        }
        return cc;
    }

    private static string FullyQualified(SyntaxNode node)
    {
        var parts = new List<string>();
        SyntaxNode? n = node;
        while (n is not null)
        {
            if (n is BaseNamespaceDeclarationSyntax ns && !string.IsNullOrEmpty(ns.Name.ToString())) { parts.Insert(0, ns.Name.ToString()); break; }
            if (n is TypeDeclarationSyntax td) { parts.Insert(0, td.Identifier.Text); }
            n = n.Parent;
        }
        return string.Join(".", parts);
    }

    private static string MethodId(MethodDeclarationSyntax md)
    {
        string name = md.Identifier.Text;
        var arities = new List<string>();
        foreach (var p in md.ParameterList.Parameters) { arities.Add(p.Type?.ToString() ?? "?"); }
        return name + "(" + string.Join(",", arities) + ")";
    }

    // ============================ module map ================================

    private static string? ModuleOf(string root, string file)
    {
        string rel = Rel(root, file).Replace('\\', '/');
        if (rel.StartsWith("src/Kumunita.Core/", StringComparison.Ordinal))
        {
            var rest = rel["src/Kumunita.Core/".Length..];
            int slash = rest.IndexOf('/');
            if (slash <= 0) { return null; }
            return rest[..slash];
        }
        if (rel.StartsWith("src/Kumunita.Web/", StringComparison.Ordinal)) { return "Web"; }
        return null;
    }

    private static bool IsAllowedBoundaryTarget(TypeDef def)
    {
        if (def.Kind is "interface" or "record" or "enum") { return true; }
        if (def.Kind == "class" && def.IsStatic && (def.Name.EndsWith("Policy", StringComparison.Ordinal)
            || def.Name.EndsWith("Keys", StringComparison.Ordinal)
            || def.Name.EndsWith("Kinds", StringComparison.Ordinal))) { return true; }
        return false;
    }

    private static List<string> ControllersWithMarten(string root)
    {
        string dir = Path.Combine(root, "src", "Kumunita.Web", "Controllers");
        var result = new List<string>();
        if (!Directory.Exists(dir)) { return result; }
        foreach (var p in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string code;
            try { code = File.ReadAllText(p); } catch { continue; }
            if (code.Contains("IDocumentStore") || code.Contains("IDocumentSession") || code.Contains("IQuerySession")) { result.Add(Rel(root, p)); }
        }
        return result;
    }

    // ============================ views + ts ================================

    private static List<ViewRecord> MeasureViews(string root)
    {
        string src = Path.Combine(root, "src");
        var result = new List<ViewRecord>();
        if (!Directory.Exists(src)) { return result; }
        foreach (var p in Directory.EnumerateFiles(src, "*.cshtml", SearchOption.AllDirectories)
            .Where(p => !p.Contains("/bin/") && !p.Contains("/obj/"))
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            int n = 0;
            using var r = new StreamReader(p);
            while (r.ReadLine() is not null) { n++; }
            result.Add(new ViewRecord { File = Rel(root, p), Physical = n });
        }
        return result;
    }

    // ============================ clone detection ===========================

    private const int CloneWindow = 100;
    private const int CloneMinLines = 10;

    private static List<CloneRegion> DetectClones(string root, List<ParseResult> parseResults)
    {
        var byHash = new Dictionary<ulong, List<(string file, int startIdx, int endIdx)>>();
        var fileTokenLines = new Dictionary<string, int[]>(StringComparer.Ordinal);

        foreach (var pr in parseResults)
        {
            if (pr.Tokens.Count < CloneWindow) { continue; }
            var rel = Rel(root, pr.File);
            var lineOf = new int[pr.Tokens.Count];
            for (int i = 0; i < pr.Tokens.Count; i++) { lineOf[i] = TokenLine(pr.Root, pr.Tokens[i].Span); }
            fileTokenLines[rel] = lineOf;

            for (int i = 0; i + CloneWindow <= pr.Tokens.Count; i++)
            {
                ulong h = FnvHash(i, pr.Tokens);
                if (!byHash.TryGetValue(h, out var list)) { list = new List<(string, int, int)>(); byHash[h] = list; }
                list.Add((rel, i, i + CloneWindow));
            }
        }

        var regions = new List<CloneRegion>();
        foreach (var (h, list) in byHash)
        {
            var fileSets = list.GroupBy(x => x.file).ToDictionary(g => g.Key, g => g.ToList());
            if (fileSets.Count < 2) { continue; }
            foreach (var (f, wins) in fileSets)
            {
                var sorted = wins.OrderBy(w => w.Item2).ToList();
                var merged = new List<(int lo, int hi)>();
                foreach (var w in sorted)
                {
                    if (merged.Count > 0 && w.Item2 <= merged[^1].hi) { merged[^1] = (merged[^1].lo, Math.Max(merged[^1].hi, w.Item3)); }
                    else { merged.Add((w.Item2, w.Item3)); }
                }
                foreach (var (lo, hi) in merged)
                {
                    int minLine = int.MaxValue, maxLine = int.MinValue;
                    if (fileTokenLines.TryGetValue(f, out var tl))
                    {
                        for (int i = lo; i < hi && i < tl.Length; i++)
                        {
                            minLine = Math.Min(minLine, tl[i]);
                            maxLine = Math.Max(maxLine, tl[i]);
                        }
                    }
                    if (maxLine - minLine + 1 < CloneMinLines) { continue; }
                    var others = fileSets.Keys.Where(x => x != f).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    if (others.Count < 1) { continue; }
                    regions.Add(new CloneRegion { StartFile = f, StartLine = minLine + 1, EndLine = maxLine + 1, TokenCount = hi - lo, CloneFiles = string.Join(";", others) });
                }
            }
        }
        return regions.OrderBy(r => r.StartFile, StringComparer.Ordinal).ThenBy(r => r.StartLine).ToList();
    }

    private static ulong FnvHash(int start, List<SyntaxToken> tokens)
    {
        const ulong prime = 1099511628211UL;
        ulong h = 14695981039346656037UL;
        for (int i = start; i < start + CloneWindow; i++)
        {
            string s = tokens[i].Text;
            if (tokens[i].IsKind(SyntaxKind.StringLiteralToken)
                || tokens[i].IsKind(SyntaxKind.NumericLiteralToken)
                || tokens[i].IsKind(SyntaxKind.CharacterLiteralToken)) { s = "<lit>"; }
            foreach (char c in s) { h = (h ^ c) * prime; }
            h = (h ^ 0x7f) * prime;
        }
        return h;
    }

    // ============================ baseline: check ===========================

    private static List<string> CheckAgainstBaseline(string baselineJson, CSharpData data, List<ViewRecord> views, TsData ts, string root)
    {
        var failures = new List<string>();
        var bs = ParseBaseline(baselineJson);

        failures.AddRange(CheckSimpleList(bs.Files, data.Files, r => r.CodeLines, r => r.File, CsFileCeiling, "file"));
        failures.AddRange(CheckSimpleList(bs.Views, views, r => r.Physical, r => r.File, CshtmlCeiling, "view"));
        failures.AddRange(CheckSimpleList(bs.Ts, ts.Files, r => r.CodeLines, r => r.File, TsFileCeiling, "ts file"));
        failures.AddRange(CheckSimpleList(bs.Classes, data.Classes, r => r.PublicMethods, r => r.File + "::" + r.Type, ClassPublicMethodsCeiling, "class public methods"));
        var ifaces = data.Types.Where(t => t.Kind == "interface").ToList();
        failures.AddRange(CheckSimpleList(bs.Interfaces, ifaces, r => r.PublicMethods, r => r.File + "::" + r.Type, InterfaceMethodsCeiling, "interface methods"));

        // methods (lines + cc)
        var methodMap = data.Methods.ToDictionary(m => m.File + "::" + m.Type + "." + m.Member, m => m, StringComparer.Ordinal);
        var methodBaseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in bs.Methods)
        {
            string id = entry?["id"]?.ToString() ?? "";
            methodBaseIds.Add(id);
            int maxLines = JsonInt(entry?["max"]);
            int maxCc = JsonInt(entry?["cc_max"]);
            if (!methodMap.TryGetValue(id, out var m))
            {
                failures.Add("stale method baseline (method gone): " + id);
                continue;
            }
            if (m.Lines <= MethodLinesCeiling && m.Cc <= MethodCcCeiling) { failures.Add("stale method baseline (now under ceiling): " + id); continue; }
            if (m.Lines > maxLines) { failures.Add("method grew past baseline max lines: " + id + $" (now {m.Lines}, max {maxLines})"); }
            if (m.Cc > maxCc) { failures.Add("method grew past baseline max cc: " + id + $" (now {m.Cc}, max {maxCc})"); }
        }
        foreach (var m in data.Methods)
        {
            string id = m.File + "::" + m.Type + "." + m.Member;
            if (methodBaseIds.Contains(id)) { continue; }
            if (m.Lines > MethodLinesCeiling) { failures.Add($"NEW method over line ceiling: {id} (lines={m.Lines}, ceiling {MethodLinesCeiling})"); }
            if (m.Cc > MethodCcCeiling) { failures.Add($"NEW method over CC ceiling: {id} (cc={m.Cc}, ceiling {MethodCcCeiling})"); }
        }

        // ts functions
        var tsFnMap = ts.Functions.ToDictionary(f => f.File + "::" + f.Name, f => f, StringComparer.Ordinal);
        var tsFnBaseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in bs.TsFunctions)
        {
            string id = entry?["id"]?.ToString() ?? "";
            tsFnBaseIds.Add(id);
            int maxLines = JsonInt(entry?["max"]);
            int maxCc = JsonInt(entry?["cc_max"]);
            if (!tsFnMap.TryGetValue(id, out var f))
            {
                failures.Add("stale ts-function baseline (gone): " + id);
                continue;
            }
            if (f.Lines <= TsFunctionLinesCeiling && f.Cc <= TsFunctionCcCeiling) { failures.Add("stale ts-function baseline (now under ceiling): " + id); continue; }
            if (f.Lines > maxLines) { failures.Add($"ts function grew past baseline max lines: {id} (now {f.Lines}, max {maxLines})"); }
            if (f.Cc > maxCc) { failures.Add($"ts function grew past baseline max cc: {id} (now {f.Cc}, max {maxCc})"); }
        }
        foreach (var f in ts.Functions)
        {
            string id = f.File + "::" + f.Name;
            if (tsFnBaseIds.Contains(id)) { continue; }
            if (f.Lines > TsFunctionLinesCeiling) { failures.Add($"NEW ts function over line ceiling: {id} (lines={f.Lines})"); }
            if (f.Cc > TsFunctionCcCeiling) { failures.Add($"NEW ts function over CC ceiling: {id} (cc={f.Cc})"); }
        }

        // boundary violations
        var edgeKey = (EdgeRecord e) => e.From + "->" + e.To + ":" + e.Symbol;
        var edgeFiles = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in data.ViolationEdges)
        {
            var k = edgeKey(e);
            edgeFiles[k] = edgeFiles.TryGetValue(k, out var c) ? c + e.Files.Split(';').Length : e.Files.Split(';').Length;
        }
        var baseEdgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in bs.BoundaryViolations)
        {
            string from = entry?["from"]?.ToString() ?? "";
            string to = entry?["to"]?.ToString() ?? "";
            string symbol = entry?["symbol"]?.ToString() ?? "";
            int max = JsonInt(entry?["count"]);
            string k = from + "->" + to + ":" + symbol;
            baseEdgeKeys.Add(k);
            if (!edgeFiles.ContainsKey(k)) { failures.Add("stale boundary baseline (edge gone): " + k); continue; }
            if (edgeFiles[k] > max) { failures.Add($"boundary edge grew: {k} (now {edgeFiles[k]} files, max {max})"); }
        }
        foreach (var k in edgeFiles.Keys)
        {
            if (baseEdgeKeys.Contains(k)) { continue; }
            failures.Add($"NEW boundary violation: {k} ({edgeFiles[k]} files)");
        }

        // controllers on Marten
        var allowlistFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in bs.ControllersAllowlist)
        {
            string file = entry?["file"]?.ToString() ?? "";
            allowlistFiles.Add(file);
            if (!data.Controllers.Contains(file)) { failures.Add("stale controllers-marten-allowlist entry: " + file); }
        }
        foreach (var file in data.Controllers)
        {
            if (allowlistFiles.Contains(file)) { continue; }
            failures.Add("controller on Marten without an allowlist entry: " + file);
        }

        // rule 4: max must not be higher than on the base ref (default
        // origin/development — the branch this repo develops on, where the
        // baseline file lives) without adr:. See CheckOriginMain for the
        // CI fail-closed behaviour (an unreadable base fails the gate under
        // CI instead of silently skipping it).
        failures.AddRange(CheckOriginMain(bs, root));

        // seam-test gate (C12 — promoted from report to gate)
        if (bs.SeamTestMode == "gate")
        {
            failures.AddRange(CheckSeamTest(data, root, bs.ServicesUnitFloor));
        }
        return failures;
    }

    private static List<string> CheckSimpleList<TItem>(List<JsonNode?> baseItems, List<TItem> items,
        Func<TItem, int> value, Func<TItem, string> idOf, int ceiling, string label)
    {
        var failures = new List<string>();
        var baseMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in baseItems) { if (e is null) { continue; } baseMap[e["id"]?.ToString() ?? ""] = JsonInt(e["max"]); }
        var baseIds = new HashSet<string>(baseMap.Keys, StringComparer.Ordinal);
        foreach (var item in items)
        {
            string iid = idOf(item);
            int v = value(item);
            if (baseMap.TryGetValue(iid, out int max))
            {
                if (v <= ceiling) { failures.Add($"stale {label} baseline (now under ceiling): {iid}"); continue; }
                if (v > max) { failures.Add($"grew past baseline: {iid} (now {v}, max {max})"); }
            }
            else if (v > ceiling)
            {
                failures.Add($"NEW {label} over ceiling: {iid} ({v} > {ceiling})");
            }
        }
        return failures;
    }

    private static List<string> CheckOriginMain(ParsedBaseline bs, string root)
    {
        var failures = new List<string>();

        // The base ref to compare against. Default is origin/development: this
        // repo develops on `development` (the baseline file lives only on that
        // branch, not on main/release), and the "max raised without an `adr:`"
        // rule means "raised relative to the branch a PR is landing into".
        // Overridable via CODE_BASELINE_BASE (CI sets it to the PR base).
        string baseRef = Environment.GetEnvironmentVariable("CODE_BASELINE_BASE")?.Trim();
        if (string.IsNullOrWhiteSpace(baseRef)) { baseRef = "origin/development"; }
        string basePath = baseRef + ":tools/code/code-baseline.json";

        // Under CI (the real gate) an unreadable base is a gate FAILURE, not a
        // skip: a ratchet that switches itself off when it cannot read its
        // base is worse than one that fails (that is how the C09 raises slipped
        // through — the base was origin/main, which does not have the file, so
        // the check silently skipped). Locally (no CI= set) a missing base is a
        // loud warning but does not fail the developer's laptop.
        bool underCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        string? oldJson = null;
        string? readError = null;
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(root);
        psi.ArgumentList.Add("show");
        psi.ArgumentList.Add(basePath);
        try
        {
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("git start failed");
            string output = proc.StandardOutput.ReadToEnd();
            string errOut = proc.StandardError.ReadToEnd();
            proc.WaitForExit(30_000);
            if (proc.ExitCode != 0) { readError = errOut.Trim(); }
            else { oldJson = output; }
        }
        catch (Exception ex) { readError = ex.Message; }

        if (oldJson is null)
        {
            // Two distinct failure modes, and only one of them is a gate failure:
            //   (a) the base branch never had the file ("exists on disk, but not
            //       in <ref>") — this is the legitimate first introduction of the
            //       baseline, so the comparison is skipped (nothing to compare to).
            //   (b) the base ref itself is unreadable ("invalid object name", or
            //       any other git failure) — in CI that is a plumbing problem the
            //       gate must NOT paper over, so it fails closed.
            bool fileNeverOnBase = readError is not null && readError.Contains("exists on disk, but not in", StringComparison.Ordinal);
            if (fileNeverOnBase)
            {
                Console.Error.WriteLine("note: baseline is new at " + baseRef + " (not present on the base branch) — first-introduction, base-ref comparison skipped (this is expected when a lane lands its baseline for the first time)");
                return failures;
            }
            string msg = $"could not read baseline at {baseRef}: {readError}";
            if (underCi)
            {
                failures.Add($"baseline base unreadable (the `max raised without adr:` check cannot run): {msg}");
            }
            else
            {
                Console.Error.WriteLine("note: " + msg + " — base-ref comparison skipped (would FAIL under CI)");
            }
            return failures;
        }

        ParsedBaseline old;
        try { old = ParseBaseline(oldJson); }
        catch (Exception ex)
        {
            string msg = "could not parse baseline at " + baseRef + ": " + ex.Message;
            if (underCi) { failures.Add(msg); }
            else { Console.Error.WriteLine("note: " + msg + " — base-ref comparison skipped (would FAIL under CI)"); }
            return failures;
        }

        void CheckOne(JsonNode? entry, JsonNode? oldEntry)
        {
            if (entry is null || oldEntry is null) { return; }
            int max = JsonInt(entry["max"]);
            int oldMax = JsonInt(oldEntry["max"]);
            if (max > oldMax && entry["adr"] is null) { failures.Add($"baseline max raised without an `adr` field ({baseRef} was {oldMax}, now {max})"); }
            int ccMax = JsonInt(entry["cc_max"]);
            int oldCcMax = JsonInt(oldEntry["cc_max"]);
            if (ccMax > 0 && ccMax > oldCcMax && entry["adr"] is null) { failures.Add($"baseline cc_max raised without an `adr` field ({baseRef} was {oldCcMax}, now {ccMax})"); }
            int cnt = JsonInt(entry["count"]);
            int oldCnt = JsonInt(oldEntry["count"]);
            if (cnt > 0 && cnt > oldCnt && entry["adr"] is null) { failures.Add($"baseline count raised without an `adr` field ({baseRef} was {oldCnt}, now {cnt})"); }
        }

        void CheckArray(string arrayName, List<JsonNode?> newItems, List<JsonNode?> oldItems, string keyField)
        {
            var oldMap = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            foreach (var e in oldItems) { if (e is null) { continue; } oldMap[e[keyField]?.ToString() ?? ""] = e; }
            foreach (var e in newItems)
            {
                if (e is null) { continue; }
                string id = e[keyField]?.ToString() ?? "";
                if (oldMap.TryGetValue(id, out var oe)) { CheckOne(e, oe); }
            }
        }

        CheckArray("files", bs.Files, old.Files, "id");
        CheckArray("methods", bs.Methods, old.Methods, "id");
        CheckArray("classes", bs.Classes, old.Classes, "id");
        CheckArray("interfaces", bs.Interfaces, old.Interfaces, "id");
        CheckArray("views", bs.Views, old.Views, "id");
        CheckArray("ts", bs.Ts, old.Ts, "id");
        CheckArray("ts_functions", bs.TsFunctions, old.TsFunctions, "id");
        // boundary: key is from->to:symbol
        var oldEdges = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var e in old.BoundaryViolations)
        {
            if (e is null) { continue; }
            string key = (e["from"]?.ToString() ?? "") + "->" + (e["to"]?.ToString() ?? "") + ":" + (e["symbol"]?.ToString() ?? "");
            oldEdges[key] = e;
        }
        foreach (var e in bs.BoundaryViolations)
        {
            if (e is null) { continue; }
            string key = (e["from"]?.ToString() ?? "") + "->" + (e["to"]?.ToString() ?? "") + ":" + (e["symbol"]?.ToString() ?? "");
            if (oldEdges.TryGetValue(key, out var oe) && oe is not null)
            {
                int cnt = JsonInt(e["count"]);
                int oldCnt = JsonInt(oe["count"]);
                if (cnt > oldCnt && e["adr"] is null) { failures.Add($"boundary count raised without an `adr` field ({baseRef} was {oldCnt}, now {cnt}): {key}"); }
            }
        }
        return failures;
    }

    // ============================ baseline: tighten ==========================

    private static string TightenBaseline(string baselineJson, CSharpData data, List<ViewRecord> views, TsData ts)
    {
        var rootNode = JsonNode.Parse(baselineJson) as JsonObject ?? throw new InvalidOperationException("baseline is not a JSON object");

        // Preserve existing `adr` reasons by id (an existing reason is the
        // only way an entry can ever have one — `tighten` never invents a
        // reason, it only drops stale entries and lowers maxes).
        Dictionary<string, string?> AdrBy(string key)
        {
            var map = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (rootNode[key] is JsonArray arr) { foreach (var e in arr) { string? id = e?["id"]?.ToString(); if (id is not null) { map[id] = e?["adr"]?.ToString(); } } }
            return map;
        }

        void TightenArray(string key, List<(string id, int max, int? cc)> current)
        {
            var adr = AdrBy(key);
            var arr = new JsonArray();
            foreach (var (id, max, cc) in current.OrderBy(c => c.id, StringComparer.Ordinal))
            {
                var e = new JsonObject { ["id"] = id, ["max"] = max };
                if (cc is not null) { e["cc_max"] = cc.Value; }
                if (adr.TryGetValue(id, out var a) && a is not null) { e["adr"] = a; }
                arr.Add(e);
            }
            rootNode[key] = arr;
        }

        TightenArray("files", data.Files.Where(f => f.CodeLines > CsFileCeiling).Select(f => (f.File, f.CodeLines, (int?)null)).ToList());
        TightenArray("views", views.Where(v => v.Physical > CshtmlCeiling).Select(v => (v.File, v.Physical, (int?)null)).ToList());
        TightenArray("ts", ts.Files.Where(f => f.CodeLines > TsFileCeiling).Select(f => (f.File, f.CodeLines, (int?)null)).ToList());
        TightenArray("classes", data.Classes.Where(c => c.PublicMethods > ClassPublicMethodsCeiling).Select(c => (c.File + "::" + c.Type, c.PublicMethods, (int?)null)).ToList());
        var ifaces = data.Types.Where(t => t.Kind == "interface").ToList();
        TightenArray("interfaces", ifaces.Where(t => t.PublicMethods > InterfaceMethodsCeiling).Select(t => (t.File + "::" + t.Type, t.PublicMethods, (int?)null)).ToList());
        TightenArray("methods", data.Methods.Where(m => m.Lines > MethodLinesCeiling || m.Cc > MethodCcCeiling).Select(m => (m.File + "::" + m.Type + "." + m.Member, m.Lines, (int?)m.Cc)).ToList());
        TightenArray("ts_functions", ts.Functions.Where(f => f.Lines > TsFunctionLinesCeiling || f.Cc > TsFunctionCcCeiling).Select(f => (f.File + "::" + f.Name, f.Lines, (int?)f.Cc)).ToList());

        var edgeKey = (EdgeRecord e) => e.From + "->" + e.To + ":" + e.Symbol;
        var currentEdges = data.ViolationEdges.GroupBy(edgeKey)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Files.Split(';').Length).Sum(), StringComparer.Ordinal);
        var edgeAdr = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (rootNode["boundary_violations"] is JsonArray edgeArr0) { foreach (var e in edgeArr0) { string k = (e?["from"]?.ToString() ?? "") + "->" + (e?["to"]?.ToString() ?? "") + ":" + (e?["symbol"]?.ToString() ?? ""); edgeAdr[k] = e?["adr"]?.ToString(); } }
        var edgeArr = new JsonArray();
        foreach (var (k, cnt) in currentEdges.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            int arrow = k.IndexOf("->", StringComparison.Ordinal);
            int colon = k.IndexOf(':', arrow + 2);
            var e = new JsonObject { ["from"] = k[..arrow], ["to"] = k[(arrow + 2)..colon], ["symbol"] = k[(colon + 1)..], ["count"] = cnt };
            if (edgeAdr.TryGetValue(k, out var a) && a is not null) { e["adr"] = a; }
            edgeArr.Add(e);
        }
        rootNode["boundary_violations"] = edgeArr;

        // controllers-marten-allowlist: keep existing entries whose file still
        // touches Marten (preserving the reason), and add a new entry for any
        // Marten controller not yet listed (so the initial baseline is green
        // and a new controller crossing in still fails the gate).
        var allowArr = new JsonArray();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        if (rootNode["controllers_marten_allowlist"] is JsonArray existing)
        {
            foreach (var e in existing)
            {
                string file = e?["file"]?.ToString() ?? "";
                if (data.Controllers.Contains(file))
                {
                    // clone the fields — the node still has `existing` as its
                    // parent and cannot be re-parented into a new array
                    var clone = new JsonObject();
                    foreach (var kv in e!.AsObject()) { clone[kv.Key] = kv.Value?.DeepClone(); }
                    allowArr.Add(clone);
                    listed.Add(file);
                }
            }
        }
        foreach (var file in data.Controllers.Where(f => !listed.Contains(f)).OrderBy(f => f, StringComparer.Ordinal))
        {
            allowArr.Add(new JsonObject { ["file"] = file, ["reason"] = "initial baseline (IMPROVE-CODE C00)" });
        }
        rootNode["controllers_marten_allowlist"] = allowArr;

        return rootNode.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    // ============================ baseline: parse ===========================

    private class ParsedBaseline
    {
        public List<JsonNode?> Files = new();
        public List<JsonNode?> Methods = new();
        public List<JsonNode?> Classes = new();
        public List<JsonNode?> Interfaces = new();
        public List<JsonNode?> Views = new();
        public List<JsonNode?> Ts = new();
        public List<JsonNode?> TsFunctions = new();
        public List<JsonNode?> BoundaryViolations = new();
        public List<JsonNode?> ControllersAllowlist = new();
        public string? SeamTestMode;   // "report" or "gate"
        public int ServicesUnitFloor;  // minimum services-with-unit-tested-part (default 10)
    }

    private static ParsedBaseline ParseBaseline(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("baseline is not a JSON object");
        var p = new ParsedBaseline();
        void Fill(string key, List<JsonNode?> list)
        {
            if (root[key] is JsonArray a) { foreach (var e in a) { list.Add(e); } }
        }
        Fill("files", p.Files);
        Fill("methods", p.Methods);
        Fill("classes", p.Classes);
        Fill("interfaces", p.Interfaces);
        Fill("views", p.Views);
        Fill("ts", p.Ts);
        Fill("ts_functions", p.TsFunctions);
        Fill("boundary_violations", p.BoundaryViolations);
        Fill("controllers_marten_allowlist", p.ControllersAllowlist);
        if (root["seam_test"] is JsonObject st)
        {
            p.SeamTestMode = st["mode"]?.ToString() ?? "report";
            p.ServicesUnitFloor = st["services_unit_floor"] is not null ? JsonInt(st["services_unit_floor"]) : 10;
        }
        return p;
    }

    // ============================ report mode ===============================

    private static void PrintReport(CSharpData data, List<ViewRecord> views, TsData ts, string root)
    {
        void Top(string title, List<(string label, int value)> items, int ceiling)
        {
            Console.WriteLine("## " + title + " (ceiling " + ceiling + ")");
            foreach (var (label, value) in items.OrderByDescending(x => x.value).Take(10))
            {
                string state = value > ceiling ? "RED " : "green";
                Console.WriteLine($"  [{state}] {value,7}  {label}");
            }
        }

        Top("C# files (code lines)", data.Files.Select(f => (f.File, f.CodeLines)).ToList(), CsFileCeiling);
        Top("Methods (code lines)", data.Methods.Select(m => (m.File + "::" + m.Type + "." + m.Member, m.Lines)).ToList(), MethodLinesCeiling);
        Top("Methods (cyclomatic)", data.Methods.Select(m => (m.File + "::" + m.Type + "." + m.Member, m.Cc)).ToList(), MethodCcCeiling);
        Top("Classes (public methods)", data.Classes.Select(c => (c.File + "::" + c.Type, c.PublicMethods)).ToList(), ClassPublicMethodsCeiling);
        Top("Interfaces (methods)", data.Types.Where(t => t.Kind == "interface").Select(t => (t.File + "::" + t.Type, t.PublicMethods)).ToList(), InterfaceMethodsCeiling);
        Top("Views (physical lines)", views.Select(v => (v.File, v.Physical)).ToList(), CshtmlCeiling);
        Top("TS files (code lines)", ts.Files.Select(f => (f.File, f.CodeLines)).ToList(), TsFileCeiling);
        Top("TS functions (lines)", ts.Functions.Select(f => (f.File + "::" + f.Name, f.Lines)).ToList(), TsFunctionLinesCeiling);

        var over = data.Files.Where(f => f.CommentLines > f.CodeLines).ToList();
        Console.WriteLine("## comment ratio (comment_lines > code_lines): " + over.Count + " files, " + over.Sum(f => f.CommentLines) + " comment lines total");

        int cloneLines = data.CloneRegions.Sum(r => r.EndLine - r.StartLine + 1);
        Console.WriteLine("## clones: " + data.CloneRegions.Count + " regions, " + cloneLines + " lines (report-only)");

        Console.WriteLine("## boundary violations (D6): " + data.ViolationEdges.Count + " edges");
        foreach (var e in data.ViolationEdges.OrderByDescending(e => e.Files.Split(';').Length).Take(10))
        {
            Console.WriteLine($"  RED {e.From} -> {e.To} : {e.Symbol}  ({e.Files.Split(';').Length} files)");
        }
        Console.WriteLine("## controllers on Marten: " + data.Controllers.Count);
        foreach (var c in data.Controllers) { Console.WriteLine("  - " + c); }
        PrintSeamTest(data, root);
    }

    /// <summary>
    /// C12 seam-test gate: fails when any public *Policy/*Resolver/*Evaluator/*Builder
    /// is not named by a non-Testcontainers test, or when the count of
    /// services-with-unit-tested-part is below the floor.
    /// A "service with a unit-tested part" is a *Service class whose own name or
    /// I-name appears in the unit (non-Testcontainers) test corpus.
    /// </summary>
    private static List<string> CheckSeamTest(CSharpData data, string root, int servicesUnitFloor)
    {
        var failures = new List<string>();
        string testsDir = Path.Combine(root, "tests");
        var allTestFiles = Directory.Exists(testsDir)
            ? Directory.EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories).Where(p => !p.Contains("/bin/") && !p.Contains("/obj/")).ToList()
            : new List<string>();
        string fullCorpus = string.Join("\n", allTestFiles.Select(p => { try { return File.ReadAllText(p); } catch { return ""; } }));

        // Unit corpus: exclude files that use IClassFixture<PostgresFixture>
        var nonTcFiles = allTestFiles.Where(p =>
        {
            try { return !File.ReadAllText(p).Contains("IClassFixture<PostgresFixture>"); }
            catch { return true; }
        }).ToList();
        string unitCorpus = string.Join("\n", nonTcFiles.Select(p => { try { return File.ReadAllText(p); } catch { return ""; } }));

        // Rule 1: every *Policy/*Resolver/*Evaluator/*Builder must be named by a unit test
        string[] suffixes = { "Policy", "Resolver", "Evaluator", "Builder" };
        var policyTypes = data.Types
            .Where(t => t.Kind == "class" && suffixes.Any(s => t.Type.Split('.').Last().EndsWith(s, StringComparison.Ordinal)))
            .ToList();
        foreach (var t in policyTypes)
        {
            string name = t.Type.Split('.').Last();
            if (!unitCorpus.Contains(name))
                failures.Add("seam-test: *Policy/Resolver/Evaluator/Builder not named by a unit test: " + name);
        }

        // Rule 2: every I*Service must be named by any test file
        var ifaces = data.Types
            .Where(t => t.Kind == "interface")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.StartsWith("I", StringComparison.Ordinal) && n.EndsWith("Service", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        foreach (string name in ifaces)
        {
            if (!fullCorpus.Contains(name))
                failures.Add("seam-test: I*Service not named by any test: " + name);
        }

        // Rule 3: services with a unit-tested part >= floor
        var serviceClasses = data.Types
            .Where(t => t.Kind == "class")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.EndsWith("Service", StringComparison.Ordinal) && !n.StartsWith("I", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        int svcCovered = serviceClasses.Count(n => unitCorpus.Contains(n) || unitCorpus.Contains("I" + n));
        if (svcCovered < servicesUnitFloor)
            failures.Add($"seam-test: services-with-unit-tested-part {svcCovered} is below floor {servicesUnitFloor}");

        // Rule 4: every public *Controller must be named by at least one test file
        var controllers = data.Types
            .Where(t => t.Kind == "class")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.EndsWith("Controller", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        foreach (string name in controllers.Where(n => !fullCorpus.Contains(n)))
            failures.Add("seam-test: controller not named by any test: " + name);

        return failures;
    }

    private static void PrintSeamTest(CSharpData data, string root)
    {
        string testsDir = Path.Combine(root, "tests");
        var allTestFiles = Directory.Exists(testsDir)
            ? Directory.EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories).Where(p => !p.Contains("/bin/") && !p.Contains("/obj/")).ToList()
            : new List<string>();
        string fullCorpus = string.Join("\n", allTestFiles.Select(p => { try { return File.ReadAllText(p); } catch { return ""; } }));

        // Unit corpus: exclude files that use IClassFixture<PostgresFixture>
        var nonTcFiles = allTestFiles.Where(p =>
        {
            try { return !File.ReadAllText(p).Contains("IClassFixture<PostgresFixture>"); }
            catch { return true; }
        }).ToList();
        string unitCorpus = string.Join("\n", nonTcFiles.Select(p => { try { return File.ReadAllText(p); } catch { return ""; } }));

        // Rule 1: *Policy/Resolver/Evaluator/Builder in unit corpus
        string[] suffixes = { "Policy", "Resolver", "Evaluator", "Builder" };
        var policyTypes = data.Types
            .Where(t => t.Kind == "class" && suffixes.Any(s => t.Type.Split('.').Last().EndsWith(s, StringComparison.Ordinal)))
            .ToList();
        int polCovered = policyTypes.Count(t => unitCorpus.Contains(t.Type.Split('.').Last()));
        Console.WriteLine("## seam-test: *Policy/Resolver/Evaluator/Builder named by a unit test: " + polCovered + "/" + policyTypes.Count);
        foreach (var t in policyTypes.Where(t => !unitCorpus.Contains(t.Type.Split('.').Last())))
            Console.WriteLine("  RED  " + t.Type.Split('.').Last());

        // Rule 2: I*Service in full test corpus (interface kind — C12 fix)
        var ifaces = data.Types
            .Where(t => t.Kind == "interface")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.StartsWith("I", StringComparison.Ordinal) && n.EndsWith("Service", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        int svcCovered = ifaces.Count(name => fullCorpus.Contains(name));
        Console.WriteLine("## seam-test: I*Service named by a test: " + svcCovered + "/" + ifaces.Count);
        foreach (string name in ifaces.Where(n => !fullCorpus.Contains(n)))
            Console.WriteLine("  RED  " + name);

        // Rule 3: services-with-unit-tested-part
        var serviceClasses = data.Types
            .Where(t => t.Kind == "class")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.EndsWith("Service", StringComparison.Ordinal) && !n.StartsWith("I", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        int svcUnitCovered = serviceClasses.Count(n => unitCorpus.Contains(n) || unitCorpus.Contains("I" + n));
        Console.WriteLine("## seam-test: services-with-unit-tested-part: " + svcUnitCovered + "/" + serviceClasses.Count + " (floor 10)");
        foreach (string n in serviceClasses.Where(n => !unitCorpus.Contains(n) && !unitCorpus.Contains("I" + n)))
            Console.WriteLine("  RED  " + n);

        // Rule 4: every public *Controller named by at least one test file
        var controllers = data.Types
            .Where(t => t.Kind == "class")
            .Select(t => t.Type.Split('.').Last())
            .Where(n => n.EndsWith("Controller", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        int ctrlCovered = controllers.Count(name => fullCorpus.Contains(name));
        Console.WriteLine("## seam-test: *Controller named by a test: " + ctrlCovered + "/" + controllers.Count);
        foreach (string name in controllers.Where(n => !fullCorpus.Contains(n)))
            Console.WriteLine("  RED  " + name);
    }

    // ============================ csv writers ===============================

    private static string CsvField(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static void WriteCsv(string path, List<FileRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("file,code_lines,comment_lines,blank_lines");
        foreach (var r in rows) { sb.AppendLine($"{r.File},{r.CodeLines},{r.CommentLines},{r.BlankLines}"); }
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteCsv(string path, List<MethodRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("file,type,member,code_lines,cyclomatic");
        foreach (var r in rows) { sb.AppendLine($"{r.File},{r.Type},{r.Member},{r.Lines},{r.Cc}"); }
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteCsv(string path, List<TypeRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("file,type,kind,public_methods");
        foreach (var r in rows) { sb.AppendLine($"{r.File},{r.Type},{r.Kind},{r.PublicMethods}"); }
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteCsv(string path, List<EdgeRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("from,to,symbol,files");
        foreach (var r in rows) { sb.AppendLine($"{r.From},{r.To},{r.Symbol},{CsvField(r.Files)}"); }
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteCsv(string path, List<CloneRegion> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("start_file,start_line,end_line,token_count,clone_files");
        foreach (var r in rows) { sb.AppendLine($"{r.StartFile},{r.StartLine},{r.EndLine},{r.TokenCount},{CsvField(r.CloneFiles)}"); }
        File.WriteAllText(path, sb.ToString());
    }

    // ============================ misc =======================================

    private static string Rel(string root, string path)
    {
        string r = root.Replace('\\', '/').TrimEnd('/');
        string p = path.Replace('\\', '/');
        if (p.StartsWith(r + "/", StringComparison.Ordinal)) { return p[(r.Length + 1)..]; }
        return p;
    }

    private static int RunProcess(string file, string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) { psi.ArgumentList.Add(a); }
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("failed to start " + file);
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(300_000);
        if (!string.IsNullOrWhiteSpace(stdout)) { Console.Out.Write(stdout); }
        if (!string.IsNullOrWhiteSpace(stderr)) { Console.Error.Write(stderr); }
        return proc.ExitCode;
    }

    private static int JsonInt(JsonNode? node)
    {
        if (node is null) { return 0; }
        try { return node.GetValue<int>(); }
        catch (InvalidOperationException)
        {
            try { return (int)node.GetValue<long>(); }
            catch (InvalidOperationException)
            {
                if (int.TryParse(node.ToString(), out var parsed)) { return parsed; }
                return 0;
            }
        }
    }

    // ============================ record types ==============================

    private sealed record FileRecord { public string File = ""; public int CodeLines; public int CommentLines; public int BlankLines; public int Physical; }
    private sealed record MethodRecord { public string File = ""; public string Type = ""; public string Member = ""; public int Lines; public int Cc; }
    private sealed record TypeRecord { public string File = ""; public string Type = ""; public string Kind = ""; public int PublicMethods; }
    private sealed record ClassCountRecord { public string File = ""; public string Type = ""; public int PublicMethods; }
    private sealed record EdgeRecord { public string From = ""; public string To = ""; public string Symbol = ""; public string Files = ""; }
    private sealed record CloneRegion { public string StartFile = ""; public int StartLine; public int EndLine; public int TokenCount; public string CloneFiles = ""; }
    private sealed record ViewRecord { public string File = ""; public int Physical; }
    private sealed record TypeDef { public string Name = ""; public string File = ""; public string? Module; public string Kind = ""; public bool IsStatic; public string Fq = ""; }

    private sealed class CSharpData
    {
        public CSharpData(List<FileRecord> files, List<MethodRecord> methods, List<TypeRecord> types,
            List<ClassCountRecord> classes, List<EdgeRecord> edges, List<string> controllers, List<CloneRegion> clones)
        {
            Files = files; Methods = methods; Types = types; Classes = classes;
            ViolationEdges = edges; Controllers = controllers; CloneRegions = clones;
        }
        public List<FileRecord> Files { get; }
        public List<MethodRecord> Methods { get; }
        public List<TypeRecord> Types { get; }
        public List<ClassCountRecord> Classes { get; }
        public List<EdgeRecord> ViolationEdges { get; }
        public List<string> Controllers { get; }
        public List<CloneRegion> CloneRegions { get; }
    }

    private sealed class TsData
    {
        public List<TsFileRecord> Files = new();
        public List<TsFnRecord> Functions = new();

        public sealed class TsFileRecord { public string File = ""; public int CodeLines; }
        public sealed class TsFnRecord { public string File = ""; public string Name = ""; public int Lines; public int Cc; }

        public static TsData Parse(string csvPath)
        {
            var d = new TsData();
            string[] lines = File.ReadAllText(csvPath).Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.Length == 0) { continue; }
                var parts = SplitCsvLine(l);
                if (parts.Length >= 4 && parts[1] == "file") { d.Files.Add(new TsFileRecord { File = parts[0], CodeLines = int.Parse(parts[3]) }); }
                else if (parts.Length >= 5 && parts[1] == "function") { d.Functions.Add(new TsFnRecord { File = parts[0], Name = parts[2], Lines = int.Parse(parts[3]), Cc = int.Parse(parts[4]) }); }
            }
            return d;
        }

        public void Write(string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("file,kind,name,code_lines,cyclomatic");
            foreach (var f in Files) { sb.AppendLine($"{f.File},file,," + f.CodeLines + ",0"); }
            foreach (var f in Functions) { sb.AppendLine($"{f.File},function,{f.Name},{f.Lines},{f.Cc}"); }
            File.WriteAllText(csvPath, sb.ToString());
        }

        private static string[] SplitCsvLine(string line)
        {
            var parts = new List<string>();
            var cur = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else { inQuotes = false; } }
                    else { cur.Append(c); }
                }
                else if (c == '"') { inQuotes = true; }
                else if (c == ',') { parts.Add(cur.ToString()); cur.Clear(); }
                else { cur.Append(c); }
            }
            parts.Add(cur.ToString());
            return parts.ToArray();
        }
    }
}
