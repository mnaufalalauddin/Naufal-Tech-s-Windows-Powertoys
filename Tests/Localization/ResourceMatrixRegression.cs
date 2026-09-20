using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Naufal_Windows_Tech_s_Powertoys;

internal static class ResourceMatrixRegression
{
    // Validate source matrices too: a complete-looking runtime dictionary can
    // hide an unfinished or accidentally unregistered new resource file.
    internal static int Run(string root)
    {
        int assertions = 0, matrices = 0;
        void Check(bool ok, string message)
        {
            assertions++;
            if (!ok) throw new InvalidOperationException("Resource matrix: " + message);
        }
        var languages = UiTranslation.LanguageOptions.Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(root, "NativeUiCatalog*.cs"))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var literal in tree.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                string data = literal.Token.ValueText;
                if (!data.StartsWith("en|", StringComparison.Ordinal)) continue;
                matrices++;
                string label = Path.GetFileName(file);
                string[] rows = data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string[] keys = rows[0].Split('|')[1..];
                Check(keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == keys.Length, label + " duplicate master key");
                Check(rows.Length == languages.Count, label + " must contain all 23 language rows");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string row in rows)
                {
                    string[] cells = row.Split('|');
                    Check(languages.Contains(cells[0]) && seen.Add(cells[0]), label + " unknown or duplicate language");
                    Check(cells.Length == keys.Length + 1, label + " column count: " + cells[0]);
                    var table = UiTranslation.GetLanguageTable(cells[0]);
                    for (int i = 0; i < keys.Length; i++)
                    {
                        string value = cells[i + 1];
                        Check(!string.IsNullOrWhiteSpace(value), label + " empty translation: " + keys[i]);
                        Check(!value.Contains('\uFFFD') && !value.Contains("???", StringComparison.Ordinal),
                            label + " corrupted translation: " + keys[i]);
                        Check(Regression.Placeholders(keys[i]).SequenceEqual(Regression.Placeholders(value)),
                            label + " placeholder mismatch: " + keys[i]);
                        Check(table.ContainsKey(keys[i]), label + " resource was not merged: " + keys[i]);
                    }
                }
            }
        }
        Check(matrices > 20, "source resource matrices discovered");
        return assertions;
    }
}
