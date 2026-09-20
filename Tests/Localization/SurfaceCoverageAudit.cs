using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Naufal_Windows_Tech_s_Powertoys;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class SurfaceCoverageAudit
{
    internal sealed record Surface(string Kind, string Text, string Location, string[] MissingLanguages,
        bool Resolved = true, string Expression = "");
    internal static Surface[] Inspect(string root)
    {
        List<(string Kind, string Text, string Location, bool Resolved, string Expression)> surfaces = new();
        foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("NativeUiCatalog") || name.StartsWith("UiTranslation") ||
                name == "SupplementalUiCatalog.cs") continue;
            SyntaxNode tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            void Add(string kind, ExpressionSyntax expression)
            {
                if (expression is ConditionalExpressionSyntax choice)
                {
                    Add(kind, choice.WhenTrue);
                    Add(kind, choice.WhenFalse);
                    return;
                }
                string? text = Constant(expression, tree);
                if (text is not null && (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, "[A-Za-z]{3}"))) return;
                surfaces.Add((kind, text ?? "", name + ":" +
                    (expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1), text is not null,
                    text is null ? expression.ToString() : ""));
            }
            void ResultArguments(string type, SeparatedSyntaxList<ArgumentSyntax> arguments)
            {
                string[] parameters = type switch
                {
                    "ToolActionResult" => ["Success", "Message", "SkippedUnavailable"],
                    "ToolToggleOperationResult" => ["Success", "Verified", "Message", "State"],
                    "ToolToggleState" => ["IsOn", "IsAvailable", "ActualValue", "Error"],
                    _ => []
                };
                for (int i = 0; i < arguments.Count; i++)
                {
                    string parameter = arguments[i].NameColon?.Name.Identifier.ValueText ??
                        (i < parameters.Length ? parameters[i] : "");
                    if (parameter is "Message" or "ActualValue" or "Error")
                        Add("Backend display " + parameter, arguments[i].Expression);
                }
            }
            void MetadataArguments(SeparatedSyntaxList<ArgumentSyntax> arguments, string[] parameterNames)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    string parameter = arguments[i].NameColon?.Name.Identifier.ValueText ??
                        (i < parameterNames.Length ? parameterNames[i] : "");
                    if (parameter.Equals("description", StringComparison.OrdinalIgnoreCase))
                        Add("Catalog description", arguments[i].Expression);
                    else if (parameter.Equals("warning", StringComparison.OrdinalIgnoreCase))
                        Add("Catalog warning", arguments[i].Expression);
                    else if (parameter.Contains("Confirmation", StringComparison.OrdinalIgnoreCase))
                        Add("Catalog confirmation", arguments[i].Expression);
                    else if (parameter is "RunLabel" or "RestoreLabel")
                        Add("Catalog button", arguments[i].Expression);
                }
            }
            foreach (AssignmentExpressionSyntax assignment in tree.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                string property = assignment.Left.ToString().Split('.').Last();
                if (property is "Description" or "Warning" or "Text" or "Content" or "Header" or "PlaceholderText")
                {
                    if (assignment.Right is ConditionalExpressionSyntax conditional)
                    {
                        Add(property, conditional.WhenTrue);
                        Add(property, conditional.WhenFalse);
                    }
                    else Add(property, assignment.Right);
                }
            }
            foreach (ObjectCreationExpressionSyntax creation in tree.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var arguments = creation.ArgumentList?.Arguments;
                if (arguments is null) continue;
                if (creation.Type.ToString() == "ToolToggleDefinition")
                    MetadataArguments(arguments.Value, ["Id", "Category", "Name", "Description",
                        "RequiresAdministrator", "RestartRecommended", "SelectionTier", "Warning", "IsFeatureSwitch"]);
                else if (creation.Type.ToString() == "ToolActionDefinition")
                    MetadataArguments(arguments.Value, ["Id", "Category", "Name", "Description", "RunLabel",
                        "RequiresAdministrator", "SupportsRestore", "Confirmation", "RestoreConfirmation",
                        "RestoreLabel", "Risk", "Warning"]);
                ResultArguments(creation.Type.ToString(), arguments.Value);
            }
            foreach (ImplicitObjectCreationExpressionSyntax creation in tree.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                // Resolve only an explicit variable declaration or direct
                // method return. Nested constructors remain unresolved rather
                // than guessing their runtime type from argument positions.
                string? type = (creation.Parent as EqualsValueClauseSyntax)?.Parent?.Parent is VariableDeclarationSyntax variable
                    ? variable.Type.ToString() : null;
                if (creation.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax)
                    type ??= creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.ReturnType.ToString();
                if (type is not null) ResultArguments(type, creation.ArgumentList.Arguments);
            }
            foreach (InvocationExpressionSyntax invocation in tree.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string method = invocation.Expression.ToString();
                var arguments = invocation.ArgumentList.Arguments;
                // Service Group arguments: id, risk, title, description, services.
                if (method == "Group" && name == "DebloatServiceGroupsService.cs" && arguments.Count > 3)
                    Add("Service description", arguments[3].Expression);
                // Local metadata factories (Lab, P, Create, Group, etc.) also
                // carry descriptions and warnings even when construction is target-typed.
                var factories = tree.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.ValueText == method &&
                        m.ParameterList.Parameters.Any(p => p.Identifier.ValueText.Equals("description", StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (factories.Length == 1)
                    MetadataArguments(arguments, factories[0].ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray());
                if (method.EndsWith("ShowConfirmationWindowAsync") || method.EndsWith("ShowMessageDialogAsync"))
                    foreach (var argument in arguments.Take(4)) Add("Dialog", argument.Expression);
                if (method == "ToolToggleState.Unavailable" && arguments.Count > 0)
                    Add("Backend availability reason", arguments[0].Expression);
            }
        }
        foreach (string file in Directory.EnumerateFiles(root, "*.xaml"))
            foreach (var attribute in XDocument.Load(file).Descendants().Attributes()
                .Where(a => a.Name.LocalName is "Text" or "Content" or "Header" or "PlaceholderText"))
                if (!attribute.Value.StartsWith('{'))
                    surfaces.Add(("XAML", attribute.Value, Path.GetFileName(file), true, ""));
        return surfaces.Distinct().Select(surface => new Surface(surface.Kind, surface.Text, surface.Location,
            UiTranslation.LanguageOptions.Where(language => surface.Resolved && language.Code != "en" &&
                UiTranslation.Translate(surface.Text, language.Code) == surface.Text)
                .Select(language => language.Code).ToArray(), surface.Resolved, surface.Expression)).ToArray();
    }

    // No execution of app source, registry/service calls or interpolation values.
    private static string? Constant(ExpressionSyntax expression, SyntaxNode tree, int depth = 0)
    {
        if (depth > 8) return null;
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            return literal.Token.ValueText;
        if (expression is ParenthesizedExpressionSyntax parenthesized) return Constant(parenthesized.Expression, tree, depth + 1);
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) &&
            Constant(binary.Left, tree, depth + 1) is string left && Constant(binary.Right, tree, depth + 1) is string right)
            return left + right;
        if (expression is IdentifierNameSyntax identifier)
        {
            // Only resolve unique const declarations. Never execute an initializer.
            var values = tree.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Identifier.ValueText == identifier.Identifier.ValueText && v.Initializer is not null &&
                    (v.Ancestors().OfType<FieldDeclarationSyntax>().Any(f => f.Modifiers.Any(SyntaxKind.ConstKeyword)) ||
                     v.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any(l => l.Modifiers.Any(SyntaxKind.ConstKeyword)))).ToArray();
            if (values.Length == 1) return Constant(values[0].Initializer!.Value, tree, depth + 1);
        }
        return null;
    }
}
