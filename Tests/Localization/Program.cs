using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Naufal_Windows_Tech_s_Powertoys;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

try
{
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var options = UiTranslation.LanguageOptions;
var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
if (args.Contains("--dump"))
{
    Console.WriteLine(JsonSerializer.Serialize(options.ToDictionary(language => language.Code,
        language => UiTranslation.GetLanguageTable(language.Code)), jsonOptions));
    return;
}

int assertions = Regression.Run();
Console.WriteLine($"PASS: {assertions} localization assertions across {options.Count} languages.");
if (args.Contains("--test-only")) return;

var candidates = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
var uiCandidates = new HashSet<string>(StringComparer.Ordinal);
var interpolatedCandidates = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
{
    if (Path.GetFileName(file).StartsWith("UiTranslation") || Path.GetFileName(file).StartsWith("NativeUiCatalog") || Path.GetFileName(file) == "SupplementalUiCatalog.cs") continue;
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    foreach (var literal in syntax.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
    {
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
        string value = literal.Token.ValueText;
        if (value.Length < 3 || value.Length > 1300 || !Regex.IsMatch(value, "[A-Za-z]{3}") ||
            value.Contains('\\') || value.Contains("https:") || value.Contains("SELECT ") || value.Contains("<") ||
            value.Contains("HKEY_") || value.Contains("{\\")) continue;
        if (!candidates.TryGetValue(value, out var locations)) candidates[value] = locations = new();
        locations.Add(Path.GetFileName(file) + ":" + (literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
        if (literal.Ancestors().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            Regex.IsMatch(assignment.Left.ToString(), @"(?:^|\.)(?:Text|Content|Header|PlaceholderText)$"))) uiCandidates.Add(value);
    }
    foreach (var interpolated in syntax.GetRoot().DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
    {
        int argument = 0;
        string template = string.Concat(interpolated.Contents.Select(part => part is InterpolatedStringTextSyntax text
            ? text.TextToken.ValueText : "{" + argument++ + "}"));
        if (template.Length < 3 || template.Length > 1300 || !Regex.IsMatch(template, "[A-Za-z]{3}")) continue;
        if (!interpolatedCandidates.TryGetValue(template, out var locations)) interpolatedCandidates[template] = locations = new();
        locations.Add(Path.GetFileName(file) + ":" + (interpolated.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
    }
}
foreach (string file in Directory.EnumerateFiles(root, "*.xaml"))
foreach (var attribute in XDocument.Load(file).Descendants().Attributes().Where(a => a.Name.LocalName is "Text" or "Content" or "Header" or "PlaceholderText"))
{
    if (attribute.Value.StartsWith('{') || attribute.Value.Length < 3) continue;
    if (!candidates.TryGetValue(attribute.Value, out var locations)) candidates[attribute.Value] = locations = new();
    locations.Add(Path.GetFileName(file));
    uiCandidates.Add(attribute.Value);
}
var english = UiTranslation.GetLanguageTable("en");
var report = new
{
    Note = "Static candidate coverage, not visual/linguistic certification. Candidates include technical strings requiring triage; diagnostic output must stay original.",
    Languages = options.Select(language =>
    {
        var table = UiTranslation.GetLanguageTable(language.Code);
        return new { language.Code, language.Name, Entries = table.Count,
            MissingReferenceKeys = english.Keys.Count(key => !table.ContainsKey(key)),
            BlankValues = table.Count(pair => string.IsNullOrWhiteSpace(pair.Value)),
            PlaceholderMismatches = table.Where(pair => !Regression.Placeholders(pair.Key).SequenceEqual(Regression.Placeholders(pair.Value))).Select(pair => pair.Key).ToArray(),
            ChangedCandidates = candidates.Keys.Count(key => UiTranslation.Translate(key, language.Code) != key) };
    }).ToArray(),
    Candidates = candidates.Select(pair => new { Text = pair.Key, Locations = pair.Value,
        DirectDisplayAssignment = uiCandidates.Contains(pair.Key),
        MissingLanguages = options.Where(option => option.Code != "en" && !UiTranslation.GetLanguageTable(option.Code).ContainsKey(pair.Key) && UiTranslation.Translate(pair.Key, option.Code) == pair.Key).Select(option => option.Code).ToArray() }).ToArray(),
    InterpolatedCandidates = interpolatedCandidates.Select(pair => new { Template = pair.Key, Locations = pair.Value,
        ExactResourceTemplate = english.ContainsKey(pair.Key) }).ToArray()
};
string output = Path.Combine(root, "artifacts", "localization-audit");
Directory.CreateDirectory(output);
string reportFile = Path.Combine(output, args.Contains("--baseline") ? "baseline.json" : "coverage.json");
File.WriteAllText(reportFile, JsonSerializer.Serialize(report, jsonOptions));
Console.WriteLine(JsonSerializer.Serialize(new { report.Note, report.Languages, CandidateCount = candidates.Count, Report = reportFile }, jsonOptions));
}
catch (Exception exception)
{
    // A failing regression must return a nonzero console exit, not leave a
    // Windows Application Error dialog on the developer's desktop.
    Console.Error.WriteLine(exception.ToString());
    Environment.ExitCode = 1;
}
